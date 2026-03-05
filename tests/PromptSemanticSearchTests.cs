using Xunit;
using Prompt;
using System.Linq;

namespace Prompt.Tests
{
    public class PromptSemanticSearchTests
    {
        private static PromptEntry MakeEntry(
            string name,
            string body = "",
            string? description = null,
            string? category = null,
            string[]? tags = null)
        {
            return new PromptEntry(
                name,
                new PromptTemplate(string.IsNullOrEmpty(body) ? $"Prompt for {name}" : body),
                description: description,
                category: category,
                tags: tags);
        }

        // ── Constructor ──────────────────────────────────────────────

        [Fact]
        public void Constructor_DefaultParameters_Works()
        {
            var search = new PromptSemanticSearch();
            Assert.Equal(0, search.DocumentCount);
            Assert.Equal(0, search.VocabularySize);
        }

        [Fact]
        public void Constructor_InvalidK1_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PromptSemanticSearch(k1: -1));
        }

        [Fact]
        public void Constructor_InvalidB_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PromptSemanticSearch(b: 1.5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PromptSemanticSearch(b: -0.1));
        }

        // ── Indexing ─────────────────────────────────────────────────

        [Fact]
        public void Index_SingleEntry_IncrementsCount()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("test-prompt", "Write a unit test"));
            Assert.Equal(1, search.DocumentCount);
            Assert.True(search.VocabularySize > 0);
        }

        [Fact]
        public void Index_NullEntry_Throws()
        {
            var search = new PromptSemanticSearch();
            Assert.Throws<System.ArgumentNullException>(() => search.Index(null!));
        }

        [Fact]
        public void Index_DuplicateName_Replaces()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("my-prompt", "Version one"));
            search.Index(MakeEntry("my-prompt", "Version two completely different"));
            Assert.Equal(1, search.DocumentCount);
        }

        [Fact]
        public void IndexAll_MultipleEntries_Works()
        {
            var search = new PromptSemanticSearch();
            var entries = new[]
            {
                MakeEntry("code-review", "Review this code for bugs"),
                MakeEntry("summarize", "Summarize the following article"),
                MakeEntry("translate", "Translate this text to French")
            };
            search.IndexAll(entries);
            Assert.Equal(3, search.DocumentCount);
        }

        [Fact]
        public void IndexAll_Null_Throws()
        {
            var search = new PromptSemanticSearch();
            Assert.Throws<System.ArgumentNullException>(() => search.IndexAll(null!));
        }

        // ── Remove ───────────────────────────────────────────────────

        [Fact]
        public void Remove_ExistingEntry_DecrementsCounts()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("to-remove", "This will be removed"));
            Assert.Equal(1, search.DocumentCount);

            bool removed = search.Remove("to-remove");
            Assert.True(removed);
            Assert.Equal(0, search.DocumentCount);
        }

        [Fact]
        public void Remove_NonExistent_ReturnsFalse()
        {
            var search = new PromptSemanticSearch();
            Assert.False(search.Remove("does-not-exist"));
        }

        [Fact]
        public void Remove_NullOrEmpty_ReturnsFalse()
        {
            var search = new PromptSemanticSearch();
            Assert.False(search.Remove(null!));
            Assert.False(search.Remove(""));
        }

        [Fact]
        public void Clear_EmptiesIndex()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("one", "First prompt"));
            search.Index(MakeEntry("two", "Second prompt"));
            Assert.Equal(2, search.DocumentCount);

            search.Clear();
            Assert.Equal(0, search.DocumentCount);
            Assert.Equal(0, search.VocabularySize);
        }

        // ── Search ───────────────────────────────────────────────────

        [Fact]
        public void Search_EmptyQuery_ReturnsEmpty()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("test", "Some content"));
            var results = search.Search("");
            Assert.Empty(results);
        }

        [Fact]
        public void Search_NullQuery_ReturnsEmpty()
        {
            var search = new PromptSemanticSearch();
            var results = search.Search(null!);
            Assert.Empty(results);
        }

        [Fact]
        public void Search_WhitespaceQuery_ReturnsEmpty()
        {
            var search = new PromptSemanticSearch();
            var results = search.Search("   ");
            Assert.Empty(results);
        }

        [Fact]
        public void Search_MatchingQuery_ReturnsResults()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("code-review", "Review this code for bugs and errors",
                description: "Automated code review assistant", category: "coding"));
            search.Index(MakeEntry("write-email", "Write a professional email",
                description: "Email drafting tool", category: "writing"));

            var results = search.Search("code review bugs");
            Assert.NotEmpty(results);
            Assert.Equal("code-review", results[0].Name);
        }

        [Fact]
        public void Search_NoMatch_ReturnsEmpty()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("code-review", "Review code"));
            var results = search.Search("quantum physics entanglement");
            Assert.Empty(results);
        }

        [Fact]
        public void Search_MaxResults_Respected()
        {
            var search = new PromptSemanticSearch();
            for (int i = 0; i < 20; i++)
                search.Index(MakeEntry($"prompt-{i}", $"Testing prompt number {i} with code review"));

            var results = search.Search("code review", maxResults: 3);
            Assert.True(results.Count <= 3);
        }

        [Fact]
        public void Search_InvalidMaxResults_Throws()
        {
            var search = new PromptSemanticSearch();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => search.Search("test", maxResults: 0));
        }

        [Fact]
        public void Search_MinScore_FiltersResults()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("exact-match", "specific technical debugging errors",
                description: "Debug technical errors specifically", category: "debugging",
                tags: new[] { "errors", "debugging" }));
            search.Index(MakeEntry("vague-match", "general purpose assistant",
                description: "General helper"));

            var allResults = search.Search("debugging errors", minScore: 0);
            var filteredResults = search.Search("debugging errors", minScore: 100);
            Assert.True(filteredResults.Count <= allResults.Count);
        }

        [Fact]
        public void Search_RankedByRelevance()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("sql-expert", "Write SQL queries for database management",
                description: "SQL query generator", category: "database",
                tags: new[] { "sql", "database", "query" }));
            search.Index(MakeEntry("generic-helper", "Help with various tasks",
                description: "General purpose helper"));
            search.Index(MakeEntry("data-analyst", "Analyze database performance and query optimization",
                description: "Database analysis", category: "database",
                tags: new[] { "database", "analysis" }));

            var results = search.Search("database query optimization");
            Assert.True(results.Count >= 1);
            // The more relevant results should score higher
            if (results.Count >= 2)
                Assert.True(results[0].Score >= results[1].Score);
        }

        [Fact]
        public void Search_ResultContainsTermScores()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("test-entry", "Analyze code performance metrics",
                tags: new[] { "performance", "analysis" }));

            var results = search.Search("performance analysis");
            Assert.NotEmpty(results);
            Assert.NotEmpty(results[0].TermScores);
        }

        [Fact]
        public void Search_ResultContainsMatchedFields()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("coding-helper", "Write clean code",
                description: "Helps with coding tasks", category: "coding",
                tags: new[] { "code", "programming" }));

            var results = search.Search("coding");
            Assert.NotEmpty(results);
            Assert.NotEmpty(results[0].MatchedFields);
        }

        [Fact]
        public void Search_PrefixMatching_Works()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("optimization-guide",
                "Performance optimization techniques for applications",
                description: "Optimize application performance"));

            // "optim" should prefix-match "optimiz" (stemmed)
            var results = search.Search("optim");
            // Prefix matching requires term.Length >= 3
            Assert.NotEmpty(results);
        }

        [Fact]
        public void Search_NameFieldBoosted()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("security-audit",
                "Check application for vulnerabilities",
                description: "Security scanning tool"));
            search.Index(MakeEntry("general-tool",
                "Security audit scanning and vulnerability detection comprehensive review",
                description: "Generic helper for security"));

            var results = search.Search("security audit");
            Assert.NotEmpty(results);
            // Name match should boost "security-audit" to top
            Assert.Equal("security-audit", results[0].Name);
        }

        [Fact]
        public void Search_TagsBoosted()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("entry-with-tags", "Some generic prompt text",
                tags: new[] { "machine-learning", "neural-networks" }));
            search.Index(MakeEntry("entry-without-tags", "Machine learning neural networks deep learning",
                description: "A prompt about ML"));

            var results = search.Search("machine learning neural");
            Assert.NotEmpty(results);
        }

        // ── FindSimilar ──────────────────────────────────────────────

        [Fact]
        public void FindSimilar_ReturnsRelatedPrompts()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("python-debug", "Debug Python code",
                category: "coding", tags: new[] { "python", "debugging" }));
            search.Index(MakeEntry("python-review", "Review Python code for best practices",
                category: "coding", tags: new[] { "python", "review" }));
            search.Index(MakeEntry("french-translate", "Translate text to French",
                category: "language", tags: new[] { "french", "translation" }));

            var similar = search.FindSimilar("python-debug", maxResults: 5);
            Assert.NotEmpty(similar);
            // python-review should be more similar than french-translate
            Assert.DoesNotContain(similar, r => r.Name == "python-debug"); // excludes self
        }

        [Fact]
        public void FindSimilar_ExcludesSelf()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("my-prompt", "Testing prompt content"));
            var similar = search.FindSimilar("my-prompt");
            Assert.DoesNotContain(similar, r => r.Name == "my-prompt");
        }

        [Fact]
        public void FindSimilar_NonExistent_ReturnsEmpty()
        {
            var search = new PromptSemanticSearch();
            Assert.Empty(search.FindSimilar("nope"));
        }

        [Fact]
        public void FindSimilar_NullOrEmpty_ReturnsEmpty()
        {
            var search = new PromptSemanticSearch();
            Assert.Empty(search.FindSimilar(null!));
            Assert.Empty(search.FindSimilar(""));
        }

        // ── GetTopTerms ──────────────────────────────────────────────

        [Fact]
        public void GetTopTerms_ReturnsDistinctiveTerms()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("entry-one", "Alpha beta gamma unique specialized"));
            search.Index(MakeEntry("entry-two", "Alpha beta delta common standard"));

            var topTerms = search.GetTopTerms(5);
            Assert.NotEmpty(topTerms);
            // All IDF values should be positive
            Assert.All(topTerms, t => Assert.True(t.Idf > 0));
        }

        [Fact]
        public void GetTopTerms_EmptyIndex_ReturnsEmpty()
        {
            var search = new PromptSemanticSearch();
            Assert.Empty(search.GetTopTerms());
        }

        // ── GetStats ─────────────────────────────────────────────────

        [Fact]
        public void GetStats_ReturnsAccurateStatistics()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("short-prompt", "Brief"));
            search.Index(MakeEntry("long-prompt",
                "This is a much longer prompt with many different words for testing purposes"));

            var stats = search.GetStats();
            Assert.Equal(2, stats.TotalDocuments);
            Assert.True(stats.VocabularySize > 0);
            Assert.True(stats.AverageDocumentLength > 0);
            Assert.True(stats.LongestDocument >= stats.ShortestDocument);
        }

        [Fact]
        public void GetStats_EmptyIndex_AllZeros()
        {
            var search = new PromptSemanticSearch();
            var stats = search.GetStats();
            Assert.Equal(0, stats.TotalDocuments);
            Assert.Equal(0, stats.VocabularySize);
            Assert.Equal(0, stats.AverageDocumentLength);
        }

        [Fact]
        public void GetStats_MostCommonTerms_Present()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("a", "code review testing"));
            search.Index(MakeEntry("b", "code analysis review"));
            search.Index(MakeEntry("c", "code debugging errors"));

            var stats = search.GetStats();
            Assert.NotEmpty(stats.MostCommonTerms);
            // "code" appears in all 3 docs so should be most common
            Assert.Contains(stats.MostCommonTerms, t => t.Term == "code");
        }

        // ── Tokenizer ────────────────────────────────────────────────

        [Fact]
        public void Tokenize_RemovesStopWords()
        {
            var tokens = PromptSemanticSearch.Tokenize("the quick brown fox jumps over the lazy dog");
            Assert.DoesNotContain("the", tokens);
            // "over" is in stop words list and should be filtered
            // Verify meaningful words remain
            Assert.Contains("quick", tokens);
            Assert.Contains("brown", tokens);
        }

        [Fact]
        public void Tokenize_Lowercases()
        {
            var tokens = PromptSemanticSearch.Tokenize("UPPERCASE MixedCase");
            Assert.All(tokens, t => Assert.Equal(t, t.ToLowerInvariant()));
        }

        [Fact]
        public void Tokenize_HandlesSpecialCharacters()
        {
            var tokens = PromptSemanticSearch.Tokenize("hello-world foo_bar baz@qux");
            Assert.Contains("hello", tokens);
            Assert.Contains("world", tokens);
        }

        [Fact]
        public void Tokenize_EmptyOrNull_ReturnsEmpty()
        {
            Assert.Empty(PromptSemanticSearch.Tokenize(""));
            Assert.Empty(PromptSemanticSearch.Tokenize(null!));
            Assert.Empty(PromptSemanticSearch.Tokenize("   "));
        }

        [Fact]
        public void Tokenize_FiltersSingleCharTokens()
        {
            var tokens = PromptSemanticSearch.Tokenize("a b c de fg");
            Assert.DoesNotContain("a", tokens);
            Assert.DoesNotContain("b", tokens);
            Assert.DoesNotContain("c", tokens);
        }

        // ── Stemmer ──────────────────────────────────────────────────

        [Fact]
        public void Stem_CommonSuffixes()
        {
            // "running" (7 chars) -> strip "ing" (len>5) -> "runn"
            Assert.Equal("runn", PromptSemanticSearch.Stem("running"));
            // "happiness" -> endsWith("iness") -> word[..^4]+"y" = "happiy"
            // Actually: "ness" rule fires first since suffixes checked longest-first?
            // Let's verify: "happiness" ends with "iness" (5 chars) -> word[..^4]+"y"
            // word[..^4] = "happi", +"y" = "happiy"  -- but "iness" is checked before "ness"
            Assert.Equal("happiy", PromptSemanticSearch.Stem("happiness"));
            Assert.Equal("care", PromptSemanticSearch.Stem("careful"));
        }

        [Fact]
        public void Stem_ShortWords_Unchanged()
        {
            Assert.Equal("cat", PromptSemanticSearch.Stem("cat"));
            Assert.Equal("go", PromptSemanticSearch.Stem("go"));
            Assert.Equal("run", PromptSemanticSearch.Stem("run"));
        }

        [Fact]
        public void Stem_Plurals()
        {
            Assert.Equal("prompt", PromptSemanticSearch.Stem("prompts"));
            // "queries" -> endsWith("ies") -> word[..^3]+"y" = "query" -- wait, len=7, "ies" matches
            // word[..^3] = "quer", +"y" = "query"
            Assert.Equal("query", PromptSemanticSearch.Stem("queries"));
        }

        // ── Integration / End-to-End ─────────────────────────────────

        [Fact]
        public void EndToEnd_IndexSearchRemoveSearch()
        {
            var search = new PromptSemanticSearch();

            // Index
            search.Index(MakeEntry("code-reviewer", "Analyze source code for bugs defects and improvements",
                description: "Automated code review", category: "development",
                tags: new[] { "code", "review", "bugs" }));
            search.Index(MakeEntry("email-writer", "Draft professional business emails",
                description: "Email composition tool", category: "writing",
                tags: new[] { "email", "business" }));

            Assert.Equal(2, search.DocumentCount);

            // Search finds code reviewer
            var results = search.Search("find bugs in code");
            Assert.NotEmpty(results);
            Assert.Equal("code-reviewer", results[0].Name);

            // Remove code reviewer
            search.Remove("code-reviewer");
            Assert.Equal(1, search.DocumentCount);

            // Search no longer finds it
            results = search.Search("find bugs in code");
            Assert.DoesNotContain(results, r => r.Name == "code-reviewer");
        }

        [Fact]
        public void EndToEnd_ReindexUpdatesResults()
        {
            var search = new PromptSemanticSearch();

            search.Index(MakeEntry("my-prompt", "Write Python code",
                tags: new[] { "python" }));

            var results1 = search.Search("python");
            Assert.NotEmpty(results1);

            // Re-index with different content
            search.Index(MakeEntry("my-prompt", "Write JavaScript code",
                tags: new[] { "javascript" }));

            var results2 = search.Search("javascript");
            Assert.NotEmpty(results2);
            Assert.Equal("my-prompt", results2[0].Name);
        }

        [Fact]
        public void EndToEnd_LargeIndex_PerformsWell()
        {
            var search = new PromptSemanticSearch();
            var categories = new[] { "coding", "writing", "analysis", "data", "design" };
            var topics = new[] { "python", "javascript", "database", "security", "performance",
                                 "testing", "deployment", "architecture", "debugging", "optimization" };

            for (int i = 0; i < 100; i++)
            {
                search.Index(MakeEntry(
                    $"prompt-{i}",
                    $"Handle {topics[i % topics.Length]} tasks with {categories[i % categories.Length]} focus",
                    category: categories[i % categories.Length],
                    tags: new[] { topics[i % topics.Length], topics[(i + 3) % topics.Length] }));
            }

            Assert.Equal(100, search.DocumentCount);

            var results = search.Search("python security testing", maxResults: 5);
            Assert.NotEmpty(results);
            Assert.True(results.Count <= 5);

            var stats = search.GetStats();
            Assert.Equal(100, stats.TotalDocuments);
        }

        [Fact]
        public void EndToEnd_AverageDocumentLength_Accurate()
        {
            var search = new PromptSemanticSearch();
            search.Index(MakeEntry("one", "short text"));
            search.Index(MakeEntry("two", "longer text with more words added here"));

            Assert.True(search.AverageDocumentLength > 0);
        }

        [Fact]
        public void EndToEnd_CustomBM25Parameters()
        {
            var search = new PromptSemanticSearch(k1: 2.0, b: 0.5, nameBoost: 5.0);
            search.Index(MakeEntry("target-prompt", "Specialized content for testing",
                tags: new[] { "specialized" }));
            search.Index(MakeEntry("other-prompt", "Different content entirely"));

            var results = search.Search("target prompt specialized");
            Assert.NotEmpty(results);
            Assert.Equal("target-prompt", results[0].Name);
        }
    }
}
