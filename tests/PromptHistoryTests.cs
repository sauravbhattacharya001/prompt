namespace Prompt.Tests
{
    using Xunit;

    public class PromptHistoryTests
    {
        [Fact]
        public void Record_AddsEntry()
        {
            var history = new PromptHistory();
            history.Record("Hello", "Hi there", TimeSpan.FromSeconds(1));

            Assert.Equal(1, history.Count);
            Assert.Equal(1, history.TotalCount);
        }

        [Fact]
        public void Record_ThrowsOnEmptyPrompt()
        {
            var history = new PromptHistory();
            Assert.Throws<ArgumentException>(() =>
                history.Record("", "response", TimeSpan.Zero));
        }

        [Fact]
        public void MaxEntries_EvictsOldest()
        {
            var history = new PromptHistory(maxEntries: 3);
            for (int i = 0; i < 5; i++)
                history.Record($"Prompt {i}", $"Response {i}", TimeSpan.FromSeconds(i));

            Assert.Equal(3, history.Count);
            Assert.Equal(5, history.TotalCount);

            var recent = history.GetRecent(10);
            Assert.Equal(3, recent.Count);
            Assert.Equal("Prompt 4", recent[0].Prompt);
        }

        [Fact]
        public void Constructor_ThrowsOnInvalidMaxEntries()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptHistory(0));
        }

        [Fact]
        public void GetRecent_ReturnsNewestFirst()
        {
            var history = new PromptHistory();
            history.Record("First", "R1", TimeSpan.FromSeconds(1));
            history.Record("Second", "R2", TimeSpan.FromSeconds(2));
            history.Record("Third", "R3", TimeSpan.FromSeconds(3));

            var recent = history.GetRecent(2);
            Assert.Equal(2, recent.Count);
            Assert.Equal("Third", recent[0].Prompt);
            Assert.Equal("Second", recent[1].Prompt);
        }

        [Fact]
        public void Search_ByQuery()
        {
            var history = new PromptHistory();
            history.Record("Tell me about cats", "Cats are great", TimeSpan.FromSeconds(1));
            history.Record("Tell me about dogs", "Dogs are loyal", TimeSpan.FromSeconds(1));
            history.Record("What is Python", "A language", TimeSpan.FromSeconds(1));

            var results = history.Search(query: "cats");
            Assert.Single(results);
            Assert.Equal("Tell me about cats", results[0].Prompt);
        }

        [Fact]
        public void Search_ByTag()
        {
            var history = new PromptHistory();
            history.Record("P1", "R1", TimeSpan.FromSeconds(1), tags: new[] { "coding" });
            history.Record("P2", "R2", TimeSpan.FromSeconds(1), tags: new[] { "writing" });
            history.Record("P3", "R3", TimeSpan.FromSeconds(1));

            var results = history.Search(tag: "coding");
            Assert.Single(results);
            Assert.Equal("P1", results[0].Prompt);
        }

        [Fact]
        public void Search_ByDuration()
        {
            var history = new PromptHistory();
            history.Record("Fast", "R1", TimeSpan.FromMilliseconds(100));
            history.Record("Slow", "R2", TimeSpan.FromSeconds(10));

            var slow = history.Search(minDuration: TimeSpan.FromSeconds(5));
            Assert.Single(slow);
            Assert.Equal("Slow", slow[0].Prompt);
        }

        [Fact]
        public void Search_SuccessAndFailure()
        {
            var history = new PromptHistory();
            history.Record("Good", "Response", TimeSpan.FromSeconds(1));
            history.Record("Bad", null, TimeSpan.FromSeconds(1), error: "Timeout");

            var ok = history.Search(successOnly: true);
            Assert.Single(ok);
            Assert.Equal("Good", ok[0].Prompt);

            var failed = history.Search(failedOnly: true);
            Assert.Single(failed);
            Assert.Equal("Bad", failed[0].Prompt);
        }

        [Fact]
        public void GetStatistics_ReturnsCorrectValues()
        {
            var history = new PromptHistory();
            history.Record("P1", "R1", TimeSpan.FromSeconds(1), tags: new[] { "a" });
            history.Record("P2", "R2", TimeSpan.FromSeconds(3), tags: new[] { "a", "b" });
            history.Record("P3", null, TimeSpan.FromSeconds(2), error: "Fail");

            var stats = history.GetStatistics();
            Assert.Equal(3, stats.TotalEntries);
            Assert.Equal(2, stats.SuccessCount);
            Assert.Equal(1, stats.FailureCount);
            Assert.True(stats.SuccessRate > 60 && stats.SuccessRate < 70);
            Assert.True(stats.AverageDurationMs > 0);
            Assert.Equal(2, stats.TopTags["a"]);
            Assert.Equal(1, stats.TopTags["b"]);
        }

        [Fact]
        public void GetStatistics_EmptyHistory()
        {
            var history = new PromptHistory();
            var stats = history.GetStatistics();
            Assert.Equal(0, stats.TotalEntries);
            Assert.Equal(0, stats.SuccessCount);
        }

        [Fact]
        public void Record_WithOptions()
        {
            var history = new PromptHistory();
            var opts = new PromptOptions { Temperature = 0.5f, MaxTokens = 2000 };
            history.Record("P1", "R1", TimeSpan.FromSeconds(1), options: opts);

            var entry = history.GetRecent(1)[0];
            Assert.Equal(0.5f, entry.Temperature);
            Assert.Equal(2000, entry.MaxTokens);
        }

        [Fact]
        public void ExportToJson_ReturnsValidJson()
        {
            var history = new PromptHistory();
            history.Record("Test prompt", "Test response", TimeSpan.FromSeconds(1));

            var json = history.ExportToJson();
            Assert.Contains("Test prompt", json);
            Assert.Contains("Test response", json);

            // Verify it's valid JSON
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
        }

        [Fact]
        public void ExportToCsv_HasHeaders()
        {
            var history = new PromptHistory();
            history.Record("Test", "Response", TimeSpan.FromSeconds(1));

            var csv = history.ExportToCsv();
            Assert.StartsWith("Id,Timestamp,Success,DurationMs", csv);
            Assert.Contains("Test", csv);
        }

        [Fact]
        public void ExportToCsv_EscapesCommas()
        {
            var history = new PromptHistory();
            history.Record("Hello, world", "Res", TimeSpan.FromSeconds(1));

            var csv = history.ExportToCsv();
            Assert.Contains("\"Hello, world\"", csv);
        }

        [Fact]
        public void Clear_RemovesAll()
        {
            var history = new PromptHistory();
            history.Record("P1", "R1", TimeSpan.FromSeconds(1));
            history.Record("P2", "R2", TimeSpan.FromSeconds(1));

            history.Clear();
            Assert.Equal(0, history.Count);
            Assert.Equal(2, history.TotalCount);
        }

        [Fact]
        public void EstimateTokens_ApproximatelyCorrect()
        {
            // ~4 chars per token
            Assert.Equal(0, PromptGuard.EstimateTokens(""));
            Assert.Equal(0, PromptGuard.EstimateTokens(null!));
            Assert.Equal(3, PromptGuard.EstimateTokens("Hello World!")); // 12 chars -> 3 tokens
        }

        [Fact]
        public void Search_DateRange()
        {
            var history = new PromptHistory();
            history.Record("Old", "R1", TimeSpan.FromSeconds(1));

            var now = DateTimeOffset.UtcNow;
            var results = history.Search(after: now.AddMinutes(-1), before: now.AddMinutes(1));
            Assert.Single(results);

            var noResults = history.Search(after: now.AddHours(1));
            Assert.Empty(noResults);
        }

        [Fact]
        public async Task SaveAndLoad_RoundTrips()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var history = new PromptHistory();
                history.Record("Save me", "Saved!", TimeSpan.FromSeconds(1.5),
                    tags: new[] { "test" }, model: "gpt-4");

                await history.SaveAsync(tempFile);

                var loaded = new PromptHistory();
                await loaded.LoadAsync(tempFile);

                Assert.Equal(1, loaded.Count);
                var entry = loaded.GetRecent(1)[0];
                Assert.Equal("Save me", entry.Prompt);
                Assert.Equal("Saved!", entry.Response);
                Assert.Equal("gpt-4", entry.Model);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TrackAsync_ThrowsOnNull()
        {
            var history = new PromptHistory();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                history.TrackAsync(null!));
        }

        [Fact]
        public async Task TrackAsync_RecordsSuccessfulCall()
        {
            var history = new PromptHistory();
            var result = await history.TrackAsync(
                () => Task.FromResult<string?>("response"),
                prompt: "test prompt");

            Assert.Equal("response", result);
            Assert.Equal(1, history.Count);
            var entry = history.GetRecent(1)[0];
            Assert.True(entry.Success);
            Assert.Equal("test prompt", entry.Prompt);
        }

        [Fact]
        public async Task TrackAsync_RecordsFailedCall()
        {
            var history = new PromptHistory();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                history.TrackAsync(
                    () => throw new InvalidOperationException("boom"),
                    prompt: "failing prompt"));

            Assert.Equal(1, history.Count);
            var entry = history.GetRecent(1)[0];
            Assert.False(entry.Success);
            Assert.Equal("boom", entry.Error);
        }

        [Fact]
        public async Task LoadAsync_ThrowsOnMissingFile()
        {
            var history = new PromptHistory();
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                history.LoadAsync("nonexistent.json"));
        }

        [Fact]
        public async Task SaveAsync_ThrowsOnEmptyPath()
        {
            var history = new PromptHistory();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                history.SaveAsync(""));
        }

        [Fact]
        public void EntryHasUniqueId()
        {
            var history = new PromptHistory();
            history.Record("P1", "R1", TimeSpan.FromSeconds(1));
            history.Record("P2", "R2", TimeSpan.FromSeconds(1));

            var entries = history.GetRecent(2);
            Assert.NotEqual(entries[0].Id, entries[1].Id);
            Assert.Equal(12, entries[0].Id.Length);
        }

        [Fact]
        public void EntryEstimatesTokens()
        {
            var history = new PromptHistory();
            history.Record("Short", "A longer response text here", TimeSpan.FromSeconds(1));

            var entry = history.GetRecent(1)[0];
            Assert.True(entry.EstimatedPromptTokens > 0);
            Assert.True(entry.EstimatedResponseTokens > entry.EstimatedPromptTokens);
        }
    }
}
