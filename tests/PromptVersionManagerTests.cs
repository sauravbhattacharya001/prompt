namespace Prompt.Tests
{
    using Xunit;

    public class PromptVersionManagerTests
    {
        // ================================================================
        // CreateVersion
        // ================================================================

        [Fact]
        public void CreateVersion_FirstVersion_ReturnsVersionOne()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("greeting", "Hello {{name}}");
            Assert.Equal(1, v.VersionNumber);
            Assert.Equal("Hello {{name}}", v.TemplateText);
        }

        [Fact]
        public void CreateVersion_MultipleVersions_IncrementsVersionNumber()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");
            var v3 = mgr.CreateVersion("t", "v3");
            Assert.Equal(3, v3.VersionNumber);
        }

        [Fact]
        public void CreateVersion_WithDescription_StoresDescription()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("t", "text", description: "initial");
            Assert.Equal("initial", v.Description);
        }

        [Fact]
        public void CreateVersion_WithAuthor_StoresAuthor()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("t", "text", author: "alice");
            Assert.Equal("alice", v.Author);
        }

        [Fact]
        public void CreateVersion_WithDefaults_StoresDefaults()
        {
            var mgr = new PromptVersionManager();
            var defaults = new Dictionary<string, string> { { "name", "World" } };
            var v = mgr.CreateVersion("t", "Hello {{name}}", defaults: defaults);
            Assert.NotNull(v.DefaultValues);
            Assert.Equal("World", v.DefaultValues!["name"]);
        }

        [Fact]
        public void CreateVersion_NullName_Throws()
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() =>
                mgr.CreateVersion(null!, "text"));
        }

        [Fact]
        public void CreateVersion_EmptyText_Throws()
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() =>
                mgr.CreateVersion("t", ""));
        }

        [Fact]
        public void CreateVersion_CaseInsensitiveName_SameHistory()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("Greeting", "v1");
            var v2 = mgr.CreateVersion("greeting", "v2");
            Assert.Equal(2, v2.VersionNumber);
        }

        // ================================================================
        // GetLatest
        // ================================================================

        [Fact]
        public void GetLatest_ExistingTemplate_ReturnsLastVersion()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");
            var latest = mgr.GetLatest("t");
            Assert.NotNull(latest);
            Assert.Equal("v2", latest!.TemplateText);
        }

        [Fact]
        public void GetLatest_UnknownTemplate_ReturnsNull()
        {
            var mgr = new PromptVersionManager();
            Assert.Null(mgr.GetLatest("nonexistent"));
        }

        [Fact]
        public void GetLatest_NullName_ReturnsNull()
        {
            var mgr = new PromptVersionManager();
            Assert.Null(mgr.GetLatest(null!));
        }

        // ================================================================
        // GetVersion
        // ================================================================

        [Fact]
        public void GetVersion_ExistingVersion_ReturnsCorrectVersion()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "first");
            mgr.CreateVersion("t", "second");
            var v = mgr.GetVersion("t", 1);
            Assert.NotNull(v);
            Assert.Equal("first", v!.TemplateText);
        }

        [Fact]
        public void GetVersion_NonexistentVersion_ReturnsNull()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "first");
            Assert.Null(mgr.GetVersion("t", 99));
        }

        // ================================================================
        // GetHistory
        // ================================================================

        [Fact]
        public void GetHistory_ReturnsAllVersionsOrdered()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "a");
            mgr.CreateVersion("t", "b");
            mgr.CreateVersion("t", "c");
            var history = mgr.GetHistory("t");
            Assert.Equal(3, history.Count);
            Assert.Equal(1, history[0].VersionNumber);
            Assert.Equal(3, history[2].VersionNumber);
        }

        [Fact]
        public void GetHistory_UnknownTemplate_ReturnsEmpty()
        {
            var mgr = new PromptVersionManager();
            Assert.Empty(mgr.GetHistory("nope"));
        }

        // ================================================================
        // HasChanges
        // ================================================================

        [Fact]
        public void HasChanges_SameText_ReturnsFalse()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "hello");
            Assert.False(mgr.HasChanges("t", "hello"));
        }

        [Fact]
        public void HasChanges_DifferentText_ReturnsTrue()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "hello");
            Assert.True(mgr.HasChanges("t", "goodbye"));
        }

        [Fact]
        public void HasChanges_NoHistory_ReturnsTrue()
        {
            var mgr = new PromptVersionManager();
            Assert.True(mgr.HasChanges("t", "anything"));
        }

        // ================================================================
        // Rollback
        // ================================================================

        [Fact]
        public void Rollback_RestoresOldVersionAsNewVersion()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "original");
            mgr.CreateVersion("t", "modified");
            var rb = mgr.Rollback("t", 1, author: "bot");
            Assert.Equal(3, rb.VersionNumber);
            Assert.Equal("original", rb.TemplateText);
            Assert.Equal("Rollback to v1", rb.Description);
            Assert.Equal("bot", rb.Author);
        }

        [Fact]
        public void Rollback_InvalidVersion_Throws()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");
            Assert.Throws<ArgumentException>(() =>
                mgr.Rollback("t", 99));
        }

        [Fact]
        public void Rollback_UnknownTemplate_Throws()
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() =>
                mgr.Rollback("nope", 1));
        }

        // ================================================================
        // Compare / VersionDiff
        // ================================================================

        [Fact]
        public void Compare_DetectsAddedAndRemovedLines()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "line1\nline2");
            mgr.CreateVersion("t", "line1\nline3");
            var diff = mgr.Compare("t", 1, 2);
            Assert.True(diff.HasTextChanges);
            Assert.Contains("line3", diff.AddedLines);
            Assert.Contains("line2", diff.RemovedLines);
        }

        [Fact]
        public void Compare_DetectsDefaultChanges()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text", defaults: new() { { "a", "1" } });
            mgr.CreateVersion("t", "text", defaults: new() { { "b", "2" } });
            var diff = mgr.Compare("t", 1, 2);
            Assert.True(diff.HasDefaultChanges);
            Assert.Contains("b", diff.AddedDefaults);
            Assert.Contains("a", diff.RemovedDefaults);
        }

        [Fact]
        public void Compare_NoChanges_ReportsSummaryCorrectly()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "same");
            mgr.CreateVersion("t", "same");
            var diff = mgr.Compare("t", 1, 2);
            Assert.False(diff.HasTextChanges);
            Assert.Equal("No changes", diff.GetSummary());
        }

        // ================================================================
        // DeleteHistory / ClearAll
        // ================================================================

        [Fact]
        public void DeleteHistory_RemovesTemplate()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");
            Assert.True(mgr.DeleteHistory("t"));
            Assert.Null(mgr.GetLatest("t"));
            Assert.Equal(0, mgr.TemplateCount);
        }

        [Fact]
        public void DeleteHistory_UnknownTemplate_ReturnsFalse()
        {
            var mgr = new PromptVersionManager();
            Assert.False(mgr.DeleteHistory("nope"));
        }

        [Fact]
        public void ClearAll_RemovesEverything()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("a", "text");
            mgr.CreateVersion("b", "text");
            mgr.ClearAll();
            Assert.Equal(0, mgr.TemplateCount);
            Assert.Equal(0, mgr.TotalVersionCount);
        }

        // ================================================================
        // Properties
        // ================================================================

        [Fact]
        public void TemplateCount_TracksCorrectly()
        {
            var mgr = new PromptVersionManager();
            Assert.Equal(0, mgr.TemplateCount);
            mgr.CreateVersion("a", "text");
            mgr.CreateVersion("b", "text");
            Assert.Equal(2, mgr.TemplateCount);
        }

        [Fact]
        public void TotalVersionCount_TracksCorrectly()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("a", "v1");
            mgr.CreateVersion("a", "v2");
            mgr.CreateVersion("b", "v1");
            Assert.Equal(3, mgr.TotalVersionCount);
        }

        // ================================================================
        // Serialization
        // ================================================================

        [Fact]
        public void ToJson_FromJson_Roundtrips()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("greeting", "Hello {{name}}",
                description: "initial", author: "alice",
                defaults: new() { { "name", "World" } });
            mgr.CreateVersion("greeting", "Hi {{name}}!",
                description: "informal");

            var json = mgr.ToJson();
            var restored = PromptVersionManager.FromJson(json);

            Assert.Equal(1, restored.TemplateCount);
            Assert.Equal(2, restored.TotalVersionCount);
            var latest = restored.GetLatest("greeting");
            Assert.NotNull(latest);
            Assert.Equal("Hi {{name}}!", latest!.TemplateText);
            Assert.Equal("informal", latest.Description);
        }

        [Fact]
        public void FromJson_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PromptVersionManager.FromJson(""));
            Assert.Throws<ArgumentException>(() =>
                PromptVersionManager.FromJson(null!));
        }

        // ================================================================
        // GetTrackedTemplates
        // ================================================================

        [Fact]
        public void GetTrackedTemplates_ReturnsSortedNames()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("zeta", "text");
            mgr.CreateVersion("alpha", "text");
            mgr.CreateVersion("mid", "text");
            var names = mgr.GetTrackedTemplates();
            Assert.Equal(3, names.Count);
            Assert.Equal("alpha", names[0]);
            Assert.Equal("mid", names[1]);
            Assert.Equal("zeta", names[2]);
        }

        // ================================================================
        // Pruning
        // ================================================================

        [Fact]
        public void CreateVersion_PrunesOldestWhenOverLimit()
        {
            var mgr = new PromptVersionManager();
            for (int i = 0; i < PromptVersionManager.MaxVersionsPerTemplate + 5; i++)
            {
                mgr.CreateVersion("t", $"version {i}");
            }
            Assert.Equal(PromptVersionManager.MaxVersionsPerTemplate,
                mgr.GetVersionCount("t"));
            // Oldest versions should have been pruned
            Assert.Null(mgr.GetVersion("t", 1));
        }
    }
}
