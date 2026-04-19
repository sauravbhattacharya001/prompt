namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="PromptTokenCounter"/> — token estimation, cost
    /// calculation, batch operations, and model management.
    /// </summary>
    public class PromptTokenCounterTests
    {
        // ── Estimate ────────────────────────────────────────────

        [Fact]
        public void Estimate_EmptyString_ReturnsZeroCounts()
        {
            var counter = new PromptTokenCounter();
            var result = counter.Estimate("");
            Assert.Equal(0, result.TokenCount);
            Assert.Equal(0, result.CharCount);
            Assert.Equal(0, result.WordCount);
        }

        [Fact]
        public void Estimate_NullString_ReturnsZeroCounts()
        {
            var counter = new PromptTokenCounter();
            var result = counter.Estimate(null);
            Assert.Equal(0, result.TokenCount);
            Assert.Equal(0, result.CharCount);
            Assert.Equal(0, result.WordCount);
        }

        [Fact]
        public void Estimate_SingleShortWord_ReturnsOneToken()
        {
            var counter = new PromptTokenCounter();
            var result = counter.Estimate("Hi");
            Assert.Equal(1, result.TokenCount);
            Assert.Equal(2, result.CharCount);
            Assert.Equal(1, result.WordCount);
        }

        [Fact]
        public void Estimate_MultipleLongWords_TokensGreaterThanWordCount()
        {
            var counter = new PromptTokenCounter();
            var result = counter.Estimate("extraordinary accomplishments internationalization");
            // Each long word should produce multiple tokens
            Assert.True(result.TokenCount > result.WordCount,
                $"Expected TokenCount ({result.TokenCount}) > WordCount ({result.WordCount})");
            Assert.Equal(3, result.WordCount);
        }

        [Fact]
        public void Estimate_PunctuationHeavyText_CountsExtraTokensForPunctuation()
        {
            var counter = new PromptTokenCounter();
            var noPunct = counter.Estimate("hello world");
            var withPunct = counter.Estimate("hello!!! world???");
            // Heavy punctuation should add extra tokens
            Assert.True(withPunct.TokenCount > noPunct.TokenCount,
                $"Punctuated ({withPunct.TokenCount}) should exceed plain ({noPunct.TokenCount})");
        }

        [Fact]
        public void Estimate_ShortWords_EachCountsAsOneToken()
        {
            var counter = new PromptTokenCounter();
            // All words <= 4 chars → 1 token each
            var result = counter.Estimate("the cat sat on a mat");
            Assert.Equal(6, result.WordCount);
            Assert.Equal(6, result.TokenCount);
        }

        [Fact]
        public void Estimate_PreservesOriginalText()
        {
            var counter = new PromptTokenCounter();
            var text = "Hello, world!";
            var result = counter.Estimate(text);
            Assert.Equal(text, result.Text);
        }

        // ── EstimateBatch ───────────────────────────────────────

        [Fact]
        public void EstimateBatch_ReturnsOneResultPerInput()
        {
            var counter = new PromptTokenCounter();
            var texts = new[] { "Hello", "World", "Foo bar baz" };
            var results = counter.EstimateBatch(texts);
            Assert.Equal(3, results.Count);
        }

        [Fact]
        public void EstimateBatch_NullInput_ThrowsArgumentNullException()
        {
            var counter = new PromptTokenCounter();
            Assert.Throws<ArgumentNullException>(() => counter.EstimateBatch(null));
        }

        [Fact]
        public void EstimateBatch_EmptyList_ReturnsEmptyResults()
        {
            var counter = new PromptTokenCounter();
            var results = counter.EstimateBatch(Array.Empty<string>());
            Assert.Empty(results);
        }

        // ── EstimateCost ────────────────────────────────────────

        [Fact]
        public void EstimateCost_ValidModel_ReturnsCostResult()
        {
            var counter = new PromptTokenCounter();
            var cost = counter.EstimateCost("Hello world", "gpt-4o", estimatedOutputTokens: 100);
            Assert.Equal("gpt-4o", cost.Model);
            Assert.True(cost.InputTokens > 0);
            Assert.Equal(100, cost.OutputTokens);
            Assert.True(cost.TotalCost >= 0);
            Assert.Equal(cost.InputCost + cost.OutputCost, cost.TotalCost);
        }

        [Fact]
        public void EstimateCost_UnknownModel_ThrowsArgumentException()
        {
            var counter = new PromptTokenCounter();
            Assert.Throws<ArgumentException>(() => counter.EstimateCost("test", "nonexistent-model"));
        }

        [Fact]
        public void EstimateCost_ZeroOutputTokens_OutputCostIsZero()
        {
            var counter = new PromptTokenCounter();
            var cost = counter.EstimateCost("Hello world", "gpt-4o", estimatedOutputTokens: 0);
            Assert.Equal(0m, cost.OutputCost);
        }

        [Fact]
        public void EstimateCost_CaseInsensitiveModelLookup()
        {
            var counter = new PromptTokenCounter();
            var cost = counter.EstimateCost("test", "GPT-4O");
            Assert.Equal("gpt-4o", cost.Model);
        }

        // ── CompareCosts ────────────────────────────────────────

        [Fact]
        public void CompareCosts_ReturnsSortedByCostAscending()
        {
            var counter = new PromptTokenCounter();
            var rows = counter.CompareCosts("Explain quantum physics in detail", estimatedOutputTokens: 500);

            Assert.True(rows.Count >= 2, "Should have multiple models");

            for (int i = 1; i < rows.Count; i++)
            {
                Assert.True(rows[i].TotalCost >= rows[i - 1].TotalCost,
                    $"{rows[i].Model} (${rows[i].TotalCost}) should be >= {rows[i - 1].Model} (${rows[i - 1].TotalCost})");
            }
        }

        [Fact]
        public void CompareCosts_AllModelsRepresented()
        {
            var counter = new PromptTokenCounter();
            var rows = counter.CompareCosts("test", estimatedOutputTokens: 0);
            var models = counter.GetModels();
            Assert.Equal(models.Count, rows.Count);
        }

        // ── FormatCostComparison ────────────────────────────────

        [Fact]
        public void FormatCostComparison_ContainsModelNames()
        {
            var counter = new PromptTokenCounter();
            var formatted = counter.FormatCostComparison("Hello world", estimatedOutputTokens: 100);
            Assert.Contains("gpt-4o", formatted);
            Assert.Contains("Token Estimate", formatted);
        }

        [Fact]
        public void FormatCostComparison_LongTextIsTruncated()
        {
            var counter = new PromptTokenCounter();
            var longText = new string('x', 100);
            var formatted = counter.FormatCostComparison(longText, estimatedOutputTokens: 0);
            Assert.Contains("...", formatted);
        }

        // ── EstimateBatchCost ───────────────────────────────────

        [Fact]
        public void EstimateBatchCost_SumsAllInputTokens()
        {
            var counter = new PromptTokenCounter();
            var texts = new[] { "Hello", "World", "Foo" };
            var batchCost = counter.EstimateBatchCost(texts, "gpt-4o", estimatedOutputTokensEach: 10);

            var individualSum = texts.Sum(t => counter.Estimate(t).TokenCount);
            Assert.Equal(individualSum, batchCost.InputTokens);
            Assert.Equal(30, batchCost.OutputTokens); // 10 * 3
        }

        [Fact]
        public void EstimateBatchCost_NullTexts_ThrowsArgumentNullException()
        {
            var counter = new PromptTokenCounter();
            Assert.Throws<ArgumentNullException>(() => counter.EstimateBatchCost(null, "gpt-4o"));
        }

        [Fact]
        public void EstimateBatchCost_UnknownModel_ThrowsArgumentException()
        {
            var counter = new PromptTokenCounter();
            Assert.Throws<ArgumentException>(() =>
                counter.EstimateBatchCost(new[] { "test" }, "unknown-model"));
        }

        // ── Model management ────────────────────────────────────

        [Fact]
        public void WithModel_AddsCustomModel()
        {
            var counter = new PromptTokenCounter();
            int originalCount = counter.GetModels().Count;
            counter.WithModel("custom-model", "Custom", "Custom Model", 1.0m, 2.0m);
            Assert.Equal(originalCount + 1, counter.GetModels().Count);

            var cost = counter.EstimateCost("test", "custom-model", estimatedOutputTokens: 10);
            Assert.Equal("custom-model", cost.Model);
        }

        [Fact]
        public void AddModel_NullPricing_ThrowsArgumentNullException()
        {
            var counter = new PromptTokenCounter();
            Assert.Throws<ArgumentNullException>(() => counter.AddModel(null));
        }

        [Fact]
        public void AddModel_ReplacesExistingModel()
        {
            var counter = new PromptTokenCounter();
            int originalCount = counter.GetModels().Count;
            // Re-add an existing model with different pricing
            counter.WithModel("gpt-4o", "OpenAI", "GPT-4o Custom", 100.0m, 200.0m);
            Assert.Equal(originalCount, counter.GetModels().Count); // count unchanged

            var cost = counter.EstimateCost("test", "gpt-4o", estimatedOutputTokens: 0);
            // Should use the new (higher) pricing
            Assert.True(cost.InputCost > 0);
        }

        [Fact]
        public void GetModels_ReturnsBuiltInModels()
        {
            var counter = new PromptTokenCounter();
            var models = counter.GetModels();
            Assert.True(models.Count >= 8, "Should have at least 8 built-in models");
            Assert.Contains(models, m => m.ModelId == "gpt-4o");
            Assert.Contains(models, m => m.ModelId == "claude-3.5-sonnet");
        }

        // ── Fluent chaining ─────────────────────────────────────

        [Fact]
        public void FluentChaining_ReturnsSameInstance()
        {
            var counter = new PromptTokenCounter();
            var returned = counter
                .WithModel("m1", "P", "M1", 1m, 1m)
                .WithModel("m2", "P", "M2", 2m, 2m);
            Assert.Same(counter, returned);
        }
    }
}
