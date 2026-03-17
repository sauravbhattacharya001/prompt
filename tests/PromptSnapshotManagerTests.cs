namespace Prompt.Tests
{
    using Xunit;

    public class PromptSnapshotManagerTests
    {
        private static PromptLibrary CreateTestLibrary()
        {
            var lib = new PromptLibrary();
            lib.Add("greet", new PromptTemplate("Hello {{name}}!"),
                description: "Greeting", category: "social", tags: new[] { "hello" });
            lib.Add("summarize", new PromptTemplate("Summarize: {{text}}"),
                description: "Summarizer", category: "writing");
            return lib;
        }

        [Fact]
        public void TakeSnapshot_CreatesSnapshot()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();

            var snap = mgr.TakeSnapshot(lib, "v1", "Initial version");

            Assert.Equal("v1", snap.Name);
            Assert.Equal("Initial version", snap.Description);
            Assert.Equal(2, snap.EntryCount);
            Assert.NotEmpty(snap.ContentHash);
            Assert.NotEmpty(snap.Id);
            Assert.Equal(1, mgr.Count);
        }

        [Fact]
        public void TakeSnapshot_NullLibrary_Throws()
        {
            var mgr = new PromptSnapshotManager();
            Assert.Throws<ArgumentNullException>(() => mgr.TakeSnapshot(null!, "v1"));
        }

        [Fact]
        public void TakeSnapshot_EmptyName_Throws()
        {
            var mgr = new PromptSnapshotManager();
            Assert.Throws<ArgumentException>(() => mgr.TakeSnapshot(new PromptLibrary(), ""));
        }

        [Fact]
        public void TakeSnapshot_DuplicateName_Throws()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "v1");
            Assert.Throws<ArgumentException>(() => mgr.TakeSnapshot(lib, "v1"));
        }

        [Fact]
        public void GetSnapshot_ReturnsCorrect()
        {
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(CreateTestLibrary(), "baseline");
            var snap = mgr.GetSnapshot("baseline");
            Assert.Equal("baseline", snap.Name);
        }

        [Fact]
        public void GetSnapshot_NotFound_Throws()
        {
            var mgr = new PromptSnapshotManager();
            Assert.Throws<KeyNotFoundException>(() => mgr.GetSnapshot("nope"));
        }

        [Fact]
        public void Contains_Works()
        {
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(CreateTestLibrary(), "v1");
            Assert.True(mgr.Contains("v1"));
            Assert.False(mgr.Contains("v2"));
        }

        [Fact]
        public void GetLatest_ReturnsNewest()
        {
            var mgr = new PromptSnapshotManager();
            Assert.Null(mgr.GetLatest());

            mgr.TakeSnapshot(CreateTestLibrary(), "first");
            mgr.TakeSnapshot(CreateTestLibrary(), "second");

            Assert.Equal("second", mgr.GetLatest()!.Name);
        }

        [Fact]
        public void Remove_Works()
        {
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(CreateTestLibrary(), "v1");
            Assert.True(mgr.Remove("v1"));
            Assert.False(mgr.Contains("v1"));
            Assert.Equal(0, mgr.Count);
            Assert.False(mgr.Remove("v1"));
        }

        [Fact]
        public void Clear_Works()
        {
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(CreateTestLibrary(), "a");
            mgr.TakeSnapshot(CreateTestLibrary(), "b");
            mgr.Clear();
            Assert.Equal(0, mgr.Count);
        }

        [Fact]
        public void Names_InChronologicalOrder()
        {
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(CreateTestLibrary(), "beta");
            mgr.TakeSnapshot(CreateTestLibrary(), "alpha");
            var names = mgr.Names;
            Assert.Equal(new[] { "beta", "alpha" }, names);
        }

        [Fact]
        public void Rollback_RestoresLibrary()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "v1");

            lib.Remove("greet");
            lib.Add("new-entry", new PromptTemplate("{{x}}"));

            var restored = mgr.Rollback("v1");
            Assert.True(restored.Contains("greet"));
            Assert.True(restored.Contains("summarize"));
            Assert.False(restored.Contains("new-entry"));
            Assert.Equal(2, restored.Count);
        }

        [Fact]
        public void Rollback_NotFound_Throws()
        {
            var mgr = new PromptSnapshotManager();
            Assert.Throws<KeyNotFoundException>(() => mgr.Rollback("nope"));
        }

        [Fact]
        public void SnapshotRestore_PreservesTemplateContent()
        {
            var lib = new PromptLibrary();
            lib.Add("test", new PromptTemplate("Hello {{who}}!",
                new Dictionary<string, string> { ["who"] = "world" }),
                description: "Test", category: "cat", tags: new[] { "t1", "t2" });

            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "snap");

            var restored = mgr.Rollback("snap");
            var entry = restored.Get("test");
            Assert.Equal("Hello {{who}}!", entry.Template.Template);
            Assert.Equal("world", entry.Template.Defaults["who"]);
            Assert.Equal("Test", entry.Description);
            Assert.Equal("cat", entry.Category);
            Assert.True(entry.HasTag("t1"));
            Assert.True(entry.HasTag("t2"));
        }

        [Fact]
        public void Compare_DetectsAdded()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            lib.Add("new-one", new PromptTemplate("{{x}}"));
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            Assert.Equal(1, diff.AddedCount);
            Assert.Equal("new-one", diff.Diffs.First(d => d.DiffType == SnapshotDiffType.Added).EntryName);
        }

        [Fact]
        public void Compare_DetectsRemoved()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            lib.Remove("greet");
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            Assert.Equal(1, diff.RemovedCount);
        }

        [Fact]
        public void Compare_DetectsTemplateChanged()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            lib.Update("greet", template: new PromptTemplate("Hi {{name}}!"));
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            Assert.True(diff.Diffs.Any(d => d.DiffType == SnapshotDiffType.TemplateChanged && d.EntryName == "greet"));
        }

        [Fact]
        public void Compare_DetectsMetadataChanged()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            lib.Update("greet", description: "Updated description");
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            Assert.True(diff.Diffs.Any(d => d.DiffType == SnapshotDiffType.MetadataChanged));
        }

        [Fact]
        public void Compare_DetectsDefaultsChanged()
        {
            var lib = new PromptLibrary();
            lib.Add("t", new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "old" }));
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            lib.Update("t", template: new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "new" }));
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            Assert.True(diff.Diffs.Any(d => d.DiffType == SnapshotDiffType.DefaultsChanged));
        }

        [Fact]
        public void Compare_IdenticalSnapshots()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "a");
            mgr.TakeSnapshot(lib, "b");

            var diff = mgr.Compare("a", "b");
            Assert.True(diff.AreIdentical);
            Assert.Equal(0, diff.AddedCount);
            Assert.Equal(0, diff.RemovedCount);
            Assert.Equal(0, diff.ModifiedCount);
        }

        [Fact]
        public void CompareWithCurrent_Works()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "snap");

            lib.Add("extra", new PromptTemplate("{{x}}"));

            var diff = mgr.CompareWithCurrent("snap", lib);
            Assert.Equal(1, diff.AddedCount);
            Assert.Equal("(current)", diff.ToSnapshot);
        }

        [Fact]
        public void HasChanged_DetectsChange()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "v1");

            Assert.False(mgr.HasChanged("v1", lib));

            lib.Add("new", new PromptTemplate("{{x}}"));
            Assert.True(mgr.HasChanged("v1", lib));
        }

        [Fact]
        public void ListSnapshots_ChronologicalOrder()
        {
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(CreateTestLibrary(), "first");
            mgr.TakeSnapshot(CreateTestLibrary(), "second");
            mgr.TakeSnapshot(CreateTestLibrary(), "third");

            var list = mgr.ListSnapshots();
            Assert.Equal(3, list.Count);
            Assert.Equal("first", list[0].Name);
            Assert.Equal("second", list[1].Name);
            Assert.Equal("third", list[2].Name);
        }

        [Fact]
        public void DiffReport_ToText_Works()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            lib.Add("added", new PromptTemplate("{{x}}"));
            lib.Remove("greet");
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            var text = diff.ToText();

            Assert.Contains("before", text);
            Assert.Contains("after", text);
            Assert.Contains("[+] added", text);
            Assert.Contains("[-] greet", text);
        }

        [Fact]
        public void DiffReport_ToText_Identical()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "a");
            mgr.TakeSnapshot(lib, "b");

            var text = mgr.Compare("a", "b").ToText();
            Assert.Contains("No differences", text);
        }

        [Fact]
        public void Serialization_RoundTrip()
        {
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(CreateTestLibrary(), "v1", "First");
            var lib2 = CreateTestLibrary();
            lib2.Add("extra", new PromptTemplate("{{x}}"));
            mgr.TakeSnapshot(lib2, "v2", "Added extra");

            var json = mgr.ToJson();
            var loaded = PromptSnapshotManager.FromJson(json);

            Assert.Equal(2, loaded.Count);
            Assert.True(loaded.Contains("v1"));
            Assert.True(loaded.Contains("v2"));

            var snap1 = loaded.GetSnapshot("v1");
            Assert.Equal("First", snap1.Description);
            Assert.Equal(2, snap1.EntryCount);

            var snap2 = loaded.GetSnapshot("v2");
            Assert.Equal(3, snap2.EntryCount);

            // Verify rollback works after deserialization
            var restored = loaded.Rollback("v1");
            Assert.Equal(2, restored.Count);
            Assert.True(restored.Contains("greet"));
        }

        [Fact]
        public void Serialization_EmptyManager()
        {
            var mgr = new PromptSnapshotManager();
            var json = mgr.ToJson();
            var loaded = PromptSnapshotManager.FromJson(json);
            Assert.Equal(0, loaded.Count);
        }

        [Fact]
        public void FromJson_InvalidJson_Throws()
        {
            Assert.ThrowsAny<Exception>(() =>
                PromptSnapshotManager.FromJson("not json"));
        }

        [Fact]
        public void FromJson_NullInput_Throws()
        {
            Assert.ThrowsAny<Exception>(() => PromptSnapshotManager.FromJson(null!));
        }

        [Fact]
        public async Task SaveAndLoad_FileRoundTrip()
        {
            var tmpFile = Path.Combine(Path.GetTempPath(), $"snapshot-test-{Guid.NewGuid()}.json");
            try
            {
                var mgr = new PromptSnapshotManager();
                mgr.TakeSnapshot(CreateTestLibrary(), "file-test");

                await mgr.SaveAsync(tmpFile);
                Assert.True(File.Exists(tmpFile));

                var loaded = await PromptSnapshotManager.LoadAsync(tmpFile);
                Assert.Equal(1, loaded.Count);
                Assert.True(loaded.Contains("file-test"));
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        [Fact]
        public async Task SaveAsync_EmptyPath_Throws()
        {
            var mgr = new PromptSnapshotManager();
            await Assert.ThrowsAsync<ArgumentException>(() => mgr.SaveAsync(""));
        }

        [Fact]
        public async Task LoadAsync_FileNotFound_Throws()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                PromptSnapshotManager.LoadAsync("nonexistent-file.json"));
        }

        [Fact]
        public void ContentHash_ConsistentForSameLibrary()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            var snap1 = mgr.TakeSnapshot(lib, "a");
            var snap2 = mgr.TakeSnapshot(lib, "b");
            Assert.Equal(snap1.ContentHash, snap2.ContentHash);
        }

        [Fact]
        public void ContentHash_DiffersForDifferentLibrary()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            var snap1 = mgr.TakeSnapshot(lib, "a");

            lib.Add("extra", new PromptTemplate("{{x}}"));
            var snap2 = mgr.TakeSnapshot(lib, "b");

            Assert.NotEqual(snap1.ContentHash, snap2.ContentHash);
        }

        [Fact]
        public void Compare_MultipleChanges()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            // Remove one, modify one, add two
            lib.Remove("greet");
            lib.Update("summarize", template: new PromptTemplate("New: {{text}}"));
            lib.Add("a", new PromptTemplate("{{x}}"));
            lib.Add("b", new PromptTemplate("{{y}}"));
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            Assert.Equal(1, diff.RemovedCount);
            Assert.Equal(2, diff.AddedCount);
            Assert.True(diff.ModifiedCount >= 1);
            Assert.False(diff.AreIdentical);
        }

        [Fact]
        public void Compare_TagsChanged()
        {
            var lib = CreateTestLibrary();
            var mgr = new PromptSnapshotManager();
            mgr.TakeSnapshot(lib, "before");

            lib.Update("greet", tags: new[] { "new-tag" });
            mgr.TakeSnapshot(lib, "after");

            var diff = mgr.Compare("before", "after");
            Assert.True(diff.Diffs.Any(d => d.DiffType == SnapshotDiffType.MetadataChanged && d.EntryName == "greet"));
        }
    }
}
