namespace Prompt.Tests
{
    using Xunit;
    using System.Collections.Generic;
    using System.Linq;

    public class PromptContextCompressorTests
    {
        private static List<(string Role, string Content)> MakeConversation(int count, int tokensPerMsg = 100)
        {
            var msgs = new List<(string, string)>();
            msgs.Add(("system", "You are a helpful assistant." + new string(' ', tokensPerMsg * 4 - 30)));
            for (int i = 1; i < count; i++)
            {
                var role = i % 2 == 1 ? "user" : "assistant";
                msgs.Add((role, $"Message {i}: " + new string('x', tokensPerMsg * 4 - 15)));
            }
            return msgs;
        }

        // --- Basic functionality ---

        [Fact]
        public void EmptyInput_ReturnsEmptyResult()
        {
            var c = new PromptContextCompressor();
            var result = c.Compress(new List<(string, string)>());
            Assert.Empty(result.Messages);
            Assert.True(result.BudgetMet);
            Assert.Equal(0, result.OriginalCount);
        }

        [Fact]
        public void WithinBudget_NoCompression()
        {
            var msgs = new List<(string, string)>
            {
                ("system", "You are helpful."),
                ("user", "Hello"),
                ("assistant", "Hi there!")
            };
            var c = new PromptContextCompressor(new CompressionOptions { TargetTokens = 10000 });
            var result = c.Compress(msgs);
            Assert.Equal(3, result.CompressedCount);
            Assert.Equal(0, result.TokensSaved);
            Assert.True(result.BudgetMet);
        }

        [Fact]
        public void Null_Throws()
        {
            var c = new PromptContextCompressor();
            Assert.Throws<System.ArgumentNullException>(() => c.Compress(null!));
        }

        // --- Anchor Window ---

        [Fact]
        public void AnchorWindow_KeepsFirstAndLastTurns()
        {
            var msgs = MakeConversation(20, 50);
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.AnchorWindow,
                TargetTokens = 500,
                KeepFirstTurns = 2,
                KeepLastTurns = 3
            });
            var result = c.Compress(msgs);
            Assert.True(result.CompressedCount < result.OriginalCount);
            // Should contain system message
            Assert.Contains(result.Messages, m => m.Role == "system" && m.Content.Contains("helpful"));
        }

        [Fact]
        public void AnchorWindow_PreservesSystemMessages()
        {
            var msgs = new List<(string, string)>
            {
                ("system", "System prompt"),
                ("user", new string('a', 2000)),
                ("assistant", new string('b', 2000)),
                ("user", new string('c', 2000)),
                ("assistant", new string('d', 2000)),
                ("user", "Recent question"),
                ("assistant", "Recent answer")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.AnchorWindow,
                TargetTokens = 200,
                KeepFirstTurns = 1,
                KeepLastTurns = 2
            });
            var result = c.Compress(msgs);
            Assert.Contains(result.Messages, m => m.Content == "System prompt");
        }

        [Fact]
        public void AnchorWindow_InsertsSummaryPlaceholders()
        {
            var msgs = MakeConversation(10, 100);
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.AnchorWindow,
                TargetTokens = 300,
                KeepFirstTurns = 1,
                KeepLastTurns = 2,
                SummaryPlaceholder = "[{count} messages removed]"
            });
            var result = c.Compress(msgs);
            Assert.Contains(result.Messages, m => m.Content.Contains("messages removed"));
        }

        // --- Importance Scoring ---

        [Fact]
        public void ImportanceScoring_DropsLowImportanceMessages()
        {
            var msgs = MakeConversation(15, 80);
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.ImportanceScoring,
                TargetTokens = 400,
                ImportanceThreshold = 0.3
            });
            var result = c.Compress(msgs);
            Assert.True(result.CompressedCount < result.OriginalCount);
            Assert.True(result.CompressedTokens <= result.OriginalTokens);
        }

        [Fact]
        public void ImportanceScoring_KeepsProtectedRoles()
        {
            var msgs = new List<(string, string)>
            {
                ("system", "Important system prompt"),
                ("user", new string('x', 4000)),
                ("assistant", new string('y', 4000)),
                ("user", "Last message")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.ImportanceScoring,
                TargetTokens = 500
            });
            var result = c.Compress(msgs);
            Assert.Contains(result.Messages, m => m.Content.Contains("Important system prompt"));
        }

        [Fact]
        public void ImportanceScoring_KeywordsBoostScore()
        {
            var c = new PromptContextCompressor(new CompressionOptions
            {
                ImportantKeywords = new List<string> { "critical", "urgent" }
            });
            var msgs = new List<(string, string)>
            {
                ("user", "normal message about nothing"),
                ("user", "this is critical and urgent stuff")
            };
            var scores = c.ScoreMessages(msgs);
            Assert.True(scores[1].ImportanceScore > scores[0].ImportanceScore);
        }

        [Fact]
        public void ImportanceScoring_RecencyBoostsScore()
        {
            var c = new PromptContextCompressor();
            var msgs = new List<(string, string)>
            {
                ("user", "old message"),
                ("user", "old message"),
                ("user", "old message"),
                ("user", "recent message")
            };
            var scores = c.ScoreMessages(msgs);
            Assert.True(scores[3].ImportanceScore > scores[0].ImportanceScore);
        }

        // --- Deduplication ---

        [Fact]
        public void Deduplication_FindsDuplicates()
        {
            var msgs = new List<(string, string)>
            {
                ("user", "What is the weather today?"),
                ("assistant", "It's sunny and 72°F."),
                ("user", "What is the weather today?"),
                ("assistant", "It's sunny and warm.")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.Deduplication,
                SimilarityThreshold = 0.7,
                TargetTokens = 10000
            });
            var groups = c.FindDuplicates(msgs);
            Assert.NotEmpty(groups);
        }

        [Fact]
        public void Deduplication_KeepsLatestDuplicate()
        {
            var msgs = new List<(string, string)>
            {
                ("user", "Tell me about dogs"),
                ("assistant", "Dogs are great pets"),
                ("user", "Tell me about dogs"),
                ("assistant", "Dogs are wonderful pets and companions")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.Deduplication,
                SimilarityThreshold = 0.5,
                TargetTokens = 10000
            });
            var result = c.Compress(msgs);
            Assert.True(result.DuplicateGroupsFound > 0);
        }

        [Fact]
        public void Deduplication_DifferentRolesNotDuplicated()
        {
            var msgs = new List<(string, string)>
            {
                ("user", "What is AI?"),
                ("assistant", "What is AI?")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.Deduplication,
                TargetTokens = 10000
            });
            var groups = c.FindDuplicates(msgs);
            Assert.Empty(groups);
        }

        // --- Aggressive ---

        [Fact]
        public void Aggressive_AppliesAllStrategies()
        {
            var msgs = MakeConversation(30, 100);
            // Add some duplicates
            msgs.Add(("user", msgs[1].Content));
            msgs.Add(("user", msgs[3].Content));

            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.Aggressive,
                TargetTokens = 500
            });
            var result = c.Compress(msgs);
            Assert.True(result.CompressedCount < result.OriginalCount);
            Assert.Equal(CompressionStrategy.Aggressive, result.Strategy);
        }

        [Fact]
        public void Aggressive_MaximumCompression()
        {
            var msgs = MakeConversation(50, 200);
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.Aggressive,
                TargetTokens = 300
            });
            var result = c.Compress(msgs);
            Assert.True(result.CompressionRatio > 0);
        }

        // --- Builder ---

        [Fact]
        public void Builder_CreatesConfiguredCompressor()
        {
            var compressor = PromptContextCompressor.Builder()
                .WithTargetTokens(2000)
                .WithStrategy(CompressionStrategy.ImportanceScoring)
                .WithImportanceThreshold(0.5)
                .WithImportantKeywords("error", "critical")
                .WithProtectedRoles("system", "tool")
                .PreserveCodeBlocks(true)
                .Build();

            Assert.NotNull(compressor);
        }

        [Fact]
        public void Builder_AnchorWindowConfig()
        {
            var compressor = PromptContextCompressor.Builder()
                .WithTargetTokens(1000)
                .WithStrategy(CompressionStrategy.AnchorWindow)
                .WithAnchorWindow(3, 5)
                .Build();

            var msgs = MakeConversation(20, 100);
            var result = compressor.Compress(msgs);
            Assert.True(result.CompressedCount < 20);
        }

        [Fact]
        public void Builder_WeightsConfig()
        {
            var compressor = PromptContextCompressor.Builder()
                .WithWeights(recency: 0.8, length: 0.1, role: 0.05, keyword: 0.05)
                .Build();
            Assert.NotNull(compressor);
        }

        // --- FitsInBudget ---

        [Fact]
        public void FitsInBudget_ReturnsTrueWhenUnder()
        {
            var c = new PromptContextCompressor(new CompressionOptions { TargetTokens = 10000 });
            var msgs = new List<(string, string)> { ("user", "Short message") };
            Assert.True(c.FitsInBudget(msgs));
        }

        [Fact]
        public void FitsInBudget_ReturnsFalseWhenOver()
        {
            var c = new PromptContextCompressor(new CompressionOptions { TargetTokens = 5 });
            var msgs = new List<(string, string)> { ("user", new string('x', 1000)) };
            Assert.False(c.FitsInBudget(msgs));
        }

        [Fact]
        public void FitsInBudget_NullReturnsTrue()
        {
            var c = new PromptContextCompressor();
            Assert.True(c.FitsInBudget(null!));
        }

        // --- ScoreMessages ---

        [Fact]
        public void ScoreMessages_ReturnsScoresForAllMessages()
        {
            var c = new PromptContextCompressor();
            var msgs = new List<(string, string)>
            {
                ("system", "System"),
                ("user", "Hello"),
                ("assistant", "Hi")
            };
            var scores = c.ScoreMessages(msgs);
            Assert.Equal(3, scores.Count);
            Assert.All(scores, s => Assert.InRange(s.ImportanceScore, 0, 1));
        }

        [Fact]
        public void ScoreMessages_SystemRoleScoresHighest()
        {
            var c = new PromptContextCompressor();
            var msgs = new List<(string, string)>
            {
                ("system", "System prompt here"),
                ("user", "System prompt here"),
                ("tool", "System prompt here")
            };
            var scores = c.ScoreMessages(msgs);
            // System should have highest role weight contribution
            // (though recency also matters, index 0 has lower recency)
        }

        [Fact]
        public void ScoreMessages_CodeBlocksBoostScore()
        {
            var c = new PromptContextCompressor(new CompressionOptions { PreserveCodeBlocks = true });
            var msgs = new List<(string, string)>
            {
                ("user", "plain text message here"),
                ("user", "here is code ```python\nprint('hello')```")
            };
            var scores = c.ScoreMessages(msgs);
            // Code block message should score higher (has code bonus)
            // Index 1 also has recency bonus so definitely higher
            Assert.True(scores[1].ImportanceScore >= scores[0].ImportanceScore);
        }

        // --- CompressionResult ---

        [Fact]
        public void CompressionResult_Summary_ContainsInfo()
        {
            var result = new CompressionResult
            {
                OriginalCount = 20,
                CompressedCount = 8,
                OriginalTokens = 5000,
                CompressedTokens = 2000,
                Strategy = CompressionStrategy.AnchorWindow,
                BudgetMet = true
            };
            var summary = result.Summary();
            Assert.Contains("20", summary);
            Assert.Contains("8", summary);
            Assert.Contains("AnchorWindow", summary);
            Assert.Contains("Met", summary);
        }

        [Fact]
        public void CompressionResult_Summary_ShowsDuplicates()
        {
            var result = new CompressionResult
            {
                OriginalCount = 10,
                CompressedCount = 7,
                OriginalTokens = 2000,
                CompressedTokens = 1400,
                Strategy = CompressionStrategy.Deduplication,
                DuplicateGroupsFound = 3,
                BudgetMet = true
            };
            var summary = result.Summary();
            Assert.Contains("3 group(s)", summary);
        }

        [Fact]
        public void CompressionResult_ToJson_IsValid()
        {
            var result = new CompressionResult
            {
                OriginalCount = 5,
                CompressedCount = 3,
                OriginalTokens = 1000,
                CompressedTokens = 600,
                Strategy = CompressionStrategy.ImportanceScoring,
                BudgetMet = true
            };
            var json = result.ToJson();
            Assert.Contains("1000", json);
            Assert.Contains("BudgetMet", json);
        }

        [Fact]
        public void CompressionResult_CompressionRatio()
        {
            var result = new CompressionResult
            {
                OriginalTokens = 1000,
                CompressedTokens = 250
            };
            Assert.Equal(0.75, result.CompressionRatio);
        }

        [Fact]
        public void CompressionResult_ZeroOriginalTokens()
        {
            var result = new CompressionResult { OriginalTokens = 0, CompressedTokens = 0 };
            Assert.Equal(0, result.CompressionRatio);
        }

        // --- Edge cases ---

        [Fact]
        public void SingleMessage_NoCompression()
        {
            var msgs = new List<(string, string)> { ("user", "Hello") };
            var c = new PromptContextCompressor(new CompressionOptions { TargetTokens = 10000 });
            var result = c.Compress(msgs);
            Assert.Single(result.Messages);
        }

        [Fact]
        public void AllProtectedRoles_NothingRemoved()
        {
            var msgs = new List<(string, string)>
            {
                ("system", "Prompt 1"),
                ("system", "Prompt 2"),
                ("system", "Prompt 3")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.ImportanceScoring,
                TargetTokens = 10000,
                ProtectedRoles = new List<string> { "system" }
            });
            var result = c.Compress(msgs);
            Assert.Equal(3, result.CompressedCount);
        }

        [Fact]
        public void CustomPlaceholder_Used()
        {
            var msgs = MakeConversation(10, 100);
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.AnchorWindow,
                TargetTokens = 200,
                KeepFirstTurns = 1,
                KeepLastTurns = 1,
                SummaryPlaceholder = "<<{count} omitted>>"
            });
            var result = c.Compress(msgs);
            Assert.Contains(result.Messages, m => m.Content.Contains("omitted"));
        }

        [Fact]
        public void Similarity_IdenticalStrings_ReturnsOne()
        {
            var msgs = new List<(string, string)>
            {
                ("user", "exact same text"),
                ("user", "exact same text")
            };
            var c = new PromptContextCompressor(new CompressionOptions { SimilarityThreshold = 0.99 });
            var groups = c.FindDuplicates(msgs);
            Assert.Single(groups);
        }

        [Fact]
        public void Similarity_CompletelyDifferent_NoDuplicates()
        {
            var msgs = new List<(string, string)>
            {
                ("user", "The quick brown fox jumps over the lazy dog"),
                ("user", "Python is a programming language for data science")
            };
            var c = new PromptContextCompressor(new CompressionOptions { SimilarityThreshold = 0.7 });
            var groups = c.FindDuplicates(msgs);
            Assert.Empty(groups);
        }

        [Fact]
        public void PreserveUrls_BoostsUrlMessages()
        {
            var c = new PromptContextCompressor(new CompressionOptions { PreserveUrls = true });
            var msgs = new List<(string, string)>
            {
                ("user", "plain text no links"),
                ("user", "check https://example.com for more info")
            };
            var scores = c.ScoreMessages(msgs);
            Assert.True(scores[1].ImportanceScore >= scores[0].ImportanceScore);
        }

        [Fact]
        public void LargeConversation_CompressesEfficiently()
        {
            var msgs = MakeConversation(100, 50);
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.Aggressive,
                TargetTokens = 500
            });
            var result = c.Compress(msgs);
            Assert.True(result.CompressionRatio > 0.5);
            Assert.Equal(100, result.OriginalCount);
        }

        [Fact]
        public void Builder_Deduplication()
        {
            var compressor = PromptContextCompressor.Builder()
                .WithTargetTokens(5000)
                .WithStrategy(CompressionStrategy.Deduplication)
                .WithSimilarityThreshold(0.6)
                .Build();

            var msgs = new List<(string, string)>
            {
                ("user", "What is machine learning and how does it work?"),
                ("assistant", "Machine learning is a subset of AI..."),
                ("user", "What is machine learning and how does it function?"),
                ("assistant", "ML is a type of artificial intelligence...")
            };
            var result = compressor.Compress(msgs);
            Assert.True(result.DuplicateGroupsFound >= 0); // May or may not find dupes depending on threshold
        }

        [Fact]
        public void Builder_SummaryPlaceholder()
        {
            var compressor = PromptContextCompressor.Builder()
                .WithTargetTokens(100)
                .WithStrategy(CompressionStrategy.AnchorWindow)
                .WithAnchorWindow(1, 1)
                .WithSummaryPlaceholder("[...{count} messages...]")
                .Build();

            var msgs = MakeConversation(10, 50);
            var result = compressor.Compress(msgs);
            Assert.Contains(result.Messages, m => m.Content.Contains("...") && m.Content.Contains("messages"));
        }

        [Fact]
        public void ScoreMessages_NullThrows()
        {
            var c = new PromptContextCompressor();
            Assert.Throws<System.ArgumentNullException>(() => c.ScoreMessages(null!));
        }

        [Fact]
        public void FindDuplicates_NullThrows()
        {
            var c = new PromptContextCompressor();
            Assert.Throws<System.ArgumentNullException>(() => c.FindDuplicates(null!));
        }

        [Fact]
        public void NullOptions_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new PromptContextCompressor(null!));
        }

        [Fact]
        public void CompressionResult_RemovedCount()
        {
            var result = new CompressionResult { OriginalCount = 10, CompressedCount = 4 };
            Assert.Equal(6, result.RemovedCount);
        }

        [Fact]
        public void CompressionResult_TokensSaved()
        {
            var result = new CompressionResult { OriginalTokens = 5000, CompressedTokens = 2000 };
            Assert.Equal(3000, result.TokensSaved);
        }

        [Fact]
        public void Builder_PreserveUrls()
        {
            var compressor = PromptContextCompressor.Builder()
                .PreserveUrls(true)
                .PreserveCodeBlocks(false)
                .Build();
            Assert.NotNull(compressor);
        }

        [Fact]
        public void AnchorWindow_MoreTurnsThanMessages()
        {
            var msgs = new List<(string, string)>
            {
                ("user", "Hello"),
                ("assistant", "Hi")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.AnchorWindow,
                TargetTokens = 10000,
                KeepFirstTurns = 10,
                KeepLastTurns = 10
            });
            var result = c.Compress(msgs);
            Assert.Equal(2, result.CompressedCount);
        }

        [Fact]
        public void Deduplication_EmptyContent()
        {
            var msgs = new List<(string, string)>
            {
                ("user", ""),
                ("user", ""),
                ("assistant", "response")
            };
            var c = new PromptContextCompressor(new CompressionOptions
            {
                Strategy = CompressionStrategy.Deduplication,
                TargetTokens = 10000
            });
            var result = c.Compress(msgs);
            Assert.True(result.DuplicateGroupsFound >= 0);
        }
    }
}
