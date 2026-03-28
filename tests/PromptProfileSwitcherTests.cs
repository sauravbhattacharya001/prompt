namespace Prompt.Tests
{
    using Xunit;

    public class PromptProfileSwitcherTests
    {
        [Fact]
        public void BuiltInProfiles_AreLoaded()
        {
            var switcher = new PromptProfileSwitcher();
            Assert.True(switcher.Count >= 5);
            Assert.True(switcher.HasProfile("creative"));
            Assert.True(switcher.HasProfile("precise"));
            Assert.True(switcher.HasProfile("concise"));
            Assert.True(switcher.HasProfile("balanced"));
            Assert.True(switcher.HasProfile("conversational"));
        }

        [Fact]
        public void SwitchTo_SetsActiveProfile()
        {
            var switcher = new PromptProfileSwitcher();
            var profile = switcher.SwitchTo("creative");
            Assert.Equal("Creative", profile.Name);
            Assert.Equal("creative", switcher.ActiveProfileName);
            Assert.NotNull(switcher.ActiveProfile);
        }

        [Fact]
        public void SwitchTo_ThrowsForUnknown()
        {
            var switcher = new PromptProfileSwitcher();
            Assert.Throws<KeyNotFoundException>(() => switcher.SwitchTo("nonexistent"));
        }

        [Fact]
        public void Register_And_Remove()
        {
            var switcher = new PromptProfileSwitcher(loadBuiltIns: false);
            var profile = new PromptProfile("Test", "A test profile");
            switcher.Register("test", profile);
            Assert.True(switcher.HasProfile("test"));
            Assert.Equal(1, switcher.Count);

            switcher.Remove("test");
            Assert.False(switcher.HasProfile("test"));
        }

        [Fact]
        public void Compare_FindsDifferences()
        {
            var switcher = new PromptProfileSwitcher();
            var diffs = switcher.Compare("creative", "precise");
            Assert.True(diffs.Count > 0);
            Assert.Contains(diffs, d => d.Property == "Temperature");
        }

        [Fact]
        public void Blend_InterpolatesValues()
        {
            var switcher = new PromptProfileSwitcher();
            var blended = switcher.Blend("precise", "creative", 0.5);
            // Temperature should be between 0.1 and 1.0
            Assert.True(blended.Temperature > 0.1 && blended.Temperature < 1.0);
        }

        [Fact]
        public void Resolve_InheritsFromParent()
        {
            var switcher = new PromptProfileSwitcher(loadBuiltIns: false);
            var parent = new PromptProfile("Parent", "Base", SystemPrompt: "Be helpful", Temperature: 0.5);
            var child = new PromptProfile("Child", "Derived", ParentProfile: "parent", Temperature: 0.7);

            switcher.Register("parent", parent);
            switcher.Register("child", child);

            var resolved = switcher.Resolve("child");
            Assert.Equal("Be helpful", resolved.SystemPrompt);
            Assert.Equal("Child", resolved.Name);
        }

        [Fact]
        public void ExportImport_RoundTrips()
        {
            var switcher = new PromptProfileSwitcher();
            switcher.SwitchTo("balanced");
            var json = switcher.ExportJson();

            var switcher2 = new PromptProfileSwitcher(loadBuiltIns: false);
            var count = switcher2.ImportJson(json);
            Assert.True(count >= 5);
            Assert.Equal("balanced", switcher2.ActiveProfileName);
        }

        [Fact]
        public void ListProfiles_IncludesAll()
        {
            var switcher = new PromptProfileSwitcher();
            switcher.SwitchTo("precise");
            var list = switcher.ListProfiles();
            Assert.Contains("precise", list);
            Assert.Contains("[ACTIVE]", list);
        }
    }
}
