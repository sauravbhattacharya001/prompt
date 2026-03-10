namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptSplitterTests
    {
        // ── Construction ────────────────────────────────────────────────

        [Fact]
        public void DefaultConfig_CreatesSplitter()
        {
            var splitter = new PromptSplitter();
            Assert.NotNull(splitter);
        }

        [Fact]
        public void MaxTokensTooLow_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PromptSplitter(new SplitterConfig { MaxTokensPerChunk = 5 }));
        }

        [Fact]
        public void NegativeOverlap_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PromptSplitter(new SplitterConfig { OverlapTokens = -1 }));
        }

        [Fact]
        public void OverlapExceedsMax_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PromptSplitter(new SplitterConfig
                {
                    MaxTokensPerChunk = 100,
                    OverlapTokens = 100
                }));
        }

        [Fact]
        public void ZeroCharsPerToken_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PromptSplitter(new SplitterConfig { CharsPerToken = 0 }));
        }

        // ── EstimateTokens ──────────────────────────────────────────────

        [Fact]
        public void EstimateTokens_EmptyString_ReturnsZero()
        {
            var splitter = new PromptSplitter();
            Assert.Equal(0, splitter.EstimateTokens(""));
        }

        [Fact]
        public void EstimateTokens_NullString_ReturnsZero()
        {
            var splitter = new PromptSplitter();
            Assert.Equal(0, splitter.EstimateTokens(null!));
        }

        [Fact]
        public void EstimateTokens_FourChars_ReturnsOne()
        {
            var splitter = new PromptSplitter(new SplitterConfig { CharsPerToken = 4.0 });
            Assert.Equal(1, splitter.EstimateTokens("word"));
        }

        [Fact]
        public void EstimateTokens_RoundsUp()
        {
            var splitter = new PromptSplitter(new SplitterConfig { CharsPerToken = 4.0 });
            // 5 chars / 4 = 1.25, ceiling = 2
            Assert.Equal(2, splitter.EstimateTokens("hello"));
        }

        // ── Split — no splitting needed ─────────────────────────────────

        [Fact]
        public void Split_ShortText_SingleChunk()
        {
            var splitter = new PromptSplitter(new SplitterConfig { MaxTokensPerChunk = 1000 });
            var result = splitter.Split("Hello world.");
            Assert.Single(result.Chunks);
            Assert.False(result.WasSplit);
            Assert.True(result.Chunks[0].IsFirst);
            Assert.True(result.Chunks[0].IsLast);
        }

        [Fact]
        public void Split_NullText_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PromptSplitter().Split(null!));
        }

        [Fact]
        public void Split_EmptyText_SingleChunk()
        {
            var result = new PromptSplitter().Split("");
            Assert.Single(result.Chunks);
            Assert.Equal(0, result.Chunks[0].EstimatedTokens);
        }

        // ── Paragraph strategy ──────────────────────────────────────────

        [Fact]
        public void Split_Paragraph_SplitsOnDoubleNewline()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(i => new string('x', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);
            Assert.True(result.Chunks.Count >= 2);

            // Each chunk should be under the limit
            foreach (var chunk in result.Chunks)
            {
                Assert.True(chunk.EstimatedTokens <= 200,
                    $"Chunk {chunk.Index} has {chunk.EstimatedTokens} tokens (max 200)");
            }
        }

        // ── Sentence strategy ───────────────────────────────────────────

        [Fact]
        public void Split_Sentence_SplitsOnSentenceBoundaries()
        {
            // Build text with many sentences
            string text = string.Join(" ",
                Enumerable.Range(0, 50).Select(i => $"This is sentence number {i}."));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 50,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Sentence,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);
        }

        // ── Line strategy ───────────────────────────────────────────────

        [Fact]
        public void Split_Line_SplitsOnNewlines()
        {
            string text = string.Join("\n", Enumerable.Range(0, 100)
                .Select(i => $"Line {i}: some content here."));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 40,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Line,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);
            Assert.True(result.Chunks.Count >= 3);
        }

        // ── Fixed strategy ──────────────────────────────────────────────

        [Fact]
        public void Split_Fixed_SplitsAtExactBoundary()
        {
            string text = new string('a', 1000);

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 100,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Fixed,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);
            // 1000 chars / (100 tokens * 4 chars) = 2.5 → at least 3 chunks
            Assert.True(result.Chunks.Count >= 3);
        }

        // ── Heading strategy ────────────────────────────────────────────

        [Fact]
        public void Split_Heading_SplitsOnMarkdownHeadings()
        {
            var sections = Enumerable.Range(0, 10)
                .Select(i => $"## Section {i}\n\n{new string('w', 300)}")
                .ToList();
            string text = string.Join("\n\n", sections);

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Heading,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);
        }

        [Fact]
        public void Split_Heading_FallsBackToParagraphWhenNoHeadings()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('z', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Heading,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);
        }

        // ── CodeBlock strategy ──────────────────────────────────────────

        [Fact]
        public void Split_CodeBlock_KeepsCodeBlocksTogether()
        {
            string text = "Intro text.\n\n" +
                "```csharp\nvar x = 1;\nvar y = 2;\n```\n\n" +
                "Middle text here with some explanation.\n\n" +
                "```python\ndef foo():\n    return 42\n```\n\n" +
                new string('m', 600);

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 100,
                OverlapTokens = 0,
                Strategy = SplitStrategy.CodeBlock,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.Chunks.Count >= 2);
        }

        // ── Overlap ─────────────────────────────────────────────────────

        [Fact]
        public void Split_WithOverlap_AddsOverlapText()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(i => new string((char)('a' + (i % 26)), 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 20,
                Strategy = SplitStrategy.Paragraph,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);

            // Second chunk should have overlap text
            if (result.Chunks.Count > 1)
            {
                Assert.True(result.Chunks[1].OverlapText.Length > 0,
                    "Second chunk should have overlap text");
            }

            // First chunk should NOT have overlap
            Assert.Equal("", result.Chunks[0].OverlapText);
        }

        // ── Headers ─────────────────────────────────────────────────────

        [Fact]
        public void Split_WithHeaders_AddsPartHeaders()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('h', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph,
                AddHeaders = true,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);

            Assert.Contains("[Part 1/", result.Chunks[0].Content);
            Assert.StartsWith("[Part 1/", result.Chunks[0].Header);
        }

        // ── System prompt ───────────────────────────────────────────────

        [Fact]
        public void Split_WithSystemPrompt_PrependedToEveryChunk()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('s', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph,
                SystemPrompt = "You are an analyst.",
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);

            foreach (var chunk in result.Chunks)
            {
                Assert.StartsWith("You are an analyst.", chunk.Content);
            }
        }

        // ── Continuation note ───────────────────────────────────────────

        [Fact]
        public void Split_WithContinuationNote_AppendedToNonFinalChunks()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('c', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph,
                ContinuationNote = "[continued in next part]",
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);

            // Non-final chunks should have the note
            for (int i = 0; i < result.Chunks.Count - 1; i++)
            {
                Assert.Contains("[continued in next part]", result.Chunks[i].Content);
            }

            // Final chunk should NOT have the note
            Assert.DoesNotContain("[continued in next part]", result.Chunks.Last().Content);
        }

        // ── Chunk metadata ──────────────────────────────────────────────

        [Fact]
        public void Chunks_HaveCorrectIndexAndTotal()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 30)
                .Select(_ => new string('q', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);

            for (int i = 0; i < result.Chunks.Count; i++)
            {
                Assert.Equal(i, result.Chunks[i].Index);
                Assert.Equal(result.Chunks.Count, result.Chunks[i].TotalChunks);
            }
        }

        [Fact]
        public void FirstChunk_IsFirstTrue_IsLastFalse()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('f', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph, CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.Chunks[0].IsFirst);
            if (result.WasSplit)
                Assert.False(result.Chunks[0].IsLast);
        }

        [Fact]
        public void LastChunk_IsLastTrue_IsFirstFalse()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('l', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph, CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            if (result.WasSplit)
            {
                Assert.True(result.Chunks.Last().IsLast);
                Assert.False(result.Chunks.Last().IsFirst);
            }
        }

        // ── Offsets ─────────────────────────────────────────────────────

        [Fact]
        public void Chunks_HaveNonOverlappingOffsets()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('o', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph, CharsPerToken = 4.0
            });

            var result = splitter.Split(text);

            for (int i = 1; i < result.Chunks.Count; i++)
            {
                Assert.True(result.Chunks[i].StartOffset >= result.Chunks[i - 1].EndOffset,
                    $"Chunk {i} start ({result.Chunks[i].StartOffset}) < chunk {i - 1} end ({result.Chunks[i - 1].EndOffset})");
            }
        }

        // ── SplitToStrings ──────────────────────────────────────────────

        [Fact]
        public void SplitToStrings_ReturnsContentStrings()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('t', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph, CharsPerToken = 4.0
            });

            var strings = splitter.SplitToStrings(text);
            Assert.True(strings.Count >= 2);
            foreach (var s in strings)
            {
                Assert.False(string.IsNullOrEmpty(s));
            }
        }

        // ── Reassemble ──────────────────────────────────────────────────

        [Fact]
        public void Reassemble_NullChunks_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PromptSplitter().Reassemble(null!));
        }

        [Fact]
        public void Reassemble_EmptyList_ReturnsEmpty()
        {
            var result = new PromptSplitter().Reassemble(new List<PromptChunk>());
            Assert.Equal("", result);
        }

        // ── ToJson ──────────────────────────────────────────────────────

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var splitter = new PromptSplitter();
            var result = splitter.Split("Hello world.");
            string json = splitter.ToJson(result);
            Assert.Contains("\"chunks\"", json);
            Assert.Contains("\"totalTokens\"", json);
        }

        [Fact]
        public void ToJson_NullResult_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PromptSplitter().ToJson(null!));
        }

        // ── SplitResult metadata ────────────────────────────────────────

        [Fact]
        public void SplitResult_HasCorrectMetadata()
        {
            string text = "Short text.";
            var splitter = new PromptSplitter(new SplitterConfig { MaxTokensPerChunk = 1000 });
            var result = splitter.Split(text);

            Assert.Equal(SplitStrategy.Paragraph, result.Strategy);
            Assert.Equal(text.Length, result.OriginalLength);
            Assert.True(result.TotalTokens > 0);
        }

        // ── Large document ──────────────────────────────────────────────

        [Fact]
        public void Split_LargeDocument_AllChunksUnderLimit()
        {
            var paragraphs = Enumerable.Range(0, 100)
                .Select(i => $"Paragraph {i}. " + new string('x', 150 + (i * 3)));
            string text = string.Join("\n\n", paragraphs);

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 500,
                OverlapTokens = 50,
                Strategy = SplitStrategy.Paragraph,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);

            Assert.True(result.Chunks.Count >= 5,
                $"Expected at least 5 chunks for large doc, got {result.Chunks.Count}");
        }

        // ── Header property ─────────────────────────────────────────────

        [Fact]
        public void Chunk_Header_FormatsCorrectly()
        {
            var chunk = new PromptChunk { Index = 2, TotalChunks = 5 };
            Assert.Equal("[Part 3/5]", chunk.Header);
        }

        // ── CustomCharsPerToken ─────────────────────────────────────────

        [Fact]
        public void Split_CustomCharsPerToken_AffectsChunking()
        {
            string text = new string('x', 1000);

            // With 2 chars per token, 1000 chars = 500 tokens
            var splitter2 = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = SplitStrategy.Fixed, CharsPerToken = 2.0
            });

            // With 10 chars per token, 1000 chars = 100 tokens
            var splitter10 = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = SplitStrategy.Fixed, CharsPerToken = 10.0
            });

            var r2 = splitter2.Split(text);
            var r10 = splitter10.Split(text);

            Assert.True(r2.Chunks.Count > r10.Chunks.Count,
                $"Expected more chunks with smaller charsPerToken ({r2.Chunks.Count} vs {r10.Chunks.Count})");
        }

        // ── Force sub-split of oversized segment ────────────────────────

        [Fact]
        public void Split_OversizedParagraph_ForceSubSplits()
        {
            string text = new string('g', 2000);

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 100,
                OverlapTokens = 0,
                Strategy = SplitStrategy.Paragraph,
                CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.Chunks.Count >= 5,
                $"Expected at least 5 chunks for 2000-char blob, got {result.Chunks.Count}");
        }

        // ── CodeBlock fallback ──────────────────────────────────────────

        [Fact]
        public void Split_CodeBlock_FallsToParagraphWhenNoFences()
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 20)
                .Select(_ => new string('n', 200)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = SplitStrategy.CodeBlock, CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.WasSplit);
        }

        // ── All strategies produce valid results ────────────────────────

        [Theory]
        [InlineData(SplitStrategy.Paragraph)]
        [InlineData(SplitStrategy.Sentence)]
        [InlineData(SplitStrategy.Line)]
        [InlineData(SplitStrategy.Fixed)]
        [InlineData(SplitStrategy.Heading)]
        [InlineData(SplitStrategy.CodeBlock)]
        public void Split_AllStrategies_ProduceNonEmptyChunks(SplitStrategy strategy)
        {
            string text = string.Join("\n\n", Enumerable.Range(0, 30)
                .Select(i => $"## Section {i}\n\nThis is paragraph {i}. " + new string('z', 100)));

            var splitter = new PromptSplitter(new SplitterConfig
            {
                MaxTokensPerChunk = 200, OverlapTokens = 0,
                Strategy = strategy, CharsPerToken = 4.0
            });

            var result = splitter.Split(text);
            Assert.True(result.Chunks.Count >= 1);
            foreach (var chunk in result.Chunks)
            {
                Assert.False(string.IsNullOrEmpty(chunk.Content));
            }
        }
    }
}
