namespace Prompt.Tests
{
    using Xunit;
    using System;
    using System.Collections.Generic;

    public class PromptMemoryIndexTests
    {
        [Fact] public void Add_Single() { var i = new PromptMemoryIndex(); i.Add("user", "Hello world"); Assert.Equal(1, i.Count); }
        [Fact] public void Add_Empty_Throws() { Assert.Throws<ArgumentException>(() => new PromptMemoryIndex().Add("user", "")); }
        [Fact] public void Add_Null_Throws() { Assert.Throws<ArgumentNullException>(() => new PromptMemoryIndex().Add((MemoryEntry)null!)); }

        [Fact]
        public void Add_ReturnsEntry()
        {
            var e = new PromptMemoryIndex().Add("assistant", "Test", new[] { "t1" }, 0.8);
            Assert.Equal("assistant", e.Role); Assert.Contains("t1", e.Tags); Assert.Equal(0.8, e.Importance);
        }

        [Fact]
        public void AddConversation_Indexes()
        {
            var i = new PromptMemoryIndex();
            i.AddConversation(new[] { ("user", "Python?"), ("assistant", "A language."), ("user", "Java?") });
            Assert.Equal(3, i.Count);
        }

        [Fact]
        public void AddConversation_SkipsEmpty()
        {
            var i = new PromptMemoryIndex();
            i.AddConversation(new[] { ("user", "Valid"), ("user", ""), ("user", "Also valid") });
            Assert.Equal(2, i.Count);
        }

        [Fact] public void Retrieve_EmptyQuery() { var i = new PromptMemoryIndex(); i.Add("user", "Content"); Assert.Empty(i.Retrieve("")); }

        [Fact]
        public void Retrieve_Matching()
        {
            var i = new PromptMemoryIndex();
            i.Add("user", "My favorite color is blue");
            i.Add("user", "I like programming in Python");
            var r = i.Retrieve("blue color");
            Assert.NotEmpty(r);
            Assert.Contains("blue", r[0].Entry.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact] public void Retrieve_NoMatch() { var i = new PromptMemoryIndex(); i.Add("user", "I like cats"); Assert.Empty(i.Retrieve("quantum physics thermodynamics")); }

        [Fact]
        public void Retrieve_RanksRelevant()
        {
            var i = new PromptMemoryIndex();
            i.Add("user", "Python is great for machine learning and data science");
            i.Add("user", "I went to the grocery store yesterday");
            i.Add("user", "Python web scraping with BeautifulSoup");
            var r = i.Retrieve("Python machine learning", new MemoryRetrievalOptions { RecencyWeight = 0, ImportanceWeight = 0, RelevanceWeight = 1.0 });
            Assert.True(r.Count >= 2);
            Assert.Contains("machine learning", r[0].Entry.Content);
        }

        [Fact]
        public void Retrieve_MaxResults()
        {
            var i = new PromptMemoryIndex();
            for (int x = 0; x < 20; x++) i.Add("user", $"Test message {x} about coding");
            Assert.True(i.Retrieve("coding", new MemoryRetrievalOptions { MaxResults = 3 }).Count <= 3);
        }

        [Fact]
        public void Retrieve_UpdatesRecall()
        {
            var i = new PromptMemoryIndex();
            var e = i.Add("user", "Remember this important fact");
            i.Retrieve("important fact");
            Assert.Equal(1, e.RecallCount); Assert.NotNull(e.LastRecalled);
        }

        [Fact]
        public void Retrieve_RoleFilter()
        {
            var i = new PromptMemoryIndex();
            i.Add("user", "User coding msg"); i.Add("assistant", "Asst coding msg");
            var r = i.Retrieve("coding", new MemoryRetrievalOptions { RoleFilter = new HashSet<string> { "user" } });
            Assert.All(r, x => Assert.Equal("user", x.Entry.Role));
        }

        [Fact]
        public void Retrieve_TagFilter()
        {
            var i = new PromptMemoryIndex();
            i.Add("user", "Tagged coding msg", new[] { "tech" });
            i.Add("user", "Untagged coding msg");
            var r = i.Retrieve("coding", new MemoryRetrievalOptions { TagFilter = new HashSet<string> { "tech" } });
            Assert.All(r, x => Assert.Contains("tech", x.Entry.Tags));
        }

        [Fact] public void Remove_Existing() { var i = new PromptMemoryIndex(); var e = i.Add("user", "Rm"); Assert.True(i.Remove(e.Id)); Assert.Equal(0, i.Count); }
        [Fact] public void Remove_Nonexistent() { Assert.False(new PromptMemoryIndex().Remove("nope")); }

        [Fact]
        public void RemoveWhere()
        {
            var i = new PromptMemoryIndex();
            i.Add("user", "Keep"); i.Add("system", "S1"); i.Add("system", "S2");
            Assert.Equal(2, i.RemoveWhere(e => e.Role == "system")); Assert.Equal(1, i.Count);
        }

        [Fact] public void FormatContext_Empty() { Assert.Equal("", new PromptMemoryIndex().FormatAsContext(new List<MemoryResult>())); }

        [Fact]
        public void FormatContext_Content()
        {
            var i = new PromptMemoryIndex(); i.Add("user", "Context about blue");
            var ctx = i.FormatAsContext(i.Retrieve("blue"));
            Assert.Contains("Relevant Context from Memory", ctx); Assert.Contains("[user]", ctx);
        }

        [Fact]
        public void FormatContext_Metadata()
        {
            var i = new PromptMemoryIndex(); i.Add("user", "Metadata test content");
            Assert.Contains("score:", i.FormatAsContext(i.Retrieve("metadata test"), includeMetadata: true));
        }

        [Fact] public void Stats_Empty() { var s = new PromptMemoryIndex().GetStats(); Assert.Equal(0, s.TotalEntries); Assert.Null(s.OldestEntry); }

        [Fact]
        public void Stats_Populated()
        {
            var i = new PromptMemoryIndex();
            i.Add("user", "Msg one", new[] { "t1" }); i.Add("assistant", "Msg two", new[] { "t2" });
            var s = i.GetStats(); Assert.Equal(2, s.TotalEntries); Assert.Equal(2, s.UniqueTags);
        }

        [Fact]
        public void ExportImport_RoundTrip()
        {
            var i = new PromptMemoryIndex(); i.Add("user", "First"); i.Add("assistant", "Second");
            var i2 = new PromptMemoryIndex(); Assert.Equal(2, i2.ImportJson(i.ExportJson())); Assert.Equal(2, i2.Count);
        }

        [Fact] public void ImportJson_Empty_Throws() { Assert.Throws<ArgumentException>(() => new PromptMemoryIndex().ImportJson("")); }
        [Fact] public void Clear() { var i = new PromptMemoryIndex(); i.Add("user", "One"); i.Add("user", "Two"); i.Clear(); Assert.Equal(0, i.Count); }
        [Fact] public void GetAll_RoleFilter() { var i = new PromptMemoryIndex(); i.Add("user", "U"); i.Add("assistant", "A"); Assert.Single(i.GetAll(roleFilter: "user")); }

        [Fact]
        public void AutoEvict()
        {
            var i = new PromptMemoryIndex { MaxEntries = 3 };
            i.Add("user", "First oldest"); i.Add("user", "Second"); i.Add("user", "Third"); i.Add("user", "Fourth");
            Assert.Equal(3, i.Count); Assert.DoesNotContain(i.GetAll(), e => e.Content == "First oldest");
        }

        [Fact] public void Merge() { var a = new PromptMemoryIndex(); a.Add("user", "A"); var b = new PromptMemoryIndex(); b.Add("user", "B"); Assert.Equal(1, a.Merge(b)); Assert.Equal(2, a.Count); }

        [Fact]
        public void Merge_Dedup()
        {
            var a = new PromptMemoryIndex(); var e = a.Add("user", "Shared");
            var b = new PromptMemoryIndex(); b.Add(new MemoryEntry { Id = e.Id, Role = "user", Content = "Shared" });
            Assert.Equal(0, a.Merge(b));
        }

        [Fact]
        public void NoveltyBoost()
        {
            var i = new PromptMemoryIndex();
            var f = i.Add("user", "Frequently recalled coding tip"); f.RecallCount = 10;
            i.Add("user", "Fresh coding tip never recalled");
            var r = i.Retrieve("coding tip", new MemoryRetrievalOptions
            { NoveltyBoost = true, RecencyWeight = 0, RelevanceWeight = 0.5, ImportanceWeight = 0.5 });
            Assert.True(r.Count >= 2);
            // Fresh entry was recalled once by this Retrieve, frequent was recalled once more (11)
            // Fresh should rank higher due to novelty boost
            Assert.Contains("Fresh", r[0].Entry.Content);
        }

        [Fact]
        public void ClampsImportance()
        {
            var i = new PromptMemoryIndex();
            Assert.Equal(1.0, i.Add("user", "High", importance: 5.0).Importance);
            Assert.Equal(0.0, i.Add("user", "Low", importance: -1.0).Importance);
        }
    }
}
