namespace Prompt.Tests
{
    using Xunit;

    public class PromptDiffTests
    {
        [Fact]
        public void Compare_IdenticalTemplates_ReturnsEqual()
        {
            var t = new PromptTemplate("Hello {{name}}",
                new Dictionary<string, string> { ["name"] = "world" });

            var result = PromptDiff.Compare(t, t);

            Assert.True(result.AreEqual);
            Assert.Equal(1.0, result.Similarity);
            Assert.Equal(0, result.Changes.Count);
        }

        [Fact]
        public void Compare_DifferentBody_DetectsModification()
        {
            var old = new PromptTemplate("You are a {{role}}.");
            var updated = new PromptTemplate("You are an expert {{role}}.");

            var result = PromptDiff.Compare(old, updated);

            Assert.False(result.AreEqual);
            Assert.True(result.Similarity > 0.5);
            Assert.True(result.Modifications > 0);
        }

        [Fact]
        public void Compare_AddedVariable_DetectsAddition()
        {
            var old = new PromptTemplate("Help with {{topic}}.");
            var updated = new PromptTemplate("Help with {{topic}} in {{style}} style.");

            var result = PromptDiff.Compare(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "variable[style]");
        }

        [Fact]
        public void Compare_RemovedVariable_DetectsRemoval()
        {
            var old = new PromptTemplate("Be {{tone}} about {{topic}}.");
            var updated = new PromptTemplate("Discuss {{topic}}.");

            var result = PromptDiff.Compare(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed && c.Field == "variable[tone]");
        }

        [Fact]
        public void Compare_ChangedDefault_DetectsModification()
        {
            var old = new PromptTemplate("You are a {{role}}.",
                new Dictionary<string, string> { ["role"] = "assistant" });
            var updated = new PromptTemplate("You are a {{role}}.",
                new Dictionary<string, string> { ["role"] = "expert" });

            var result = PromptDiff.Compare(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Modified &&
                c.Field == "default[role]" &&
                c.OldValue == "assistant" &&
                c.NewValue == "expert");
        }

        [Fact]
        public void Compare_AddedDefault_DetectsAddition()
        {
            var old = new PromptTemplate("Be {{style}}.");
            var updated = new PromptTemplate("Be {{style}}.",
                new Dictionary<string, string> { ["style"] = "concise" });

            var result = PromptDiff.Compare(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "default[style]");
        }

        [Fact]
        public void CompareEntries_DetectsMetadataChanges()
        {
            var oldEntry = new PromptEntry("test",
                new PromptTemplate("Hello"),
                description: "Old description",
                category: "general",
                tags: new[] { "hello", "greeting" });

            var newEntry = new PromptEntry("test",
                new PromptTemplate("Hello"),
                description: "New description",
                category: "social",
                tags: new[] { "hello", "welcome" });

            var result = PromptDiff.CompareEntries(oldEntry, newEntry);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Modified && c.Field == "description");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Modified && c.Field == "category");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "tag[welcome]");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed && c.Field == "tag[greeting]");
        }

        [Fact]
        public void CompareLibraries_DetectsAddedAndRemovedEntries()
        {
            var oldLib = new PromptLibrary();
            oldLib.Add("entry-a", new PromptTemplate("A"));
            oldLib.Add("entry-b", new PromptTemplate("B"));

            var newLib = new PromptLibrary();
            newLib.Add("entry-b", new PromptTemplate("B modified"));
            newLib.Add("entry-c", new PromptTemplate("C"));

            var result = PromptDiff.CompareLibraries(oldLib, newLib);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed && c.Field == "entry[entry-a]");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "entry[entry-c]");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Modified && c.Field == "entry[entry-b]");
        }

        [Fact]
        public void ToSummary_ReturnsReadableOutput()
        {
            var old = new PromptTemplate("Hello {{name}}.");
            var updated = new PromptTemplate("Hi {{name}}, welcome!");

            var result = PromptDiff.Compare(old, updated);
            var summary = result.ToSummary();

            Assert.Contains("Similarity:", summary);
            Assert.Contains("Changes:", summary);
        }

        [Fact]
        public void ToJson_ReturnsValidJson()
        {
            var old = new PromptTemplate("A {{x}}.");
            var updated = new PromptTemplate("B {{y}}.");

            var result = PromptDiff.Compare(old, updated);
            var json = result.ToJson();

            Assert.Contains("\"areEqual\"", json);
            Assert.Contains("\"similarity\"", json);
            Assert.Contains("\"changes\"", json);
        }

        [Fact]
        public void ToUnifiedDiff_ReturnsUnifiedFormat()
        {
            var old = new PromptTemplate("Line one.\nLine two.");
            var updated = new PromptTemplate("Line one.\nLine three.");

            var result = PromptDiff.Compare(old, updated);
            var unified = result.ToUnifiedDiff();

            Assert.Contains("--- old", unified);
            Assert.Contains("+++ new", unified);
        }

        [Fact]
        public void Compare_NullTemplate_ThrowsArgumentNull()
        {
            var t = new PromptTemplate("Hello");

            Assert.Throws<ArgumentNullException>(() => PromptDiff.Compare(null!, t));
            Assert.Throws<ArgumentNullException>(() => PromptDiff.Compare(t, null!));
        }
    }
}
