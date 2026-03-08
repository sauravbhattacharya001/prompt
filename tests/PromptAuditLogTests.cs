namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptAuditLogTests
    {
        private static AuditEntry MakeEntry(
            string promptId = "test-prompt",
            bool success = true,
            string? userId = "user1",
            string? model = "gpt-4",
            int inputTokens = 100,
            int outputTokens = 50,
            double? costUsd = 0.01,
            AuditSeverity severity = AuditSeverity.Info)
        {
            return new AuditEntry(
                promptId: promptId,
                success: success,
                userId: userId,
                model: model,
                inputTokens: inputTokens,
                outputTokens: outputTokens,
                costUsd: costUsd,
                severity: severity,
                duration: TimeSpan.FromMilliseconds(150));
        }

        // ── Construction ──────────────────────────────────────────

        [Fact]
        public void DefaultConstruction_EmptyLog()
        {
            var log = new PromptAuditLog();
            Assert.Equal(0, log.Count);
        }

        [Fact]
        public void CustomConfig_Applies()
        {
            var log = new PromptAuditLog(new AuditLogConfig
            {
                MaxEntries = 50,
                RetentionPeriod = TimeSpan.FromDays(7),
                EnableHashChain = false,
                AutoPurge = false
            });
            Assert.Equal(0, log.Count);
        }

        // ── Append ────────────────────────────────────────────────

        [Fact]
        public void Append_AddsEntry()
        {
            var log = new PromptAuditLog();
            var entry = log.Append(MakeEntry());
            Assert.Equal(1, log.Count);
            Assert.NotEmpty(entry.Id);
            Assert.NotEmpty(entry.Hash);
        }

        [Fact]
        public void Append_SetsHashChain()
        {
            var log = new PromptAuditLog();
            var e1 = log.Append(MakeEntry());
            var e2 = log.Append(MakeEntry());
            Assert.Equal("", e1.PreviousHash);
            Assert.Equal(e1.Hash, e2.PreviousHash);
            Assert.NotEqual(e1.Hash, e2.Hash);
        }

        [Fact]
        public void Append_ThrowsOnNull()
        {
            var log = new PromptAuditLog();
            Assert.Throws<ArgumentNullException>(() => log.Append(null!));
        }

        [Fact]
        public void Append_RespectsMaxEntries()
        {
            var log = new PromptAuditLog(new AuditLogConfig { MaxEntries = 3 });
            for (int i = 0; i < 5; i++)
                log.Append(MakeEntry(promptId: $"p{i}"));
            Assert.Equal(3, log.Count);
        }

        [Fact]
        public void Append_WithoutHashChain()
        {
            var log = new PromptAuditLog(new AuditLogConfig { EnableHashChain = false });
            var e = log.Append(MakeEntry());
            Assert.Equal("", e.Hash);
            Assert.Equal("", e.PreviousHash);
        }

        // ── AuditEntry ────────────────────────────────────────────

        [Fact]
        public void AuditEntry_HasUniqueId()
        {
            var e1 = MakeEntry();
            var e2 = MakeEntry();
            Assert.NotEqual(e1.Id, e2.Id);
        }

        [Fact]
        public void AuditEntry_ThrowsOnNullPromptId()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AuditEntry(promptId: null!, success: true));
        }

        [Fact]
        public void AuditEntry_StoresMetadata()
        {
            var meta = new Dictionary<string, string> { ["env"] = "prod", ["region"] = "us-west" };
            var entry = new AuditEntry("p1", true, metadata: meta);
            Assert.Equal("prod", entry.Metadata["env"]);
            Assert.Equal("us-west", entry.Metadata["region"]);
        }

        [Fact]
        public void AuditEntry_DefaultsToEmptyMetadata()
        {
            var entry = MakeEntry();
            Assert.Empty(entry.Metadata);
        }

        [Fact]
        public void AuditEntry_CopiesMetadata()
        {
            var meta = new Dictionary<string, string> { ["key"] = "val" };
            var entry = new AuditEntry("p1", true, metadata: meta);
            meta["key"] = "changed";
            Assert.Equal("val", entry.Metadata["key"]);
        }

        // ── Query ─────────────────────────────────────────────────

        [Fact]
        public void Query_AllEntries()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            log.Append(MakeEntry());
            var results = log.Query();
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public void Query_ByPromptId()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(promptId: "a"));
            log.Append(MakeEntry(promptId: "b"));
            log.Append(MakeEntry(promptId: "a"));
            var results = log.Query(promptId: "a");
            Assert.Equal(2, results.Count);
            Assert.All(results, e => Assert.Equal("a", e.PromptId));
        }

        [Fact]
        public void Query_ByUserId()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(userId: "alice"));
            log.Append(MakeEntry(userId: "bob"));
            var results = log.Query(userId: "alice");
            Assert.Single(results);
        }

        [Fact]
        public void Query_ByModel()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(model: "gpt-4"));
            log.Append(MakeEntry(model: "claude-3"));
            var results = log.Query(model: "claude-3");
            Assert.Single(results);
        }

        [Fact]
        public void Query_BySuccess()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(success: true));
            log.Append(MakeEntry(success: false));
            log.Append(MakeEntry(success: true));
            Assert.Equal(2, log.Query(success: true).Count);
            Assert.Single(log.Query(success: false));
        }

        [Fact]
        public void Query_ByMinSeverity()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(severity: AuditSeverity.Info));
            log.Append(MakeEntry(severity: AuditSeverity.Warning));
            log.Append(MakeEntry(severity: AuditSeverity.Error));
            log.Append(MakeEntry(severity: AuditSeverity.Critical));

            Assert.Equal(2, log.Query(minSeverity: AuditSeverity.Error).Count);
            Assert.Equal(4, log.Query(minSeverity: AuditSeverity.Info).Count);
        }

        [Fact]
        public void Query_WithLimit()
        {
            var log = new PromptAuditLog();
            for (int i = 0; i < 10; i++)
                log.Append(MakeEntry());
            var results = log.Query(limit: 3);
            Assert.Equal(3, results.Count);
        }

        [Fact]
        public void Query_Ascending()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(promptId: "first"));
            log.Append(MakeEntry(promptId: "second"));
            var results = log.Query(descending: false);
            Assert.Equal("first", results[0].PromptId);
        }

        [Fact]
        public void Query_CombinedFilters()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(promptId: "a", userId: "u1", success: true));
            log.Append(MakeEntry(promptId: "a", userId: "u1", success: false));
            log.Append(MakeEntry(promptId: "a", userId: "u2", success: true));
            log.Append(MakeEntry(promptId: "b", userId: "u1", success: true));

            var results = log.Query(promptId: "a", userId: "u1", success: true);
            Assert.Single(results);
        }

        // ── GetById ───────────────────────────────────────────────

        [Fact]
        public void GetById_FindsEntry()
        {
            var log = new PromptAuditLog();
            var entry = log.Append(MakeEntry());
            var found = log.GetById(entry.Id);
            Assert.NotNull(found);
            Assert.Equal(entry.Id, found!.Id);
        }

        [Fact]
        public void GetById_ReturnsNullForUnknown()
        {
            var log = new PromptAuditLog();
            Assert.Null(log.GetById("nonexistent"));
        }

        // ── Summarize ─────────────────────────────────────────────

        [Fact]
        public void Summarize_EmptyLog()
        {
            var log = new PromptAuditLog();
            var summary = log.Summarize();
            Assert.Equal(0, summary.TotalEntries);
        }

        [Fact]
        public void Summarize_ComputesStats()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(success: true, inputTokens: 100, outputTokens: 50, costUsd: 0.01));
            log.Append(MakeEntry(success: true, inputTokens: 200, outputTokens: 100, costUsd: 0.02));
            log.Append(MakeEntry(success: false, inputTokens: 50, outputTokens: 0, costUsd: 0.005));

            var summary = log.Summarize();
            Assert.Equal(3, summary.TotalEntries);
            Assert.Equal(2, summary.SuccessCount);
            Assert.Equal(1, summary.FailureCount);
            Assert.Equal(350, summary.TotalInputTokens);
            Assert.Equal(150, summary.TotalOutputTokens);
            Assert.True(summary.TotalCostUsd > 0);
            Assert.True(summary.AverageDurationMs > 0);
            Assert.True(summary.SuccessRate > 60);
        }

        [Fact]
        public void Summarize_ByModel()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(model: "gpt-4"));
            log.Append(MakeEntry(model: "gpt-4"));
            log.Append(MakeEntry(model: "claude-3"));

            var summary = log.Summarize();
            Assert.Equal(2, summary.ByModel["gpt-4"]);
            Assert.Equal(1, summary.ByModel["claude-3"]);
        }

        [Fact]
        public void Summarize_ByUser()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(userId: "alice"));
            log.Append(MakeEntry(userId: "bob"));
            log.Append(MakeEntry(userId: null));

            var summary = log.Summarize();
            Assert.Equal(1, summary.ByUser["alice"]);
            Assert.Equal(1, summary.ByUser["(anonymous)"]);
        }

        [Fact]
        public void Summarize_BySeverity()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(severity: AuditSeverity.Info));
            log.Append(MakeEntry(severity: AuditSeverity.Error));
            log.Append(MakeEntry(severity: AuditSeverity.Error));

            var summary = log.Summarize();
            Assert.Equal(1, summary.BySeverity[AuditSeverity.Info]);
            Assert.Equal(2, summary.BySeverity[AuditSeverity.Error]);
        }

        // ── VerifyIntegrity ───────────────────────────────────────

        [Fact]
        public void VerifyIntegrity_ValidChain()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            log.Append(MakeEntry());
            log.Append(MakeEntry());

            var result = log.VerifyIntegrity();
            Assert.True(result.IsValid);
            Assert.Equal(3, result.EntriesChecked);
            Assert.Equal(0, result.CorruptEntries);
        }

        [Fact]
        public void VerifyIntegrity_DisabledHashChain()
        {
            var log = new PromptAuditLog(new AuditLogConfig { EnableHashChain = false });
            log.Append(MakeEntry());
            var result = log.VerifyIntegrity();
            Assert.True(result.IsValid);
            Assert.Equal(0, result.EntriesChecked);
        }

        [Fact]
        public void VerifyIntegrity_EmptyLog()
        {
            var log = new PromptAuditLog();
            var result = log.VerifyIntegrity();
            Assert.True(result.IsValid);
            Assert.Equal(0, result.EntriesChecked);
        }

        // ── Recent ────────────────────────────────────────────────

        [Fact]
        public void Recent_ReturnsLatest()
        {
            var log = new PromptAuditLog();
            for (int i = 0; i < 5; i++)
                log.Append(MakeEntry(promptId: $"p{i}"));

            var recent = log.Recent(3);
            Assert.Equal(3, recent.Count);
            Assert.Equal("p4", recent[0].PromptId);
            Assert.Equal("p3", recent[1].PromptId);
        }

        [Fact]
        public void Recent_AllWhenFewerThanCount()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            var recent = log.Recent(10);
            Assert.Single(recent);
        }

        // ── Export ────────────────────────────────────────────────

        [Fact]
        public void ExportJson_ReturnsValidJson()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            log.Append(MakeEntry(success: false, severity: AuditSeverity.Error));

            var json = log.ExportJson();
            Assert.StartsWith("[", json);
            Assert.Contains("promptId", json);
            Assert.Contains("test-prompt", json);
        }

        [Fact]
        public void ExportJson_IndentedOption()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            var json = log.ExportJson(indented: true);
            Assert.Contains("\n", json);
        }

        [Fact]
        public void ExportCsv_HasHeaders()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            var csv = log.ExportCsv();
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(lines.Length >= 2);
            Assert.Contains("Id,Timestamp", lines[0]);
        }

        [Fact]
        public void ExportCsv_EscapesCommas()
        {
            var log = new PromptAuditLog();
            log.Append(new AuditEntry("test,with,commas", true));
            var csv = log.ExportCsv();
            Assert.Contains("\"test,with,commas\"", csv);
        }

        // ── PurgeExpired ──────────────────────────────────────────

        [Fact]
        public void PurgeExpired_RemovesNothing_WhenFresh()
        {
            var log = new PromptAuditLog(new AuditLogConfig { AutoPurge = false });
            log.Append(MakeEntry());
            var purged = log.PurgeExpired();
            Assert.Equal(0, purged);
            Assert.Equal(1, log.Count);
        }

        // ── Clear ─────────────────────────────────────────────────

        [Fact]
        public void Clear_RemovesAllEntries()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            log.Append(MakeEntry());
            log.Clear();
            Assert.Equal(0, log.Count);
        }

        [Fact]
        public void Clear_ResetsHashChain()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry());
            log.Clear();
            var entry = log.Append(MakeEntry());
            Assert.Equal("", entry.PreviousHash);
        }

        // ── GetLoggedPromptIds / GetLoggedUserIds ─────────────────

        [Fact]
        public void GetLoggedPromptIds_ReturnsDistinct()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(promptId: "a"));
            log.Append(MakeEntry(promptId: "b"));
            log.Append(MakeEntry(promptId: "a"));
            var ids = log.GetLoggedPromptIds();
            Assert.Equal(2, ids.Count);
            Assert.Contains("a", ids);
            Assert.Contains("b", ids);
        }

        [Fact]
        public void GetLoggedUserIds_ExcludesNull()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(userId: "alice"));
            log.Append(MakeEntry(userId: null));
            var ids = log.GetLoggedUserIds();
            Assert.Single(ids);
            Assert.Equal("alice", ids[0]);
        }

        // ── GetErrorRate ──────────────────────────────────────────

        [Fact]
        public void GetErrorRate_ComputesCorrectly()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(promptId: "p1", success: true));
            log.Append(MakeEntry(promptId: "p1", success: true));
            log.Append(MakeEntry(promptId: "p1", success: false));
            log.Append(MakeEntry(promptId: "p2", success: false));

            var rate = log.GetErrorRate("p1");
            Assert.True(rate > 33 && rate < 34, $"Expected ~33.33%, got {rate}");
        }

        [Fact]
        public void GetErrorRate_ZeroForUnknownPrompt()
        {
            var log = new PromptAuditLog();
            Assert.Equal(0, log.GetErrorRate("nonexistent"));
        }

        [Fact]
        public void GetErrorRate_ZeroWhenAllSucceed()
        {
            var log = new PromptAuditLog();
            log.Append(MakeEntry(promptId: "p1", success: true));
            log.Append(MakeEntry(promptId: "p1", success: true));
            Assert.Equal(0, log.GetErrorRate("p1"));
        }
    }
}
