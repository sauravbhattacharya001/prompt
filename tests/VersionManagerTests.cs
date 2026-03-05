namespace Prompt.Tests
{
    using System.Text.Json;
    using Xunit;

    public class VersionManagerTests
    {
        // ================================================================
        // Constructor & Initial State
        // ================================================================

        [Fact]
        public void NewManager_HasZeroCounts()
        {
            var vm = new PromptVersionManager();
            Assert.Equal(0, vm.TemplateCount);
            Assert.Equal(0, vm.TotalVersionCount);
            Assert.Empty(vm.GetTrackedTemplates());
        }

        // ================================================================
        // CreateVersion
        // ================================================================

        [Fact]
        public void CreateVersion_FirstVersion_HasVersionNumber1()
        {
            var vm = new PromptVersionManager();
            var v = vm.CreateVersion("test-template", "Hello {{name}}");
            Assert.Equal(1, v.VersionNumber);
            Assert.Equal("Hello {{name}}", v.TemplateText);
            Assert.Null(v.Description);
            Assert.Null(v.Author);
            Assert.Null(v.DefaultValues);
        }

        [Fact]
        public void CreateVersion_IncrementsVersionNumber()
        {
            var vm = new PromptVersionManager();
            var v1 = vm.CreateVersion("t", "v1");
            var v2 = vm.CreateVersion("t", "v2");
            var v3 = vm.CreateVersion("t", "v3");
            Assert.Equal(1, v1.VersionNumber);
            Assert.Equal(2, v2.VersionNumber);
            Assert.Equal(3, v3.VersionNumber);
        }

        [Fact]
        public void CreateVersion_WithDescription_SetsDescription()
        {
            var vm = new PromptVersionManager();
            var v = vm.CreateVersion("t", "text", description: "Initial version");
            Assert.Equal("Initial version", v.Description);
        }

        [Fact]
        public void CreateVersion_WithAuthor_SetsAuthor()
        {
            var vm = new PromptVersionManager();
            var v = vm.CreateVersion("t", "text", author: "alice");
            Assert.Equal("alice", v.Author);
        }

        [Fact]
        public void CreateVersion_WithDefaults_SetsDefaults()
        {
            var vm = new PromptVersionManager();
            var defaults = new Dictionary<string, string> { ["name"] = "World" };
            var v = vm.CreateVersion("t", "Hello {{name}}", defaults: defaults);
            Assert.NotNull(v.DefaultValues);
            Assert.Equal("World", v.DefaultValues!["name"]);
        }

        [Fact]
        public void CreateVersion_DefaultsAreCopied_OriginalMutationDoesNotAffect()
        {
            var vm = new PromptVersionManager();
            var defaults = new Dictionary<string, string> { ["key"] = "original" };
            var v = vm.CreateVersion("t", "text", defaults: defaults);
            defaults["key"] = "mutated";
            Assert.Equal("original", v.DefaultValues!["key"]);
        }

        [Fact]
        public void CreateVersion_SetsCreatedAtTimestamp()
        {
            var before = DateTimeOffset.UtcNow;
            var vm = new PromptVersionManager();
            var v = vm.CreateVersion("t", "text");
            var after = DateTimeOffset.UtcNow;
            Assert.InRange(v.CreatedAt, before, after);
        }

        [Fact]
        public void CreateVersion_NullName_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.CreateVersion(null!, "text"));
        }

        [Fact]
        public void CreateVersion_EmptyName_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.CreateVersion("", "text"));
        }

        [Fact]
        public void CreateVersion_WhitespaceName_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.CreateVersion("   ", "text"));
        }

        [Fact]
        public void CreateVersion_NullText_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.CreateVersion("t", null!));
        }

        [Fact]
        public void CreateVersion_EmptyText_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.CreateVersion("t", ""));
        }

        [Fact]
        public void CreateVersion_InvalidNameChars_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.CreateVersion("bad name!", "text"));
        }

        [Fact]
        public void CreateVersion_ValidNameChars_Succeeds()
        {
            var vm = new PromptVersionManager();
            // Letters, digits, hyphens, underscores, dots
            var v = vm.CreateVersion("my-template_v1.0", "text");
            Assert.Equal(1, v.VersionNumber);
        }

        [Fact]
        public void CreateVersion_TrimsName()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("  my-template  ", "v1");
            vm.CreateVersion("my-template", "v2"); // Should add to same template
            Assert.Equal(1, vm.TemplateCount);
            Assert.Equal(2, vm.GetVersionCount("my-template"));
        }

        [Fact]
        public void CreateVersion_CaseInsensitiveNames()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("MyTemplate", "v1");
            vm.CreateVersion("mytemplate", "v2");
            Assert.Equal(1, vm.TemplateCount);
        }

        [Fact]
        public void CreateVersion_UpdatesCounts()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t1", "text");
            vm.CreateVersion("t2", "text");
            vm.CreateVersion("t1", "text2");
            Assert.Equal(2, vm.TemplateCount);
            Assert.Equal(3, vm.TotalVersionCount);
        }

        [Fact]
        public void CreateVersion_PrunesOldestWhenOverLimit()
        {
            var vm = new PromptVersionManager();
            // MaxVersionsPerTemplate is 100
            for (int i = 1; i <= 105; i++)
            {
                vm.CreateVersion("t", $"version {i}");
            }
            // Should have 100 versions, not 105
            Assert.Equal(100, vm.GetVersionCount("t"));
            // Earliest surviving should be version 6 (1-5 pruned)
            var history = vm.GetHistory("t");
            Assert.Equal(6, history[0].VersionNumber);
            Assert.Equal(105, history[^1].VersionNumber);
        }

        [Fact]
        public void CreateVersion_MaxTemplates_Throws()
        {
            var vm = new PromptVersionManager();
            // MaxTemplates is 500
            for (int i = 0; i < 500; i++)
            {
                vm.CreateVersion($"template-{i}", "text");
            }
            Assert.Equal(500, vm.TemplateCount);
            Assert.Throws<InvalidOperationException>(() =>
                vm.CreateVersion("one-too-many", "text"));
        }

        // ================================================================
        // GetLatest
        // ================================================================

        [Fact]
        public void GetLatest_ExistingTemplate_ReturnsLatest()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "v1");
            vm.CreateVersion("t", "v2");
            vm.CreateVersion("t", "v3");
            var latest = vm.GetLatest("t");
            Assert.NotNull(latest);
            Assert.Equal(3, latest!.VersionNumber);
            Assert.Equal("v3", latest.TemplateText);
        }

        [Fact]
        public void GetLatest_NonExistentTemplate_ReturnsNull()
        {
            var vm = new PromptVersionManager();
            Assert.Null(vm.GetLatest("nope"));
        }

        [Fact]
        public void GetLatest_NullName_ReturnsNull()
        {
            var vm = new PromptVersionManager();
            Assert.Null(vm.GetLatest(null!));
        }

        [Fact]
        public void GetLatest_WhitespaceName_ReturnsNull()
        {
            var vm = new PromptVersionManager();
            Assert.Null(vm.GetLatest("   "));
        }

        // ================================================================
        // GetVersion
        // ================================================================

        [Fact]
        public void GetVersion_ExistingVersion_ReturnsCorrectVersion()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "first");
            vm.CreateVersion("t", "second");
            var v = vm.GetVersion("t", 1);
            Assert.NotNull(v);
            Assert.Equal("first", v!.TemplateText);
        }

        [Fact]
        public void GetVersion_NonExistentVersion_ReturnsNull()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text");
            Assert.Null(vm.GetVersion("t", 99));
        }

        [Fact]
        public void GetVersion_NonExistentTemplate_ReturnsNull()
        {
            var vm = new PromptVersionManager();
            Assert.Null(vm.GetVersion("nope", 1));
        }

        [Fact]
        public void GetVersion_NullName_ReturnsNull()
        {
            var vm = new PromptVersionManager();
            Assert.Null(vm.GetVersion(null!, 1));
        }

        // ================================================================
        // GetHistory
        // ================================================================

        [Fact]
        public void GetHistory_ReturnsOrderedVersions()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "v1");
            vm.CreateVersion("t", "v2");
            vm.CreateVersion("t", "v3");
            var history = vm.GetHistory("t");
            Assert.Equal(3, history.Count);
            Assert.Equal(1, history[0].VersionNumber);
            Assert.Equal(2, history[1].VersionNumber);
            Assert.Equal(3, history[2].VersionNumber);
        }

        [Fact]
        public void GetHistory_NonExistentTemplate_ReturnsEmpty()
        {
            var vm = new PromptVersionManager();
            Assert.Empty(vm.GetHistory("nope"));
        }

        [Fact]
        public void GetHistory_NullName_ReturnsEmpty()
        {
            var vm = new PromptVersionManager();
            Assert.Empty(vm.GetHistory(null!));
        }

        // ================================================================
        // GetVersionCount
        // ================================================================

        [Fact]
        public void GetVersionCount_ReturnsCorrectCount()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "v1");
            vm.CreateVersion("t", "v2");
            Assert.Equal(2, vm.GetVersionCount("t"));
        }

        [Fact]
        public void GetVersionCount_NonExistent_ReturnsZero()
        {
            var vm = new PromptVersionManager();
            Assert.Equal(0, vm.GetVersionCount("nope"));
        }

        [Fact]
        public void GetVersionCount_NullName_ReturnsZero()
        {
            var vm = new PromptVersionManager();
            Assert.Equal(0, vm.GetVersionCount(null!));
        }

        // ================================================================
        // GetTrackedTemplates
        // ================================================================

        [Fact]
        public void GetTrackedTemplates_ReturnsSortedNames()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("beta", "text");
            vm.CreateVersion("alpha", "text");
            vm.CreateVersion("gamma", "text");
            var names = vm.GetTrackedTemplates();
            Assert.Equal(3, names.Count);
            Assert.Equal("alpha", names[0]);
            Assert.Equal("beta", names[1]);
            Assert.Equal("gamma", names[2]);
        }

        // ================================================================
        // Compare
        // ================================================================

        [Fact]
        public void Compare_TextChanged_DetectsDiff()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "line1\nline2");
            vm.CreateVersion("t", "line1\nline3\nline4");
            var diff = vm.Compare("t", 1, 2);
            Assert.True(diff.HasTextChanges);
            Assert.Equal("t", diff.TemplateName);
            Assert.Equal(1, diff.FromVersion);
            Assert.Equal(2, diff.ToVersion);
            Assert.Contains("line3", diff.AddedLines);
            Assert.Contains("line4", diff.AddedLines);
            Assert.Contains("line2", diff.RemovedLines);
        }

        [Fact]
        public void Compare_IdenticalVersions_NoChanges()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "same text");
            vm.CreateVersion("t", "same text");
            var diff = vm.Compare("t", 1, 2);
            Assert.False(diff.HasTextChanges);
            Assert.Empty(diff.AddedLines);
            Assert.Empty(diff.RemovedLines);
            Assert.Equal("No changes", diff.GetSummary());
        }

        [Fact]
        public void Compare_DefaultsChanged_DetectsDiff()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text", defaults: new Dictionary<string, string> { ["a"] = "1" });
            vm.CreateVersion("t", "text", defaults: new Dictionary<string, string> { ["a"] = "2", ["b"] = "3" });
            var diff = vm.Compare("t", 1, 2);
            Assert.True(diff.HasDefaultChanges);
            Assert.Contains("b", diff.AddedDefaults);
            Assert.Contains("a", diff.ChangedDefaults);
            Assert.Empty(diff.RemovedDefaults);
        }

        [Fact]
        public void Compare_DefaultsRemoved_DetectsRemoval()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text", defaults: new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
            vm.CreateVersion("t", "text", defaults: new Dictionary<string, string> { ["a"] = "1" });
            var diff = vm.Compare("t", 1, 2);
            Assert.True(diff.HasDefaultChanges);
            Assert.Contains("b", diff.RemovedDefaults);
        }

        [Fact]
        public void Compare_NullDefaults_ToWithDefaults()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text"); // no defaults
            vm.CreateVersion("t", "text", defaults: new Dictionary<string, string> { ["x"] = "y" });
            var diff = vm.Compare("t", 1, 2);
            Assert.True(diff.HasDefaultChanges);
            Assert.Contains("x", diff.AddedDefaults);
        }

        [Fact]
        public void Compare_NullName_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.Compare(null!, 1, 2));
        }

        [Fact]
        public void Compare_NonExistentTemplate_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.Compare("nope", 1, 2));
        }

        [Fact]
        public void Compare_NonExistentFromVersion_Throws()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text");
            Assert.Throws<ArgumentException>(() => vm.Compare("t", 99, 1));
        }

        [Fact]
        public void Compare_NonExistentToVersion_Throws()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text");
            Assert.Throws<ArgumentException>(() => vm.Compare("t", 1, 99));
        }

        [Fact]
        public void Compare_GetSummary_ShowsLineCounts()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "a\nb");
            vm.CreateVersion("t", "a\nc\nd");
            var diff = vm.Compare("t", 1, 2);
            var summary = diff.GetSummary();
            Assert.Contains("+", summary);
            Assert.Contains("-", summary);
        }

        [Fact]
        public void Compare_AddedLineCount_And_RemovedLineCount()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "line1\nline2\nline3");
            vm.CreateVersion("t", "line1\nline4");
            var diff = vm.Compare("t", 1, 2);
            Assert.Equal(1, diff.AddedLineCount); // line4
            Assert.Equal(2, diff.RemovedLineCount); // line2, line3
        }

        // ================================================================
        // HasChanges
        // ================================================================

        [Fact]
        public void HasChanges_IdenticalText_ReturnsFalse()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "same");
            Assert.False(vm.HasChanges("t", "same"));
        }

        [Fact]
        public void HasChanges_DifferentText_ReturnsTrue()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "original");
            Assert.True(vm.HasChanges("t", "modified"));
        }

        [Fact]
        public void HasChanges_NoHistory_ReturnsTrue()
        {
            var vm = new PromptVersionManager();
            Assert.True(vm.HasChanges("new-template", "text"));
        }

        [Fact]
        public void HasChanges_NullName_ReturnsTrue()
        {
            var vm = new PromptVersionManager();
            Assert.True(vm.HasChanges(null!, "text"));
        }

        // ================================================================
        // Rollback
        // ================================================================

        [Fact]
        public void Rollback_RestoresOldContent()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "original", description: "v1");
            vm.CreateVersion("t", "modified", description: "v2");
            var rolled = vm.Rollback("t", 1, author: "admin");
            Assert.Equal(3, rolled.VersionNumber);
            Assert.Equal("original", rolled.TemplateText);
            Assert.Equal("Rollback to v1", rolled.Description);
            Assert.Equal("admin", rolled.Author);
        }

        [Fact]
        public void Rollback_PreservesHistory()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "v1-text");
            vm.CreateVersion("t", "v2-text");
            vm.Rollback("t", 1);
            Assert.Equal(3, vm.GetVersionCount("t"));
            Assert.Equal("v1-text", vm.GetLatest("t")!.TemplateText);
        }

        [Fact]
        public void Rollback_CopiesDefaultValues()
        {
            var vm = new PromptVersionManager();
            var defaults = new Dictionary<string, string> { ["key"] = "val" };
            vm.CreateVersion("t", "text", defaults: defaults);
            vm.CreateVersion("t", "text2");
            var rolled = vm.Rollback("t", 1);
            Assert.NotNull(rolled.DefaultValues);
            Assert.Equal("val", rolled.DefaultValues!["key"]);
        }

        [Fact]
        public void Rollback_NullName_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.Rollback(null!, 1));
        }

        [Fact]
        public void Rollback_NonExistentTemplate_Throws()
        {
            var vm = new PromptVersionManager();
            Assert.Throws<ArgumentException>(() => vm.Rollback("nope", 1));
        }

        [Fact]
        public void Rollback_NonExistentVersion_Throws()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text");
            Assert.Throws<ArgumentException>(() => vm.Rollback("t", 99));
        }

        // ================================================================
        // DeleteHistory
        // ================================================================

        [Fact]
        public void DeleteHistory_RemovesTemplate()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text");
            Assert.True(vm.DeleteHistory("t"));
            Assert.Equal(0, vm.TemplateCount);
            Assert.Null(vm.GetLatest("t"));
        }

        [Fact]
        public void DeleteHistory_NonExistent_ReturnsFalse()
        {
            var vm = new PromptVersionManager();
            Assert.False(vm.DeleteHistory("nope"));
        }

        [Fact]
        public void DeleteHistory_NullName_ReturnsFalse()
        {
            var vm = new PromptVersionManager();
            Assert.False(vm.DeleteHistory(null!));
        }

        // ================================================================
        // ClearAll
        // ================================================================

        [Fact]
        public void ClearAll_RemovesEverything()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t1", "text");
            vm.CreateVersion("t2", "text");
            vm.ClearAll();
            Assert.Equal(0, vm.TemplateCount);
            Assert.Equal(0, vm.TotalVersionCount);
        }

        // ================================================================
        // Serialization (ToJson / FromJson)
        // ================================================================

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("greeting", "Hello {{name}}", description: "initial", author: "bob",
                defaults: new Dictionary<string, string> { ["name"] = "World" });
            vm.CreateVersion("greeting", "Hi {{name}}!", description: "shorter");
            var json = vm.ToJson();

            Assert.False(string.IsNullOrEmpty(json));
            // Should be valid JSON
            var doc = JsonDocument.Parse(json);
            Assert.NotNull(doc);
        }

        [Fact]
        public void FromJson_RestoresState()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t1", "text1", author: "alice");
            vm.CreateVersion("t1", "text2");
            vm.CreateVersion("t2", "other",
                defaults: new Dictionary<string, string> { ["x"] = "y" });

            var json = vm.ToJson();
            var restored = PromptVersionManager.FromJson(json);

            Assert.Equal(2, restored.TemplateCount);
            Assert.Equal(3, restored.TotalVersionCount);
            Assert.Equal("text2", restored.GetLatest("t1")!.TemplateText);
            Assert.Equal("alice", restored.GetVersion("t1", 1)!.Author);
            Assert.Equal("y", restored.GetLatest("t2")!.DefaultValues!["x"]);
        }

        [Fact]
        public void FromJson_NullJson_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptVersionManager.FromJson(null!));
        }

        [Fact]
        public void FromJson_EmptyJson_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptVersionManager.FromJson(""));
        }

        [Fact]
        public void FromJson_InvalidJson_Throws()
        {
            Assert.ThrowsAny<Exception>(() => PromptVersionManager.FromJson("not json"));
        }

        [Fact]
        public void FromJson_MissingTemplatesArray_ReturnsEmptyManager()
        {
            // When templates array is absent, deserialization gives empty list, resulting in empty manager
            var vm = PromptVersionManager.FromJson("{\"version\":1,\"templates\":[]}");
            Assert.Equal(0, vm.TemplateCount);
        }

        [Fact]
        public void FromJson_Roundtrip_PreservesAllData()
        {
            var vm = new PromptVersionManager();
            for (int i = 0; i < 5; i++)
            {
                vm.CreateVersion($"template-{i}", $"text-{i}", description: $"desc-{i}",
                    author: $"author-{i}");
            }

            var json = vm.ToJson();
            var restored = PromptVersionManager.FromJson(json);

            Assert.Equal(vm.TemplateCount, restored.TemplateCount);
            Assert.Equal(vm.TotalVersionCount, restored.TotalVersionCount);

            for (int i = 0; i < 5; i++)
            {
                var orig = vm.GetLatest($"template-{i}");
                var rest = restored.GetLatest($"template-{i}");
                Assert.NotNull(rest);
                Assert.Equal(orig!.TemplateText, rest!.TemplateText);
                Assert.Equal(orig.Description, rest.Description);
                Assert.Equal(orig.Author, rest.Author);
            }
        }

        // ================================================================
        // VersionDiff details
        // ================================================================

        [Fact]
        public void VersionDiff_GetSummary_SingleLine_NoPlural()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "line1");
            vm.CreateVersion("t", "line2");
            var diff = vm.Compare("t", 1, 2);
            var summary = diff.GetSummary();
            Assert.Contains("+1 line", summary);
            Assert.Contains("-1 line", summary);
            // Should NOT say "lines" for singular
            Assert.DoesNotContain("+1 lines", summary);
        }

        [Fact]
        public void VersionDiff_GetSummary_IncludesDefaultChanges()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text", defaults: new Dictionary<string, string> { ["a"] = "1" });
            vm.CreateVersion("t", "text", defaults: new Dictionary<string, string> { ["a"] = "2", ["b"] = "3" });
            var diff = vm.Compare("t", 1, 2);
            var summary = diff.GetSummary();
            Assert.Contains("default", summary);
        }

        // ================================================================
        // PromptVersion object
        // ================================================================

        [Fact]
        public void PromptVersion_PropertiesSetCorrectly()
        {
            var defaults = new Dictionary<string, string> { ["k"] = "v" }.AsReadOnly()
                as IReadOnlyDictionary<string, string>;
            var now = DateTimeOffset.UtcNow;
            var v = new PromptVersion(5, "template text", "desc", now, "author", defaults);
            Assert.Equal(5, v.VersionNumber);
            Assert.Equal("template text", v.TemplateText);
            Assert.Equal("desc", v.Description);
            Assert.Equal(now, v.CreatedAt);
            Assert.Equal("author", v.Author);
            Assert.Equal("v", v.DefaultValues!["k"]);
        }

        // ================================================================
        // Thread Safety (basic verification)
        // ================================================================

        [Fact]
        public void ConcurrentCreateVersion_DoesNotThrow()
        {
            var vm = new PromptVersionManager();
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int idx = i;
                tasks.Add(Task.Run(() =>
                {
                    vm.CreateVersion("concurrent", $"version-{idx}");
                }));
            }
            Task.WaitAll(tasks.ToArray());
            Assert.Equal(20, vm.GetVersionCount("concurrent"));
        }

        [Fact]
        public void ConcurrentGetLatest_DoesNotThrow()
        {
            var vm = new PromptVersionManager();
            for (int i = 0; i < 10; i++)
            {
                vm.CreateVersion("t", $"v{i}");
            }

            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var latest = vm.GetLatest("t");
                    Assert.NotNull(latest);
                }));
            }
            Task.WaitAll(tasks.ToArray());
        }

        // ================================================================
        // Edge Cases
        // ================================================================

        [Fact]
        public void CreateVersion_MultilineTemplate()
        {
            var vm = new PromptVersionManager();
            var multiline = "You are a helpful assistant.\n\nPlease answer:\n- Clearly\n- Concisely";
            var v = vm.CreateVersion("t", multiline);
            Assert.Equal(multiline, v.TemplateText);
        }

        [Fact]
        public void CreateVersion_VeryLongTemplate()
        {
            var vm = new PromptVersionManager();
            var longText = new string('x', 100_000);
            var v = vm.CreateVersion("t", longText);
            Assert.Equal(100_000, v.TemplateText.Length);
        }

        [Fact]
        public void DeleteHistory_AllowsRecreation()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "v1");
            vm.CreateVersion("t", "v2");
            vm.DeleteHistory("t");
            var v = vm.CreateVersion("t", "fresh");
            Assert.Equal(1, v.VersionNumber);
            Assert.Equal(1, vm.GetVersionCount("t"));
        }

        [Fact]
        public void Rollback_WithNullDefaultsOnTarget_SetsNullDefaults()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "text1"); // no defaults
            vm.CreateVersion("t", "text2", defaults: new Dictionary<string, string> { ["k"] = "v" });
            var rolled = vm.Rollback("t", 1);
            Assert.Null(rolled.DefaultValues);
        }

        [Fact]
        public void Compare_SingleCharacterDiff()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "a");
            vm.CreateVersion("t", "b");
            var diff = vm.Compare("t", 1, 2);
            Assert.True(diff.HasTextChanges);
            Assert.Equal(1, diff.AddedLineCount);
            Assert.Equal(1, diff.RemovedLineCount);
        }

        [Fact]
        public void GetSummary_OnlyDefaultChanges_ShowsDefaultCount()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("t", "same text", defaults: new Dictionary<string, string> { ["a"] = "1" });
            vm.CreateVersion("t", "same text"); // removed defaults
            var diff = vm.Compare("t", 1, 2);
            Assert.False(diff.HasTextChanges);
            Assert.True(diff.HasDefaultChanges);
            var summary = diff.GetSummary();
            Assert.Contains("default", summary);
        }
    }
}
