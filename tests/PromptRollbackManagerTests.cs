namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptRollbackManagerTests
    {
        // ─── Construction ───

        [Fact]
        public void Constructor_NullName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptRollbackManager(null!));
        }

        [Fact]
        public void NewManager_HasNoVersions()
        {
            var m = new PromptRollbackManager("p");
            Assert.Equal(0, m.CurrentVersion);
            Assert.Equal("", m.CurrentText);
            Assert.Empty(m.Versions);
        }

        // ─── Commit ───

        [Fact]
        public void Commit_AssignsIncrementingVersions()
        {
            var m = new PromptRollbackManager("p");
            var v1 = m.Commit("hello");
            var v2 = m.Commit("hello world");
            var v3 = m.Commit("hi");
            Assert.Equal(1, v1.Version);
            Assert.Equal(2, v2.Version);
            Assert.Equal(3, v3.Version);
            Assert.Equal(3, m.CurrentVersion);
            Assert.Equal("hi", m.CurrentText);
        }

        [Fact]
        public void Commit_NullText_Throws()
        {
            var m = new PromptRollbackManager("p");
            Assert.Throws<ArgumentNullException>(() => m.Commit(null!));
        }

        [Fact]
        public void Commit_FirstVersion_HasNullCharDelta()
        {
            var m = new PromptRollbackManager("p");
            var v = m.Commit("hello");
            Assert.Null(v.CharDelta);
        }

        [Fact]
        public void Commit_SubsequentVersion_RecordsCharDelta()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("hello");      // 5
            var v = m.Commit("hi"); // 2
            Assert.Equal(-3, v.CharDelta);
        }

        [Fact]
        public void Commit_StoresMessageAndTags()
        {
            var m = new PromptRollbackManager("p");
            var tags = new Dictionary<string, string> { ["env"] = "prod", ["author"] = "alice" };
            var v = m.Commit("text", "first commit", tags);
            Assert.Equal("first commit", v.Message);
            Assert.Equal("prod", v.Tags["env"]);
            Assert.Equal("alice", v.Tags["author"]);
        }

        [Fact]
        public void Commit_NullMessage_StoredAsEmpty()
        {
            var v = new PromptRollbackManager("p").Commit("x");
            Assert.Equal("", v.Message);
            Assert.NotNull(v.Tags);
        }

        // ─── SetScore / GetVersion ───

        [Fact]
        public void SetScore_ClampsTo0_100()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            m.SetScore(1, 150);
            Assert.Equal(100, m.GetVersion(1)!.Score);
            m.SetScore(1, -10);
            Assert.Equal(0, m.GetVersion(1)!.Score);
        }

        [Fact]
        public void SetScore_UnknownVersion_Throws()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            Assert.Throws<ArgumentException>(() => m.SetScore(99, 50));
        }

        [Fact]
        public void GetVersion_Missing_ReturnsNull()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            Assert.Null(m.GetVersion(42));
        }

        // ─── Rollback ───

        [Fact]
        public void Rollback_CreatesNewVersionWithOldText()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("v1-text");
            m.Commit("v2-text");
            m.Commit("v3-text");

            var result = m.Rollback(1);

            Assert.True(result.Success);
            Assert.Equal(3, result.PreviousVersion);
            Assert.Equal(4, result.NewVersion);
            Assert.Equal(1, result.RestoredFromVersion);
            Assert.Equal("v1-text", m.CurrentText);
            Assert.Equal(4, m.CurrentVersion);
        }

        [Fact]
        public void Rollback_MarksNewVersionAsRollback()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            m.Commit("b");
            m.Rollback(1, "revert b");
            var v = m.GetVersion(3)!;
            Assert.True(v.IsRollback);
            Assert.Equal(1, v.RolledBackFrom);
            Assert.Equal("revert b", v.Message);
        }

        [Fact]
        public void Rollback_PreservesHistory()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            m.Commit("b");
            m.Rollback(1);
            // History is non-destructive: all original versions remain.
            Assert.Equal(3, m.Versions.Count);
            Assert.Equal("a", m.GetVersion(1)!.Text);
            Assert.Equal("b", m.GetVersion(2)!.Text);
            Assert.Equal("a", m.GetVersion(3)!.Text);
        }

        [Fact]
        public void Rollback_UnknownVersion_Throws()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            Assert.Throws<ArgumentException>(() => m.Rollback(99));
        }

        [Fact]
        public void Rollback_DefaultMessage()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            m.Commit("b");
            m.Rollback(1);
            Assert.Equal("Rollback to v1", m.GetVersion(3)!.Message);
        }

        // ─── Best / Worst / Trend ───

        [Fact]
        public void BestVersion_ReturnsHighestScored()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 70);
            m.Commit("b"); m.SetScore(2, 90);
            m.Commit("c"); m.SetScore(3, 80);
            Assert.Equal(2, m.BestVersion()!.Version);
        }

        [Fact]
        public void WorstVersion_ReturnsLowestScored()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 70);
            m.Commit("b"); m.SetScore(2, 40);
            m.Commit("c"); m.SetScore(3, 80);
            Assert.Equal(2, m.WorstVersion()!.Version);
        }

        [Fact]
        public void BestVersion_NoScores_ReturnsNull()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            Assert.Null(m.BestVersion());
            Assert.Null(m.WorstVersion());
        }

        [Fact]
        public void ScoreTrend_OnlyScoredVersionsInOrder()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 50);
            m.Commit("b");                        // unscored
            m.Commit("c"); m.SetScore(3, 80);

            var trend = m.ScoreTrend();
            Assert.Equal(2, trend.Count);
            Assert.Equal((1, 50.0), trend[0]);
            Assert.Equal((3, 80.0), trend[1]);
        }

        // ─── Compare ───

        [Fact]
        public void Compare_IdenticalVersions()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("same\nline");
            m.Commit("same\nline");
            var c = m.Compare(1, 2);
            Assert.True(c.TextIdentical);
            Assert.Equal(0, c.CharDelta);
            Assert.Equal(0, c.LinesAdded);
            Assert.Equal(0, c.LinesRemoved);
        }

        [Fact]
        public void Compare_TracksCharAndLineDeltas()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("line1\nline2");
            m.Commit("line1\nline2\nline3");
            var c = m.Compare(1, 2);
            Assert.False(c.TextIdentical);
            Assert.True(c.CharDelta > 0);
            Assert.Equal(1, c.LinesAdded);
            Assert.Equal(0, c.LinesRemoved);
        }

        [Fact]
        public void Compare_ScoreDeltaNullWhenEitherMissing()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 50);
            m.Commit("b"); // unscored
            var c = m.Compare(1, 2);
            Assert.Null(c.ScoreDelta);
        }

        [Fact]
        public void Compare_ScoreDeltaComputedWhenBothPresent()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 50);
            m.Commit("b"); m.SetScore(2, 80);
            var c = m.Compare(1, 2);
            Assert.Equal(30, c.ScoreDelta);
        }

        [Fact]
        public void Compare_UnknownVersion_Throws()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a");
            Assert.Throws<ArgumentException>(() => m.Compare(1, 99));
        }

        // ─── Regressions ───

        [Fact]
        public void FindRegressions_DetectsScoreDrops()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 80);
            m.Commit("b"); m.SetScore(2, 60); // regression
            m.Commit("c"); m.SetScore(3, 90);
            m.Commit("d"); m.SetScore(4, 85); // regression

            var regs = m.FindRegressions().Select(v => v.Version).ToList();
            Assert.Equal(new[] { 2, 4 }, regs);
        }

        [Fact]
        public void FindRegressions_IgnoresUnscoredPairs()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 80);
            m.Commit("b"); // unscored
            m.Commit("c"); m.SetScore(3, 70); // unscored predecessor → not a regression
            Assert.Empty(m.FindRegressions());
        }

        // ─── AutoRollbackIfBelow ───

        [Fact]
        public void AutoRollback_BelowThreshold_RollsBackToBest()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("good"); m.SetScore(1, 90);
            m.Commit("bad");  m.SetScore(2, 40);

            var result = m.AutoRollbackIfBelow(50);
            Assert.NotNull(result);
            Assert.Equal("good", m.CurrentText);
            Assert.True(m.GetVersion(m.CurrentVersion)!.IsRollback);
        }

        [Fact]
        public void AutoRollback_AboveThreshold_DoesNothing()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 90);
            m.Commit("b"); m.SetScore(2, 80);
            Assert.Null(m.AutoRollbackIfBelow(50));
            Assert.Equal("b", m.CurrentText);
        }

        [Fact]
        public void AutoRollback_CurrentIsAlsoBest_DoesNothing()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 30);
            m.Commit("b"); m.SetScore(2, 40); // best, but still below threshold
            Assert.Null(m.AutoRollbackIfBelow(50));
        }

        [Fact]
        public void AutoRollback_UnscoredCurrent_ReturnsNull()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 90);
            m.Commit("b"); // unscored current
            Assert.Null(m.AutoRollbackIfBelow(50));
        }

        // ─── ExportJson / ImportJson round-trip ───
        // Regression: ExportJson writes snake_case ([JsonPropertyName]),
        // but ImportJson previously read camelCase, silently dropping
        // CreatedAt, IsRollback, RolledBackFrom, CharDelta, and Tags.

        [Fact]
        public void ExportImport_PreservesAllFields()
        {
            var original = new PromptRollbackManager("greeting");
            var tags = new Dictionary<string, string> { ["env"] = "prod" };
            original.Commit("v1 text", "initial", tags);
            original.Commit("v2 text longer", "expanded");
            original.SetScore(1, 75);
            original.SetScore(2, 60);
            original.Rollback(1, "revert: v2 regressed");

            var json = original.ExportJson();
            var imported = PromptRollbackManager.ImportJson(json);

            Assert.Equal("greeting", imported.Name);
            Assert.Equal(3, imported.Versions.Count);
            Assert.Equal(original.CurrentText, imported.CurrentText);
            Assert.Equal(original.CurrentVersion, imported.CurrentVersion);

            var v1Orig = original.GetVersion(1)!;
            var v1Imp  = imported.GetVersion(1)!;
            Assert.Equal(v1Orig.Text, v1Imp.Text);
            Assert.Equal(v1Orig.Message, v1Imp.Message);
            Assert.Equal(v1Orig.Score, v1Imp.Score);
            Assert.Equal(v1Orig.CreatedAt, v1Imp.CreatedAt, TimeSpan.FromSeconds(1));
            Assert.Equal("prod", v1Imp.Tags["env"]);

            var v2Orig = original.GetVersion(2)!;
            var v2Imp  = imported.GetVersion(2)!;
            Assert.Equal(v2Orig.CharDelta, v2Imp.CharDelta);

            // The rollback version (v3) must round-trip its IsRollback / RolledBackFrom flags.
            var v3Imp = imported.GetVersion(3)!;
            Assert.True(v3Imp.IsRollback);
            Assert.Equal(1, v3Imp.RolledBackFrom);
        }

        [Fact]
        public void ImportJson_AcceptsCamelCaseLegacyPayload()
        {
            // Legacy / hand-written JSON using camelCase should still load.
            string legacy = """
            {
              "name": "legacy",
              "currentVersion": 2,
              "totalVersions": 2,
              "versions": [
                {
                  "version": 1,
                  "text": "hello",
                  "createdAt": "2025-01-01T00:00:00Z",
                  "message": "first",
                  "isRollback": false,
                  "tags": { "env": "dev" }
                },
                {
                  "version": 2,
                  "text": "hello world",
                  "createdAt": "2025-01-02T00:00:00Z",
                  "message": "second",
                  "score": 75,
                  "isRollback": true,
                  "rolledBackFrom": 1,
                  "charDelta": 6
                }
              ]
            }
            """;

            var m = PromptRollbackManager.ImportJson(legacy);
            Assert.Equal("legacy", m.Name);
            Assert.Equal(2, m.Versions.Count);
            Assert.True(m.GetVersion(2)!.IsRollback);
            Assert.Equal(1, m.GetVersion(2)!.RolledBackFrom);
            Assert.Equal(6, m.GetVersion(2)!.CharDelta);
            Assert.Equal(75, m.GetVersion(2)!.Score);
            Assert.Equal("dev", m.GetVersion(1)!.Tags["env"]);
        }

        [Fact]
        public void ExportJson_IsValidJsonAndContainsName()
        {
            var m = new PromptRollbackManager("xyz");
            m.Commit("a");
            var json = m.ExportJson();
            Assert.Contains("\"xyz\"", json);
            // Must not throw.
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal("xyz", doc.RootElement.GetProperty("name").GetString());
        }

        // ─── Summary ───

        [Fact]
        public void Summary_EmptyManager_NoVersionsMessage()
        {
            var m = new PromptRollbackManager("p");
            Assert.Contains("No versions", m.Summary());
        }

        [Fact]
        public void Summary_IncludesBestAndRegressionFlags()
        {
            var m = new PromptRollbackManager("p");
            m.Commit("a"); m.SetScore(1, 90);
            m.Commit("b"); m.SetScore(2, 50);
            var s = m.Summary();
            Assert.Contains("Best:", s);
            Assert.Contains("regression", s);
        }
    }
}
