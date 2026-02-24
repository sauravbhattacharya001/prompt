namespace Prompt.Tests
{
    using System.Text.Json;
    using Xunit;

    // =================================================================
    // PromptVersion Tests
    // =================================================================
    public class PromptVersionTests
    {
        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var defaults = new Dictionary<string, string> { ["key"] = "val" };
            var created = DateTimeOffset.UtcNow;

            var v = new PromptVersion(3, "Hello {{name}}", "initial",
                created, "alice", defaults.AsReadOnly());

            Assert.Equal(3, v.VersionNumber);
            Assert.Equal("Hello {{name}}", v.TemplateText);
            Assert.Equal("initial", v.Description);
            Assert.Equal(created, v.CreatedAt);
            Assert.Equal("alice", v.Author);
            Assert.NotNull(v.DefaultValues);
            Assert.Equal("val", v.DefaultValues!["key"]);
        }

        [Fact]
        public void Constructor_NullDefaults_DefaultValuesIsNull()
        {
            var v = new PromptVersion(1, "text");
            Assert.Null(v.DefaultValues);
        }

        [Fact]
        public void Constructor_NullDescription_IsNull()
        {
            var v = new PromptVersion(1, "text");
            Assert.Null(v.Description);
        }

        [Fact]
        public void Constructor_NullAuthor_IsNull()
        {
            var v = new PromptVersion(1, "text");
            Assert.Null(v.Author);
        }

        [Fact]
        public void DefaultValues_IsCopy_NotReference()
        {
            var original = new Dictionary<string, string> { ["a"] = "1" };
            var v = new PromptVersion(1, "text", defaultValues: original.AsReadOnly());

            original["a"] = "changed";
            Assert.Equal("1", v.DefaultValues!["a"]);
        }

        [Fact]
        public void Constructor_DefaultCreatedAt_IsSet()
        {
            var before = DateTimeOffset.UtcNow;
            var v = new PromptVersion(1, "text");
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(v.CreatedAt, before, after);
        }
    }

    // =================================================================
    // CreateVersion Tests
    // =================================================================
    public class CreateVersionTests
    {
        [Fact]
        public void CreateVersion_BasicCreation()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("my-template", "Hello world");

            Assert.Equal(1, v.VersionNumber);
            Assert.Equal("Hello world", v.TemplateText);
        }

        [Fact]
        public void CreateVersion_AutoIncrementsVersionNumber()
        {
            var mgr = new PromptVersionManager();
            var v1 = mgr.CreateVersion("t", "v1");
            var v2 = mgr.CreateVersion("t", "v2");
            var v3 = mgr.CreateVersion("t", "v3");

            Assert.Equal(1, v1.VersionNumber);
            Assert.Equal(2, v2.VersionNumber);
            Assert.Equal(3, v3.VersionNumber);
        }

        [Fact]
        public void CreateVersion_SetsDescription()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("t", "text", description: "my change");

            Assert.Equal("my change", v.Description);
        }

        [Fact]
        public void CreateVersion_SetsAuthor()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("t", "text", author: "bob");

            Assert.Equal("bob", v.Author);
        }

        [Fact]
        public void CreateVersion_SetsDefaults()
        {
            var mgr = new PromptVersionManager();
            var defaults = new Dictionary<string, string> { ["lang"] = "en" };
            var v = mgr.CreateVersion("t", "text", defaults: defaults);

            Assert.NotNull(v.DefaultValues);
            Assert.Equal("en", v.DefaultValues!["lang"]);
        }

        [Fact]
        public void CreateVersion_DefaultsCopied()
        {
            var mgr = new PromptVersionManager();
            var defaults = new Dictionary<string, string> { ["k"] = "v" };
            var v = mgr.CreateVersion("t", "text", defaults: defaults);

            defaults["k"] = "changed";
            Assert.Equal("v", v.DefaultValues!["k"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateVersion_EmptyName_Throws(string? name)
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(
                () => mgr.CreateVersion(name!, "text"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CreateVersion_NullOrEmptyText_Throws(string? text)
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(
                () => mgr.CreateVersion("t", text!));
        }

        [Theory]
        [InlineData("has space")]
        [InlineData("has@symbol")]
        [InlineData("has!bang")]
        [InlineData("has/slash")]
        public void CreateVersion_InvalidNameChars_Throws(string name)
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(
                () => mgr.CreateVersion(name, "text"));
        }

        [Fact]
        public void CreateVersion_ValidNameChars_Succeeds()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("my-template_v2.0", "text");
            Assert.Equal(1, v.VersionNumber);
        }

        [Fact]
        public void CreateVersion_MultipleTemplates()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("a", "text-a");
            mgr.CreateVersion("b", "text-b");

            Assert.Equal(2, mgr.TemplateCount);
        }

        [Fact]
        public void CreateVersion_SetsCreatedAt()
        {
            var mgr = new PromptVersionManager();
            var before = DateTimeOffset.UtcNow;
            var v = mgr.CreateVersion("t", "text");
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(v.CreatedAt, before, after);
        }
    }

    // =================================================================
    // GetLatest Tests
    // =================================================================
    public class GetLatestTests
    {
        [Fact]
        public void GetLatest_ReturnsMostRecent()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");
            var v3 = mgr.CreateVersion("t", "v3");

            var latest = mgr.GetLatest("t");
            Assert.NotNull(latest);
            Assert.Equal(3, latest!.VersionNumber);
            Assert.Equal("v3", latest.TemplateText);
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
    }

    // =================================================================
    // GetVersion Tests
    // =================================================================
    public class GetVersionTests
    {
        [Fact]
        public void GetVersion_ReturnsCorrectVersion()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");

            var v = mgr.GetVersion("t", 1);
            Assert.NotNull(v);
            Assert.Equal("v1", v!.TemplateText);
        }

        [Fact]
        public void GetVersion_WrongNumber_ReturnsNull()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");

            Assert.Null(mgr.GetVersion("t", 99));
        }

        [Fact]
        public void GetVersion_UnknownTemplate_ReturnsNull()
        {
            var mgr = new PromptVersionManager();
            Assert.Null(mgr.GetVersion("nope", 1));
        }
    }

    // =================================================================
    // GetHistory Tests
    // =================================================================
    public class GetHistoryTests
    {
        [Fact]
        public void GetHistory_ReturnsOrderedList()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");
            mgr.CreateVersion("t", "v3");

            var history = mgr.GetHistory("t");
            Assert.Equal(3, history.Count);
            Assert.Equal(1, history[0].VersionNumber);
            Assert.Equal(2, history[1].VersionNumber);
            Assert.Equal(3, history[2].VersionNumber);
        }

        [Fact]
        public void GetHistory_UnknownTemplate_ReturnsEmpty()
        {
            var mgr = new PromptVersionManager();
            var history = mgr.GetHistory("nonexistent");
            Assert.Empty(history);
        }

        [Fact]
        public void GetHistory_NullName_ReturnsEmpty()
        {
            var mgr = new PromptVersionManager();
            Assert.Empty(mgr.GetHistory(null!));
        }
    }

    // =================================================================
    // GetVersionCount Tests
    // =================================================================
    public class GetVersionCountTests
    {
        [Fact]
        public void GetVersionCount_CorrectCount()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");

            Assert.Equal(2, mgr.GetVersionCount("t"));
        }

        [Fact]
        public void GetVersionCount_UnknownTemplate_Zero()
        {
            var mgr = new PromptVersionManager();
            Assert.Equal(0, mgr.GetVersionCount("nope"));
        }
    }

    // =================================================================
    // GetTrackedTemplates Tests
    // =================================================================
    public class GetTrackedTemplatesTests
    {
        [Fact]
        public void GetTrackedTemplates_ListsAllNames()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("beta", "text");
            mgr.CreateVersion("alpha", "text");
            mgr.CreateVersion("gamma", "text");

            var names = mgr.GetTrackedTemplates();
            Assert.Equal(3, names.Count);
            Assert.Contains("alpha", names);
            Assert.Contains("beta", names);
            Assert.Contains("gamma", names);
        }

        [Fact]
        public void GetTrackedTemplates_Empty_ReturnsEmpty()
        {
            var mgr = new PromptVersionManager();
            Assert.Empty(mgr.GetTrackedTemplates());
        }

        [Fact]
        public void GetTrackedTemplates_SortedAlphabetically()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("z-template", "text");
            mgr.CreateVersion("a-template", "text");
            mgr.CreateVersion("m-template", "text");

            var names = mgr.GetTrackedTemplates();
            Assert.Equal("a-template", names[0]);
            Assert.Equal("m-template", names[1]);
            Assert.Equal("z-template", names[2]);
        }
    }

    // =================================================================
    // Compare Tests
    // =================================================================
    public class CompareTests
    {
        [Fact]
        public void Compare_TextChangesDetected()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "Hello world");
            mgr.CreateVersion("t", "Hello universe");

            var diff = mgr.Compare("t", 1, 2);
            Assert.True(diff.HasTextChanges);
        }

        [Fact]
        public void Compare_NoChanges()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "Same text");
            mgr.CreateVersion("t", "Same text");

            var diff = mgr.Compare("t", 1, 2);
            Assert.False(diff.HasTextChanges);
            Assert.Empty(diff.AddedLines);
            Assert.Empty(diff.RemovedLines);
        }

        [Fact]
        public void Compare_AddedLines()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "line1");
            mgr.CreateVersion("t", "line1\nline2\nline3");

            var diff = mgr.Compare("t", 1, 2);
            Assert.Contains("line2", diff.AddedLines);
            Assert.Contains("line3", diff.AddedLines);
            Assert.Equal(2, diff.AddedLineCount);
        }

        [Fact]
        public void Compare_RemovedLines()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "line1\nline2\nline3");
            mgr.CreateVersion("t", "line1");

            var diff = mgr.Compare("t", 1, 2);
            Assert.Contains("line2", diff.RemovedLines);
            Assert.Contains("line3", diff.RemovedLines);
            Assert.Equal(2, diff.RemovedLineCount);
        }

        [Fact]
        public void Compare_DefaultValueChanges()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text", defaults:
                new Dictionary<string, string> { ["key"] = "old" });
            mgr.CreateVersion("t", "text", defaults:
                new Dictionary<string, string> { ["key"] = "new" });

            var diff = mgr.Compare("t", 1, 2);
            Assert.True(diff.HasDefaultChanges);
            Assert.Contains("key", diff.ChangedDefaults);
        }

        [Fact]
        public void Compare_AddedDefaults()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");
            mgr.CreateVersion("t", "text", defaults:
                new Dictionary<string, string> { ["newKey"] = "val" });

            var diff = mgr.Compare("t", 1, 2);
            Assert.True(diff.HasDefaultChanges);
            Assert.Contains("newKey", diff.AddedDefaults);
        }

        [Fact]
        public void Compare_RemovedDefaults()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text", defaults:
                new Dictionary<string, string> { ["oldKey"] = "val" });
            mgr.CreateVersion("t", "text");

            var diff = mgr.Compare("t", 1, 2);
            Assert.True(diff.HasDefaultChanges);
            Assert.Contains("oldKey", diff.RemovedDefaults);
        }

        [Fact]
        public void Compare_SameVersion_NoChanges()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");

            var diff = mgr.Compare("t", 1, 1);
            Assert.False(diff.HasTextChanges);
            Assert.False(diff.HasDefaultChanges);
            Assert.Empty(diff.AddedLines);
            Assert.Empty(diff.RemovedLines);
        }

        [Fact]
        public void Compare_InvalidFromVersion_Throws()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");

            Assert.Throws<ArgumentException>(
                () => mgr.Compare("t", 99, 1));
        }

        [Fact]
        public void Compare_InvalidToVersion_Throws()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");

            Assert.Throws<ArgumentException>(
                () => mgr.Compare("t", 1, 99));
        }

        [Fact]
        public void Compare_UnknownTemplate_Throws()
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(
                () => mgr.Compare("nope", 1, 2));
        }

        [Fact]
        public void Compare_GetSummary_Format()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "line1\nline2",
                defaults: new Dictionary<string, string> { ["a"] = "1" });
            mgr.CreateVersion("t", "line1\nline2\nline3\nline4\nline5",
                defaults: new Dictionary<string, string>
                    { ["a"] = "2", ["b"] = "3" });

            var diff = mgr.Compare("t", 1, 2);
            var summary = diff.GetSummary();

            Assert.Contains("+", summary);
            Assert.Contains("line", summary);
            Assert.Contains("default", summary);
        }

        [Fact]
        public void Compare_GetSummary_NoChanges_ReturnsNoChanges()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "same");
            mgr.CreateVersion("t", "same");

            var diff = mgr.Compare("t", 1, 2);
            Assert.Equal("No changes", diff.GetSummary());
        }

        [Fact]
        public void Compare_EmptyName_Throws()
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(
                () => mgr.Compare("", 1, 2));
        }

        [Fact]
        public void Compare_PropertiesSet()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("my-tmpl", "v1");
            mgr.CreateVersion("my-tmpl", "v2");

            var diff = mgr.Compare("my-tmpl", 1, 2);
            Assert.Equal("my-tmpl", diff.TemplateName);
            Assert.Equal(1, diff.FromVersion);
            Assert.Equal(2, diff.ToVersion);
        }
    }

    // =================================================================
    // HasChanges Tests
    // =================================================================
    public class HasChangesTests
    {
        [Fact]
        public void HasChanges_True_WhenTextDiffers()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "original");

            Assert.True(mgr.HasChanges("t", "modified"));
        }

        [Fact]
        public void HasChanges_False_WhenIdentical()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "same text");

            Assert.False(mgr.HasChanges("t", "same text"));
        }

        [Fact]
        public void HasChanges_True_WhenNoHistory()
        {
            var mgr = new PromptVersionManager();
            Assert.True(mgr.HasChanges("unknown", "any text"));
        }

        [Fact]
        public void HasChanges_True_WhenNullName()
        {
            var mgr = new PromptVersionManager();
            Assert.True(mgr.HasChanges(null!, "text"));
        }
    }

    // =================================================================
    // Rollback Tests
    // =================================================================
    public class RollbackTests
    {
        [Fact]
        public void Rollback_CreatesNewVersionWithOldContent()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1-text");
            mgr.CreateVersion("t", "v2-text");
            mgr.CreateVersion("t", "v3-text");

            var rolled = mgr.Rollback("t", 1);

            Assert.Equal(4, rolled.VersionNumber);
            Assert.Equal("v1-text", rolled.TemplateText);
        }

        [Fact]
        public void Rollback_PreservesHistory()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1-text");
            mgr.CreateVersion("t", "v2-text");
            mgr.Rollback("t", 1);

            // All 3 versions should exist
            Assert.Equal(3, mgr.GetVersionCount("t"));
            Assert.NotNull(mgr.GetVersion("t", 1));
            Assert.NotNull(mgr.GetVersion("t", 2));
            Assert.NotNull(mgr.GetVersion("t", 3));
        }

        [Fact]
        public void Rollback_DescriptionMentionsRollback()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1-text");
            mgr.CreateVersion("t", "v2-text");

            var rolled = mgr.Rollback("t", 1);
            Assert.Contains("Rollback", rolled.Description);
            Assert.Contains("v1", rolled.Description!);
        }

        [Fact]
        public void Rollback_SetsAuthor()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1-text");
            mgr.CreateVersion("t", "v2-text");

            var rolled = mgr.Rollback("t", 1, author: "admin");
            Assert.Equal("admin", rolled.Author);
        }

        [Fact]
        public void Rollback_InvalidTargetVersion_Throws()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1-text");

            Assert.Throws<ArgumentException>(
                () => mgr.Rollback("t", 99));
        }

        [Fact]
        public void Rollback_UnknownTemplate_Throws()
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(
                () => mgr.Rollback("nope", 1));
        }

        [Fact]
        public void Rollback_RestoresDefaults()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1", defaults:
                new Dictionary<string, string> { ["key"] = "val1" });
            mgr.CreateVersion("t", "v2", defaults:
                new Dictionary<string, string> { ["key"] = "val2" });

            var rolled = mgr.Rollback("t", 1);
            Assert.NotNull(rolled.DefaultValues);
            Assert.Equal("val1", rolled.DefaultValues!["key"]);
        }

        [Fact]
        public void Rollback_EmptyName_Throws()
        {
            var mgr = new PromptVersionManager();
            Assert.Throws<ArgumentException>(
                () => mgr.Rollback("", 1));
        }
    }

    // =================================================================
    // DeleteHistory Tests
    // =================================================================
    public class DeleteHistoryTests
    {
        [Fact]
        public void DeleteHistory_RemovesAllVersions()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");

            Assert.True(mgr.DeleteHistory("t"));
            Assert.Equal(0, mgr.GetVersionCount("t"));
            Assert.Equal(0, mgr.TemplateCount);
        }

        [Fact]
        public void DeleteHistory_UnknownTemplate_ReturnsFalse()
        {
            var mgr = new PromptVersionManager();
            Assert.False(mgr.DeleteHistory("nope"));
        }

        [Fact]
        public void DeleteHistory_NullName_ReturnsFalse()
        {
            var mgr = new PromptVersionManager();
            Assert.False(mgr.DeleteHistory(null!));
        }
    }

    // =================================================================
    // ClearAll Tests
    // =================================================================
    public class ClearAllTests
    {
        [Fact]
        public void ClearAll_EmptiesEverything()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("a", "text");
            mgr.CreateVersion("b", "text");
            mgr.CreateVersion("c", "text");

            mgr.ClearAll();

            Assert.Equal(0, mgr.TemplateCount);
            Assert.Equal(0, mgr.TotalVersionCount);
            Assert.Empty(mgr.GetTrackedTemplates());
        }
    }

    // =================================================================
    // Limits Tests
    // =================================================================
    public class VersionManagerLimitsTests
    {
        [Fact]
        public void MaxVersionsPerTemplate_PrunesOldest()
        {
            var mgr = new PromptVersionManager();

            for (int i = 1; i <= 105; i++)
                mgr.CreateVersion("t", $"version {i}");

            Assert.Equal(100, mgr.GetVersionCount("t"));
            // First 5 should have been pruned
            Assert.Null(mgr.GetVersion("t", 1));
            Assert.Null(mgr.GetVersion("t", 5));
            Assert.NotNull(mgr.GetVersion("t", 6));
            Assert.NotNull(mgr.GetVersion("t", 105));
        }

        [Fact]
        public void MaxTemplates_ThrowsWhenLimitReached()
        {
            var mgr = new PromptVersionManager();

            for (int i = 0; i < 500; i++)
                mgr.CreateVersion($"template-{i:D4}", "text");

            Assert.Throws<InvalidOperationException>(
                () => mgr.CreateVersion("one-too-many", "text"));
        }

        [Fact]
        public void MaxTemplates_ExistingTemplate_StillWorks()
        {
            var mgr = new PromptVersionManager();

            for (int i = 0; i < 500; i++)
                mgr.CreateVersion($"template-{i:D4}", "text");

            // Adding a version to an existing template should work
            var v = mgr.CreateVersion("template-0000", "updated text");
            Assert.Equal(2, v.VersionNumber);
        }
    }

    // =================================================================
    // Thread Safety Tests
    // =================================================================
    public class ThreadSafetyTests
    {
        [Fact]
        public void ConcurrentCreateVersion_DoesNotCrash()
        {
            var mgr = new PromptVersionManager();
            var tasks = new List<Task>();

            for (int i = 0; i < 50; i++)
            {
                int capture = i;
                tasks.Add(Task.Run(() =>
                    mgr.CreateVersion("shared", $"text-{capture}")));
            }

            Task.WaitAll(tasks.ToArray());

            Assert.Equal(50, mgr.GetVersionCount("shared"));
        }

        [Fact]
        public void ConcurrentCreateVersion_DifferentTemplates()
        {
            var mgr = new PromptVersionManager();
            var tasks = new List<Task>();

            for (int i = 0; i < 50; i++)
            {
                int capture = i;
                tasks.Add(Task.Run(() =>
                    mgr.CreateVersion($"template-{capture}", "text")));
            }

            Task.WaitAll(tasks.ToArray());

            Assert.Equal(50, mgr.TemplateCount);
        }
    }

    // =================================================================
    // JSON Serialization Tests
    // =================================================================
    public class VersionManagerJsonTests
    {
        [Fact]
        public void ToJson_FromJson_RoundTrip()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("template-a", "Hello {{name}}",
                "first version", "alice",
                new Dictionary<string, string> { ["name"] = "World" });
            mgr.CreateVersion("template-a", "Hi {{name}}!",
                "shorter greeting", "bob");
            mgr.CreateVersion("template-b", "Summarize: {{text}}");

            var json = mgr.ToJson();
            var restored = PromptVersionManager.FromJson(json);

            Assert.Equal(2, restored.TemplateCount);
            Assert.Equal(3, restored.TotalVersionCount);

            var v1 = restored.GetVersion("template-a", 1);
            Assert.NotNull(v1);
            Assert.Equal("Hello {{name}}", v1!.TemplateText);
            Assert.Equal("first version", v1.Description);
            Assert.Equal("alice", v1.Author);
            Assert.NotNull(v1.DefaultValues);
            Assert.Equal("World", v1.DefaultValues!["name"]);
        }

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");

            var json = mgr.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.NotNull(doc);
        }

        [Fact]
        public void FromJson_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PromptVersionManager.FromJson(null!));
            Assert.Throws<ArgumentException>(
                () => PromptVersionManager.FromJson(""));
            Assert.Throws<ArgumentException>(
                () => PromptVersionManager.FromJson("   "));
        }

        [Fact]
        public void FromJson_InvalidJson_Throws()
        {
            Assert.ThrowsAny<Exception>(
                () => PromptVersionManager.FromJson("not json"));
        }

        [Fact]
        public void ToJson_EmptyManager()
        {
            var mgr = new PromptVersionManager();
            var json = mgr.ToJson();
            var restored = PromptVersionManager.FromJson(json);

            Assert.Equal(0, restored.TemplateCount);
        }

        [Fact]
        public void RoundTrip_PreservesVersionNumbers()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");
            mgr.CreateVersion("t", "v3");

            var restored = PromptVersionManager.FromJson(mgr.ToJson());
            var history = restored.GetHistory("t");

            Assert.Equal(3, history.Count);
            Assert.Equal(1, history[0].VersionNumber);
            Assert.Equal(2, history[1].VersionNumber);
            Assert.Equal(3, history[2].VersionNumber);
        }

        [Fact]
        public void RoundTrip_PreservesCreatedAt()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("t", "text");
            var originalCreatedAt = v.CreatedAt;

            var restored = PromptVersionManager.FromJson(mgr.ToJson());
            var restoredV = restored.GetVersion("t", 1);

            Assert.Equal(originalCreatedAt, restoredV!.CreatedAt);
        }

        [Fact]
        public void RoundTrip_NullDefaults()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text");

            var restored = PromptVersionManager.FromJson(mgr.ToJson());
            var v = restored.GetVersion("t", 1);

            Assert.Null(v!.DefaultValues);
        }
    }

    // =================================================================
    // TemplateCount / TotalVersionCount Tests
    // =================================================================
    public class VersionManagerCountTests
    {
        [Fact]
        public void TemplateCount_CorrectAfterAdds()
        {
            var mgr = new PromptVersionManager();
            Assert.Equal(0, mgr.TemplateCount);

            mgr.CreateVersion("a", "text");
            Assert.Equal(1, mgr.TemplateCount);

            mgr.CreateVersion("b", "text");
            Assert.Equal(2, mgr.TemplateCount);

            // Adding to existing template doesn't increase count
            mgr.CreateVersion("a", "text2");
            Assert.Equal(2, mgr.TemplateCount);
        }

        [Fact]
        public void TotalVersionCount_CorrectAcrossTemplates()
        {
            var mgr = new PromptVersionManager();
            Assert.Equal(0, mgr.TotalVersionCount);

            mgr.CreateVersion("a", "text");
            mgr.CreateVersion("a", "text2");
            mgr.CreateVersion("b", "text");

            Assert.Equal(3, mgr.TotalVersionCount);
        }

        [Fact]
        public void TotalVersionCount_DecreasesAfterDelete()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("a", "v1");
            mgr.CreateVersion("a", "v2");
            mgr.CreateVersion("b", "v1");

            mgr.DeleteHistory("a");
            Assert.Equal(1, mgr.TotalVersionCount);
        }
    }

    // =================================================================
    // Edge Case Tests
    // =================================================================
    public class VersionManagerEdgeCaseTests
    {
        [Fact]
        public void VeryLongTemplateText()
        {
            var mgr = new PromptVersionManager();
            var longText = new string('x', 100_000);
            var v = mgr.CreateVersion("t", longText);

            Assert.Equal(100_000, v.TemplateText.Length);
        }

        [Fact]
        public void EmptyDescription_IsStored()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("t", "text", description: "");
            Assert.Equal("", v.Description);
        }

        [Fact]
        public void NullAuthor_IsStored()
        {
            var mgr = new PromptVersionManager();
            var v = mgr.CreateVersion("t", "text", author: null);
            Assert.Null(v.Author);
        }

        [Fact]
        public void SpecialCharsInTemplateText()
        {
            var mgr = new PromptVersionManager();
            var text = "Hello 🌍! <script>alert('xss')</script>\n\t\"quotes\" & 'apostrophes'";
            var v = mgr.CreateVersion("t", text);
            Assert.Equal(text, v.TemplateText);
        }

        [Fact]
        public void TemplateNameCaseInsensitive()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("MyTemplate", "v1");
            mgr.CreateVersion("mytemplate", "v2");

            Assert.Equal(1, mgr.TemplateCount);
            Assert.Equal(2, mgr.GetVersionCount("MYTEMPLATE"));
        }

        [Fact]
        public void GetSummary_SingleLine()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "line1");
            mgr.CreateVersion("t", "line1\nline2");

            var diff = mgr.Compare("t", 1, 2);
            Assert.Equal("+1 line", diff.GetSummary());
        }

        [Fact]
        public void GetSummary_PluralLines()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "a");
            mgr.CreateVersion("t", "a\nb\nc");

            var diff = mgr.Compare("t", 1, 2);
            Assert.Contains("+2 lines", diff.GetSummary());
        }

        [Fact]
        public void Rollback_ThenGetLatest_ReturnsRollback()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "original");
            mgr.CreateVersion("t", "changed");
            mgr.Rollback("t", 1);

            var latest = mgr.GetLatest("t");
            Assert.Equal("original", latest!.TemplateText);
        }

        [Fact]
        public void MultipleRollbacks_IncrementVersions()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "v1");
            mgr.CreateVersion("t", "v2");
            var r1 = mgr.Rollback("t", 1);
            var r2 = mgr.Rollback("t", 1);

            Assert.Equal(3, r1.VersionNumber);
            Assert.Equal(4, r2.VersionNumber);
            Assert.Equal(4, mgr.GetVersionCount("t"));
        }

        [Fact]
        public void CreateVersion_TrimsName()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("  my-template  ", "text");

            Assert.NotNull(mgr.GetLatest("my-template"));
            Assert.Equal(1, mgr.TemplateCount);
        }

        [Fact]
        public void Compare_DefaultsChangedCount()
        {
            var mgr = new PromptVersionManager();
            mgr.CreateVersion("t", "text", defaults:
                new Dictionary<string, string>
                    { ["a"] = "1", ["b"] = "2", ["c"] = "3" });
            mgr.CreateVersion("t", "text", defaults:
                new Dictionary<string, string>
                    { ["a"] = "changed", ["d"] = "new" });

            var diff = mgr.Compare("t", 1, 2);

            Assert.Contains("a", diff.ChangedDefaults);
            Assert.Contains("d", diff.AddedDefaults);
            Assert.Contains("b", diff.RemovedDefaults);
            Assert.Contains("c", diff.RemovedDefaults);
        }
    }
}
