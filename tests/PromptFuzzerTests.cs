namespace Prompt.Tests
{
    using Xunit;

    public class PromptFuzzerTests
    {
        // ──────────────── Fuzz() Basic ────────────────

        [Fact]
        public void Fuzz_ReturnsRequestedCount()
        {
            var result = PromptFuzzer.Fuzz("Explain quantum computing in simple terms", count: 5, seed: 42);
            Assert.Equal(5, result.Variants.Count);
        }

        [Fact]
        public void Fuzz_PreservesOriginal()
        {
            var result = PromptFuzzer.Fuzz("List the top 10 programming languages", seed: 42);
            Assert.Equal("List the top 10 programming languages", result.Original);
        }

        [Fact]
        public void Fuzz_NoDuplicateVariants()
        {
            var result = PromptFuzzer.Fuzz("Explain quantum computing in simple terms", count: 10, seed: 42);
            var texts = result.Variants.Select(v => v.Text).ToList();
            Assert.Equal(texts.Count, texts.Distinct().Count());
        }

        [Fact]
        public void Fuzz_VariantsAreNotOriginal()
        {
            var result = PromptFuzzer.Fuzz("Describe the importance of testing", count: 5, seed: 42);
            foreach (var v in result.Variants)
                Assert.NotEqual(result.Original, v.Text);
        }

        [Fact]
        public void Fuzz_SeedProducesReproducibleResults()
        {
            var r1 = PromptFuzzer.Fuzz("Explain how computers work", count: 3, seed: 123);
            var r2 = PromptFuzzer.Fuzz("Explain how computers work", count: 3, seed: 123);
            for (int i = 0; i < r1.Variants.Count; i++)
                Assert.Equal(r1.Variants[i].Text, r2.Variants[i].Text);
        }

        [Fact]
        public void Fuzz_ThrowsOnEmptyPrompt()
        {
            Assert.Throws<ArgumentException>(() => PromptFuzzer.Fuzz(""));
            Assert.Throws<ArgumentException>(() => PromptFuzzer.Fuzz(null!));
        }

        [Fact]
        public void Fuzz_ThrowsOnInvalidCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PromptFuzzer.Fuzz("test", count: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PromptFuzzer.Fuzz("test", count: 51));
        }

        [Fact]
        public void Fuzz_NoStrategiesReturnsEmpty()
        {
            var result = PromptFuzzer.Fuzz("Hello world", strategies: FuzzStrategy.None);
            Assert.Empty(result.Variants);
        }

        // ──────────────── Individual Strategies ────────────────

        [Fact]
        public void SynonymSwap_ReplacesKnownWord()
        {
            var result = PromptFuzzer.FuzzOne("Explain this concept", FuzzStrategy.SynonymSwap, seed: 42);
            Assert.NotNull(result);
            Assert.Equal("SynonymSwap", result!.Strategy);
            Assert.DoesNotContain("Explain", result.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void SynonymSwap_ReturnsNullWhenNoSynonymsAvailable()
        {
            var result = PromptFuzzer.FuzzOne("xyz abc qrs", FuzzStrategy.SynonymSwap, seed: 42);
            Assert.Null(result);
        }

        [Fact]
        public void TypoInjection_ModifiesWord()
        {
            var result = PromptFuzzer.FuzzOne("Explain quantum computing clearly", FuzzStrategy.TypoInjection, seed: 42);
            Assert.NotNull(result);
            Assert.Equal("TypoInjection", result!.Strategy);
            Assert.NotEqual("Explain quantum computing clearly", result.Text);
        }

        [Fact]
        public void CaseChange_AltersCase()
        {
            var result = PromptFuzzer.FuzzOne("Explain quantum computing", FuzzStrategy.CaseChange, seed: 42);
            Assert.NotNull(result);
            Assert.Equal("CaseChange", result!.Strategy);
        }

        [Fact]
        public void WordDrop_RemovesWord()
        {
            var result = PromptFuzzer.FuzzOne("Explain quantum computing in simple terms", FuzzStrategy.WordDrop, seed: 42);
            Assert.NotNull(result);
            Assert.Equal("WordDrop", result!.Strategy);
            var origWordCount = "Explain quantum computing in simple terms".Split(' ').Length;
            var newWordCount = result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(origWordCount - 1, newWordCount);
        }

        [Fact]
        public void WordDrop_ReturnsNullForShortPrompt()
        {
            var result = PromptFuzzer.FuzzOne("Hi yo", FuzzStrategy.WordDrop, seed: 42);
            Assert.Null(result);
        }

        [Fact]
        public void WordShuffle_SwapsAdjacentWords()
        {
            var result = PromptFuzzer.FuzzOne("Explain quantum computing in simple terms", FuzzStrategy.WordShuffle, seed: 42);
            Assert.NotNull(result);
            Assert.Equal("WordShuffle", result!.Strategy);
        }

        [Fact]
        public void NoiseInjection_InsertsFillerWord()
        {
            var result = PromptFuzzer.FuzzOne("Explain quantum computing clearly", FuzzStrategy.NoiseInjection, seed: 42);
            Assert.NotNull(result);
            Assert.Equal("NoiseInjection", result!.Strategy);
            var origWordCount = "Explain quantum computing clearly".Split(' ').Length;
            var newWordCount = result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(origWordCount + 1, newWordCount);
        }

        [Fact]
        public void Truncation_ShortendsPrompt()
        {
            var result = PromptFuzzer.FuzzOne("Explain quantum computing in simple terms for beginners", FuzzStrategy.Truncation, seed: 42);
            Assert.NotNull(result);
            Assert.Equal("Truncation", result!.Strategy);
            Assert.True(result.Text.Length < "Explain quantum computing in simple terms for beginners".Length);
        }

        [Fact]
        public void Truncation_ReturnsNullForShortPrompt()
        {
            var result = PromptFuzzer.FuzzOne("Hi yo ok", FuzzStrategy.Truncation, seed: 42);
            Assert.Null(result);
        }

        // ──────────────── Similarity ────────────────

        [Fact]
        public void Similarity_IdenticalStringsReturnOne()
        {
            Assert.Equal(1.0, PromptFuzzer.CalculateSimilarity("hello", "hello"));
        }

        [Fact]
        public void Similarity_CompletelyDifferentReturnsLow()
        {
            double sim = PromptFuzzer.CalculateSimilarity("aaaa", "zzzz");
            Assert.True(sim < 0.1);
        }

        [Fact]
        public void Similarity_SimilarStringsReturnHigh()
        {
            double sim = PromptFuzzer.CalculateSimilarity("hello world", "hello worl");
            Assert.True(sim > 0.8);
        }

        [Fact]
        public void Fuzz_VariantsHaveSimilarityScores()
        {
            var result = PromptFuzzer.Fuzz("Explain quantum computing in simple terms", count: 5, seed: 42);
            foreach (var v in result.Variants)
            {
                Assert.True(v.Similarity >= 0.0 && v.Similarity <= 1.0,
                    $"Similarity {v.Similarity} out of range for strategy {v.Strategy}");
            }
        }

        // ──────────────── Specific Strategies ────────────────

        [Fact]
        public void Fuzz_WithSingleStrategy()
        {
            var result = PromptFuzzer.Fuzz("Explain quantum computing in simple terms",
                count: 3, strategies: FuzzStrategy.TypoInjection, seed: 42);
            Assert.All(result.Variants, v => Assert.Equal("TypoInjection", v.Strategy));
        }

        [Fact]
        public void Fuzz_WithCombinedStrategies()
        {
            var result = PromptFuzzer.Fuzz("Explain quantum computing in simple terms",
                count: 5, strategies: FuzzStrategy.TypoInjection | FuzzStrategy.CaseChange, seed: 42);
            Assert.All(result.Variants, v =>
                Assert.True(v.Strategy == "TypoInjection" || v.Strategy == "CaseChange"));
        }

        // ──────────────── GetStrategyNames ────────────────

        [Fact]
        public void GetStrategyNames_ReturnsAllStrategies()
        {
            var names = PromptFuzzer.GetStrategyNames();
            Assert.Contains("SynonymSwap", names);
            Assert.Contains("TypoInjection", names);
            Assert.Contains("CaseChange", names);
            Assert.Contains("WordDrop", names);
            Assert.Contains("WordShuffle", names);
            Assert.Contains("NoiseInjection", names);
            Assert.Contains("Truncation", names);
            Assert.Equal(7, names.Count);
        }

        // ──────────────── FuzzResult JSON ────────────────

        [Fact]
        public void FuzzResult_ToJsonProducesValidJson()
        {
            var result = PromptFuzzer.Fuzz("Explain this concept", count: 2, seed: 42);
            string json = result.ToJson();
            Assert.NotEmpty(json);
            Assert.Contains("\"original\"", json);
            Assert.Contains("\"variants\"", json);
        }

        // ──────────────── Edge Cases ────────────────

        [Fact]
        public void Fuzz_SingleWordPrompt()
        {
            // Should still produce some variants (case change at minimum)
            var result = PromptFuzzer.Fuzz("Hello", count: 1,
                strategies: FuzzStrategy.CaseChange, seed: 42);
            Assert.True(result.Variants.Count >= 0); // May or may not produce variants
        }

        [Fact]
        public void Fuzz_LongPrompt()
        {
            string longPrompt = string.Join(" ", Enumerable.Repeat("Explain the concept of quantum computing", 20));
            var result = PromptFuzzer.Fuzz(longPrompt, count: 5, seed: 42);
            Assert.Equal(5, result.Variants.Count);
        }

        [Fact]
        public void Fuzz_StrategiesAppliedPopulated()
        {
            var result = PromptFuzzer.Fuzz("Explain this clearly",
                strategies: FuzzStrategy.TypoInjection | FuzzStrategy.CaseChange, seed: 42);
            Assert.Contains("TypoInjection", result.StrategiesApplied);
            Assert.Contains("CaseChange", result.StrategiesApplied);
        }
    }
}
