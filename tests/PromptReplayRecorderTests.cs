namespace Prompt.Tests
{
    using Prompt;
    using Xunit;
    using System.Text.Json;

    public class PromptReplayRecorderTests
    {
        private RecordedInteraction MakeInteraction(
            string prompt = "Hello",
            string response = "Hi there",
            string model = "gpt-4",
            long latencyMs = 200,
            int inputTokens = 10,
            int outputTokens = 5,
            double cost = 0.001)
        {
            return new RecordedInteraction
            {
                Prompt = prompt,
                Response = response,
                Model = model,
                LatencyMs = latencyMs,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                EstimatedCostUsd = cost
            };
        }

        // ── Construction ────────────────────────────────────────

        [Fact]
        public void Constructor_DefaultStrategy_Exact()
        {
            var recorder = new PromptReplayRecorder();
            Assert.False(recorder.IsRecording);
            Assert.Null(recorder.ActiveCassetteName);
            Assert.Equal(0, recorder.CassetteCount);
        }

        [Fact]
        public void Constructor_CustomStrategy()
        {
            var recorder = new PromptReplayRecorder(ReplayMatchStrategy.Sequential);
            Assert.False(recorder.IsRecording);
        }

        // ── Recording ───────────────────────────────────────────

        [Fact]
        public void StartRecording_SetsActiveState()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("test-cassette", "A test");
            Assert.True(recorder.IsRecording);
            Assert.Equal("test-cassette", recorder.ActiveCassetteName);
            Assert.Equal(1, recorder.CassetteCount);
        }

        [Fact]
        public void StartRecording_EmptyName_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<ArgumentException>(() => recorder.StartRecording(""));
            Assert.Throws<ArgumentException>(() => recorder.StartRecording("  "));
        }

        [Fact]
        public void StartRecording_WhileRecording_Throws()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("first");
            Assert.Throws<InvalidOperationException>(() => recorder.StartRecording("second"));
        }

        [Fact]
        public void StartRecording_Overwrite_ClearsExisting()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction());
            recorder.StopRecording();

            recorder.StartRecording("c1"); // overwrite (append=false default)
            recorder.StopRecording();

            var c = recorder.GetCassette("c1");
            Assert.NotNull(c);
            Assert.Empty(c!.Interactions);
        }

        [Fact]
        public void StartRecording_Append_KeepsExisting()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "first"));
            recorder.StopRecording();

            recorder.StartRecording("c1", append: true);
            recorder.Record(MakeInteraction(prompt: "second"));
            recorder.StopRecording();

            var c = recorder.GetCassette("c1");
            Assert.Equal(2, c!.Interactions.Count);
        }

        [Fact]
        public void Record_AddsInteraction()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction());
            var cassette = recorder.StopRecording();

            Assert.Single(cassette.Interactions);
            Assert.NotEmpty(cassette.Interactions[0].Fingerprint);
        }

        [Fact]
        public void Record_NullInteraction_Throws()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            Assert.Throws<ArgumentNullException>(() => recorder.Record(null!));
        }

        [Fact]
        public void Record_WhenNotRecording_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<InvalidOperationException>(() => recorder.Record(MakeInteraction()));
        }

        [Fact]
        public void StopRecording_ReturnsCassette()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction());
            var cassette = recorder.StopRecording();

            Assert.Equal("c1", cassette.Name);
            Assert.False(recorder.IsRecording);
        }

        [Fact]
        public void StopRecording_WhenNotRecording_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<InvalidOperationException>(() => recorder.StopRecording());
        }

        // ── Replay: Exact ───────────────────────────────────────

        [Fact]
        public void Replay_ExactMatch_Hit()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "What is 2+2?", response: "4", model: "gpt-4"));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            var result = recorder.Replay("What is 2+2?", model: "gpt-4");

            Assert.True(result.IsHit);
            Assert.Equal("4", result.Recording!.Response);
            Assert.Equal("Exact", result.MatchStrategy);
        }

        [Fact]
        public void Replay_ExactMatch_DifferentModel_Miss()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "Hello", model: "gpt-4"));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            var result = recorder.Replay("Hello", model: "claude-3");

            Assert.False(result.IsHit);
        }

        [Fact]
        public void Replay_ExactMatch_DifferentPrompt_Miss()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "Hello", model: "gpt-4"));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            var result = recorder.Replay("Goodbye", model: "gpt-4");

            Assert.False(result.IsHit);
        }

        // ── Replay: PromptOnly ──────────────────────────────────

        [Fact]
        public void Replay_PromptOnly_IgnoresModel()
        {
            var recorder = new PromptReplayRecorder(ReplayMatchStrategy.PromptOnly);
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "What is AI?", response: "Artificial Intelligence", model: "gpt-4"));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            var result = recorder.Replay("What is AI?", model: "claude-3");

            Assert.True(result.IsHit);
            Assert.Equal("Artificial Intelligence", result.Recording!.Response);
        }

        [Fact]
        public void Replay_PromptOnly_MatchesWithSystemPrompt()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            var interaction = MakeInteraction(prompt: "Hi", response: "Hello!");
            interaction.SystemPrompt = "Be friendly";
            recorder.Record(interaction);
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            var result = recorder.Replay("Hi", systemPrompt: "Be friendly",
                strategy: ReplayMatchStrategy.PromptOnly);

            Assert.True(result.IsHit);
        }

        // ── Replay: Sequential ──────────────────────────────────

        [Fact]
        public void Replay_Sequential_ReturnsInOrder()
        {
            var recorder = new PromptReplayRecorder(ReplayMatchStrategy.Sequential);
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "Q1", response: "A1"));
            recorder.Record(MakeInteraction(prompt: "Q2", response: "A2"));
            recorder.Record(MakeInteraction(prompt: "Q3", response: "A3"));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            Assert.Equal("A1", recorder.Replay("anything").Recording!.Response);
            Assert.Equal("A2", recorder.Replay("whatever").Recording!.Response);
            Assert.Equal("A3", recorder.Replay("doesn't matter").Recording!.Response);
        }

        [Fact]
        public void Replay_Sequential_MissAfterExhausted()
        {
            var recorder = new PromptReplayRecorder(ReplayMatchStrategy.Sequential);
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction());
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            recorder.Replay("a");
            var result = recorder.Replay("b");

            Assert.False(result.IsHit);
        }

        [Fact]
        public void ResetSequentialIndex_RestartsFromBeginning()
        {
            var recorder = new PromptReplayRecorder(ReplayMatchStrategy.Sequential);
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(response: "first"));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            recorder.Replay("a");
            recorder.ResetSequentialIndex();
            var result = recorder.Replay("a");

            Assert.True(result.IsHit);
            Assert.Equal("first", result.Recording!.Response);
        }

        // ── Replay: no cassette loaded ──────────────────────────

        [Fact]
        public void Replay_NoCassetteLoaded_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<InvalidOperationException>(() => recorder.Replay("hello"));
        }

        [Fact]
        public void LoadCassette_Unknown_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<KeyNotFoundException>(() => recorder.LoadCassette("nonexistent"));
        }

        // ── Strategy override ───────────────────────────────────

        [Fact]
        public void Replay_StrategyOverride()
        {
            var recorder = new PromptReplayRecorder(ReplayMatchStrategy.Exact);
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "Q", response: "A", model: "gpt-4"));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            // Override to PromptOnly — should match even with different model
            var result = recorder.Replay("Q", model: "claude-3",
                strategy: ReplayMatchStrategy.PromptOnly);
            Assert.True(result.IsHit);
        }

        // ── Cassette management ─────────────────────────────────

        [Fact]
        public void ListCassettes_SortedAlphabetically()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("charlie"); recorder.StopRecording();
            recorder.StartRecording("alpha"); recorder.StopRecording();
            recorder.StartRecording("bravo"); recorder.StopRecording();

            var names = recorder.ListCassettes();
            Assert.Equal(new[] { "alpha", "bravo", "charlie" }, names);
        }

        [Fact]
        public void GetCassette_Unknown_ReturnsNull()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Null(recorder.GetCassette("nope"));
        }

        [Fact]
        public void RemoveCassette_Succeeds()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1"); recorder.StopRecording();
            Assert.True(recorder.RemoveCassette("c1"));
            Assert.Equal(0, recorder.CassetteCount);
        }

        [Fact]
        public void RemoveCassette_Unknown_ReturnsFalse()
        {
            var recorder = new PromptReplayRecorder();
            Assert.False(recorder.RemoveCassette("nope"));
        }

        [Fact]
        public void RemoveCassette_ActiveCassette_StopsRecording()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.RemoveCassette("c1");
            Assert.False(recorder.IsRecording);
        }

        [Fact]
        public void Clear_RemovesEverything()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1"); recorder.StopRecording();
            recorder.StartRecording("c2"); recorder.StopRecording();
            recorder.Clear();

            Assert.Equal(0, recorder.CassetteCount);
            Assert.False(recorder.IsRecording);
        }

        // ── Tags ────────────────────────────────────────────────

        [Fact]
        public void TagCassette_AddsTags()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1"); recorder.StopRecording();
            recorder.TagCassette("c1", "regression");
            recorder.TagCassette("c1", "v2");

            var c = recorder.GetCassette("c1");
            Assert.Contains("regression", c!.Tags);
            Assert.Contains("v2", c.Tags);
        }

        [Fact]
        public void TagCassette_DuplicateIgnored()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1"); recorder.StopRecording();
            recorder.TagCassette("c1", "tag1");
            recorder.TagCassette("c1", "tag1");

            Assert.Single(recorder.GetCassette("c1")!.Tags);
        }

        [Fact]
        public void FindByTag_ReturnsMatching()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1"); recorder.StopRecording();
            recorder.StartRecording("c2"); recorder.StopRecording();
            recorder.StartRecording("c3"); recorder.StopRecording();
            recorder.TagCassette("c1", "regression");
            recorder.TagCassette("c3", "regression");

            var found = recorder.FindByTag("regression");
            Assert.Equal(new[] { "c1", "c3" }, found);
        }

        [Fact]
        public void FindByTag_NoMatch_ReturnsEmpty()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Empty(recorder.FindByTag("nonexistent"));
        }

        // ── Export / Import ─────────────────────────────────────

        [Fact]
        public void ExportImport_Roundtrip()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1", "test cassette");
            recorder.Record(MakeInteraction(prompt: "Q", response: "A"));
            recorder.StopRecording();

            var json = recorder.ExportCassette("c1");
            Assert.Contains("c1", json);
            Assert.Contains("test cassette", json);

            var recorder2 = new PromptReplayRecorder();
            var name = recorder2.ImportCassette(json);
            Assert.Equal("c1", name);

            recorder2.LoadCassette("c1");
            var result = recorder2.Replay("Q", model: "gpt-4");
            Assert.True(result.IsHit);
            Assert.Equal("A", result.Recording!.Response);
        }

        [Fact]
        public void ExportCassette_Unknown_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<KeyNotFoundException>(() => recorder.ExportCassette("nope"));
        }

        [Fact]
        public void ImportCassette_EmptyJson_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<ArgumentException>(() => recorder.ImportCassette(""));
        }

        [Fact]
        public void ImportCassette_InvalidJson_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<ArgumentException>(() => recorder.ImportCassette("not json"));
        }

        [Fact]
        public void ImportCassette_DuplicateWithoutOverwrite_Throws()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1"); recorder.StopRecording();
            var json = recorder.ExportCassette("c1");
            Assert.Throws<InvalidOperationException>(() => recorder.ImportCassette(json));
        }

        [Fact]
        public void ImportCassette_DuplicateWithOverwrite_Succeeds()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction());
            recorder.StopRecording();

            var json = recorder.ExportCassette("c1");
            recorder.ImportCassette(json, overwrite: true);

            Assert.Equal(1, recorder.CassetteCount);
        }

        [Fact]
        public void ExportAll_IncludesAllCassettes()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1"); recorder.StopRecording();
            recorder.StartRecording("c2"); recorder.StopRecording();

            var json = recorder.ExportAll();
            Assert.Contains("c1", json);
            Assert.Contains("c2", json);
        }

        // ── Stats ───────────────────────────────────────────────

        [Fact]
        public void Stats_TrackHitsAndMisses()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "Hi", model: "gpt-4",
                latencyMs: 300, inputTokens: 10, outputTokens: 5, cost: 0.01));
            recorder.StopRecording();

            recorder.LoadCassette("c1");
            recorder.Replay("Hi", model: "gpt-4"); // hit
            recorder.Replay("Bye", model: "gpt-4"); // miss

            var stats = recorder.GetStats();
            Assert.Equal(2, stats.TotalAttempts);
            Assert.Equal(1, stats.Hits);
            Assert.Equal(1, stats.Misses);
            Assert.Equal(50.0, stats.HitRate);
            Assert.Equal(15, stats.TotalTokensReplayed);
            Assert.Equal(0.01, stats.EstimatedCostSaved);
            Assert.Equal(300, stats.LatencySavedMs);
        }

        [Fact]
        public void Stats_InitiallyEmpty()
        {
            var recorder = new PromptReplayRecorder();
            var stats = recorder.GetStats();
            Assert.Equal(0, stats.TotalAttempts);
            Assert.Equal(0, stats.HitRate);
        }

        // ── Summary ─────────────────────────────────────────────

        [Fact]
        public void GetCassetteSummary_CorrectValues()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1", "test");
            var i1 = MakeInteraction(prompt: "Q1", model: "gpt-4",
                inputTokens: 100, outputTokens: 50, cost: 0.005, latencyMs: 200);
            i1.Tags.Add("math");
            recorder.Record(i1);
            recorder.Record(MakeInteraction(prompt: "Q2", model: "claude-3",
                inputTokens: 80, outputTokens: 30, cost: 0.003, latencyMs: 150));
            recorder.StopRecording();

            var summary = recorder.GetCassetteSummary("c1");
            Assert.Equal("c1", summary["name"]);
            Assert.Equal(2, summary["interactionCount"]);
            Assert.Equal(260, summary["totalTokens"]);
            Assert.Equal(Math.Round(0.008, 6), summary["totalEstimatedCostUsd"]);
            Assert.Equal(350L, summary["totalLatencyMs"]);
        }

        [Fact]
        public void GetCassetteSummary_Unknown_Throws()
        {
            var recorder = new PromptReplayRecorder();
            Assert.Throws<KeyNotFoundException>(() => recorder.GetCassetteSummary("nope"));
        }

        // ── Compare ─────────────────────────────────────────────

        [Fact]
        public void CompareCassettes_MatchingResponses()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("v1");
            recorder.Record(MakeInteraction(prompt: "Q1", response: "A1", model: "gpt-4"));
            recorder.StopRecording();

            recorder.StartRecording("v2");
            recorder.Record(MakeInteraction(prompt: "Q1", response: "A1", model: "gpt-4"));
            recorder.StopRecording();

            var diff = recorder.CompareCassettes("v1", "v2");
            Assert.Single(diff);
            Assert.Equal("match", diff[0]["status"]);
        }

        [Fact]
        public void CompareCassettes_MismatchedResponses()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("v1");
            recorder.Record(MakeInteraction(prompt: "Q1", response: "Old answer", model: "gpt-4"));
            recorder.StopRecording();

            recorder.StartRecording("v2");
            recorder.Record(MakeInteraction(prompt: "Q1", response: "New answer", model: "gpt-4"));
            recorder.StopRecording();

            var diff = recorder.CompareCassettes("v1", "v2");
            Assert.Single(diff);
            Assert.Equal("mismatch", diff[0]["status"]);
        }

        [Fact]
        public void CompareCassettes_OnlyInA()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("v1");
            recorder.Record(MakeInteraction(prompt: "Q1", model: "gpt-4"));
            recorder.Record(MakeInteraction(prompt: "Q2", model: "gpt-4"));
            recorder.StopRecording();

            recorder.StartRecording("v2");
            recorder.Record(MakeInteraction(prompt: "Q1", model: "gpt-4"));
            recorder.StopRecording();

            var diff = recorder.CompareCassettes("v1", "v2");
            Assert.Equal(2, diff.Count);
            Assert.Contains(diff, d => d["status"].ToString() == "only_in_a");
        }

        [Fact]
        public void CompareCassettes_OnlyInB()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("v1");
            recorder.Record(MakeInteraction(prompt: "Q1", model: "gpt-4"));
            recorder.StopRecording();

            recorder.StartRecording("v2");
            recorder.Record(MakeInteraction(prompt: "Q1", model: "gpt-4"));
            recorder.Record(MakeInteraction(prompt: "Q3", model: "gpt-4"));
            recorder.StopRecording();

            var diff = recorder.CompareCassettes("v1", "v2");
            Assert.Contains(diff, d => d["status"].ToString() == "only_in_b");
        }

        [Fact]
        public void CompareCassettes_Unknown_Throws()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("v1"); recorder.StopRecording();
            Assert.Throws<KeyNotFoundException>(() => recorder.CompareCassettes("v1", "nope"));
            Assert.Throws<KeyNotFoundException>(() => recorder.CompareCassettes("nope", "v1"));
        }

        // ── RecordedInteraction ─────────────────────────────────

        [Fact]
        public void RecordedInteraction_DefaultValues()
        {
            var i = new RecordedInteraction();
            Assert.NotEmpty(i.Id);
            Assert.Equal(12, i.Id.Length);
            Assert.Equal("", i.Prompt);
            Assert.Equal("", i.Response);
            Assert.False(i.IsError);
        }

        [Fact]
        public void RecordedInteraction_ErrorState()
        {
            var i = new RecordedInteraction
            {
                Prompt = "test",
                IsError = true,
                ErrorMessage = "Rate limited"
            };
            Assert.True(i.IsError);
            Assert.Equal("Rate limited", i.ErrorMessage);
        }

        // ── Fingerprint determinism ─────────────────────────────

        [Fact]
        public void Fingerprint_SameInput_SameHash()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "test", model: "gpt-4"));
            recorder.Record(MakeInteraction(prompt: "test", model: "gpt-4"));
            var cassette = recorder.StopRecording();

            Assert.Equal(cassette.Interactions[0].Fingerprint,
                         cassette.Interactions[1].Fingerprint);
        }

        [Fact]
        public void Fingerprint_DifferentInput_DifferentHash()
        {
            var recorder = new PromptReplayRecorder();
            recorder.StartRecording("c1");
            recorder.Record(MakeInteraction(prompt: "test1", model: "gpt-4"));
            recorder.Record(MakeInteraction(prompt: "test2", model: "gpt-4"));
            var cassette = recorder.StopRecording();

            Assert.NotEqual(cassette.Interactions[0].Fingerprint,
                            cassette.Interactions[1].Fingerprint);
        }

        // ── Cassette model ──────────────────────────────────────

        [Fact]
        public void Cassette_DefaultVersion_Is1()
        {
            var c = new Cassette();
            Assert.Equal(1, c.Version);
            Assert.Empty(c.Interactions);
            Assert.Empty(c.Tags);
        }

        // ── ReplayResult model ──────────────────────────────────

        [Fact]
        public void ReplayResult_IsHit_FalseWhenNull()
        {
            var r = new ReplayResult();
            Assert.False(r.IsHit);
            Assert.Null(r.Recording);
        }

        // ── ReplayStats ─────────────────────────────────────────

        [Fact]
        public void ReplayStats_HitRate_ZeroWhenNoAttempts()
        {
            var s = new ReplayStats();
            Assert.Equal(0, s.HitRate);
        }
    }
}
