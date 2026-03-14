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

        // ═══════════════════════════════════════
        // DiffChange.ToString()
        // ═══════════════════════════════════════

        [Fact]
        public void DiffChange_ToString_Added()
        {
            var change = new DiffChange(DiffChangeType.Added, "field", null, "newVal");
            Assert.Equal("+ field: newVal", change.ToString());
        }

        [Fact]
        public void DiffChange_ToString_Removed()
        {
            var change = new DiffChange(DiffChangeType.Removed, "field", "oldVal", null);
            Assert.Equal("- field: oldVal", change.ToString());
        }

        [Fact]
        public void DiffChange_ToString_Modified()
        {
            var change = new DiffChange(DiffChangeType.Modified, "field", "old", "new");
            Assert.Contains("→", change.ToString());
            Assert.Contains("old", change.ToString());
            Assert.Contains("new", change.ToString());
        }

        [Fact]
        public void DiffChange_ToString_Unchanged()
        {
            var change = new DiffChange(DiffChangeType.Unchanged, "field", "val", "val");
            Assert.Contains("field", change.ToString());
            Assert.Contains("val", change.ToString());
        }

        [Fact]
        public void DiffChange_NullField_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DiffChange(DiffChangeType.Added, null!, null, "val"));
        }

        // ═══════════════════════════════════════
        // DiffResult — AreEqual/Additions/Removals/Modifications
        // ═══════════════════════════════════════

        [Fact]
        public void DiffResult_NoChanges_AreEqualTrue()
        {
            var t = new PromptTemplate("Hello");
            var result = PromptDiff.Compare(t, t);

            Assert.True(result.AreEqual);
            Assert.Equal(0, result.Additions);
            Assert.Equal(0, result.Removals);
            Assert.Equal(0, result.Modifications);
        }

        [Fact]
        public void DiffResult_CountsMatchChanges()
        {
            var old = new PromptTemplate("Test {{a}} and {{b}}.",
                new Dictionary<string, string> { ["a"] = "x", ["b"] = "y" });
            var updated = new PromptTemplate("Test {{a}} and {{c}}.",
                new Dictionary<string, string> { ["a"] = "z", ["c"] = "w" });

            var result = PromptDiff.Compare(old, updated);

            // b removed, c added, a modified, template modified
            Assert.True(result.Additions > 0);
            Assert.True(result.Removals > 0);
            Assert.True(result.Modifications > 0);
            Assert.False(result.AreEqual);
        }

        // ═══════════════════════════════════════
        // Similarity — edge cases
        // ═══════════════════════════════════════

        [Fact]
        public void Compare_IdenticalBody_SimilarityIsOne()
        {
            var t1 = new PromptTemplate("Exact same text.");
            var t2 = new PromptTemplate("Exact same text.");
            var result = PromptDiff.Compare(t1, t2);
            Assert.Equal(1.0, result.Similarity, 4);
        }

        [Fact]
        public void Compare_CompletelyDifferent_LowSimilarity()
        {
            var t1 = new PromptTemplate("AAAA BBBB CCCC DDDD");
            var t2 = new PromptTemplate("XXXX YYYY ZZZZ WWWW");
            var result = PromptDiff.Compare(t1, t2);
            Assert.True(result.Similarity < 0.5);
        }

        [Fact]
        public void Compare_SimilarBodies_ModerateSimilarity()
        {
            var t1 = new PromptTemplate("You are a helpful assistant that answers questions.");
            var t2 = new PromptTemplate("You are a helpful assistant that answers queries concisely.");
            var result = PromptDiff.Compare(t1, t2);
            Assert.True(result.Similarity > 0.5);
            Assert.True(result.Similarity < 1.0);
        }

        // ═══════════════════════════════════════
        // Line-level diff (LCS-based)
        // ═══════════════════════════════════════

        [Fact]
        public void Compare_AddedLine_DetectsAddition()
        {
            var old = new PromptTemplate("Line one.\nLine two.");
            var updated = new PromptTemplate("Line one.\nLine inserted.\nLine two.");

            var result = PromptDiff.Compare(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field.StartsWith("line[") &&
                c.NewValue != null && c.NewValue.Contains("inserted"));
        }

        [Fact]
        public void Compare_RemovedLine_DetectsRemoval()
        {
            var old = new PromptTemplate("Line one.\nLine two.\nLine three.");
            var updated = new PromptTemplate("Line one.\nLine three.");

            var result = PromptDiff.Compare(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed && c.Field.StartsWith("line[") &&
                c.OldValue != null && c.OldValue.Contains("two"));
        }

        [Fact]
        public void Compare_MultiLineChange_ReportsPerLine()
        {
            var old = new PromptTemplate("A\nB\nC\nD");
            var updated = new PromptTemplate("A\nX\nC\nD");

            var result = PromptDiff.Compare(old, updated);

            // Should have line-level changes for B removed and X added
            var lineChanges = result.Changes.Where(c => c.Field.StartsWith("line[")).ToList();
            Assert.True(lineChanges.Count >= 2); // at least remove B, add X
        }

        // ═══════════════════════════════════════
        // Defaults — more scenarios
        // ═══════════════════════════════════════

        [Fact]
        public void Compare_RemovedDefault_DetectsRemoval()
        {
            var old = new PromptTemplate("Use {{style}}.",
                new Dictionary<string, string> { ["style"] = "formal" });
            var updated = new PromptTemplate("Use {{style}}.");

            var result = PromptDiff.Compare(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed &&
                c.Field == "default[style]" &&
                c.OldValue == "formal");
        }

        [Fact]
        public void Compare_MultipleDefaultChanges_AllDetected()
        {
            var old = new PromptTemplate("{{a}} {{b}} {{c}}.",
                new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
            var updated = new PromptTemplate("{{a}} {{b}} {{c}}.",
                new Dictionary<string, string> { ["a"] = "1", ["b"] = "changed", ["c"] = "new" });

            var result = PromptDiff.Compare(old, updated);

            // b modified, c added, a unchanged
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Modified && c.Field == "default[b]");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "default[c]");
            Assert.DoesNotContain(result.Changes, c =>
                c.Field == "default[a]");
        }

        // ═══════════════════════════════════════
        // CompareEntries — more scenarios
        // ═══════════════════════════════════════

        [Fact]
        public void CompareEntries_NameChange_DetectsModification()
        {
            var old = new PromptEntry("old-name", new PromptTemplate("Body"));
            var updated = new PromptEntry("new-name", new PromptTemplate("Body"));

            var result = PromptDiff.CompareEntries(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Modified && c.Field == "name");
        }

        [Fact]
        public void CompareEntries_NullToDescription_DetectsAddition()
        {
            var old = new PromptEntry("test", new PromptTemplate("Body"));
            var updated = new PromptEntry("test", new PromptTemplate("Body"),
                description: "Added description");

            var result = PromptDiff.CompareEntries(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "description");
        }

        [Fact]
        public void CompareEntries_DescriptionToNull_DetectsRemoval()
        {
            var old = new PromptEntry("test", new PromptTemplate("Body"),
                description: "Has description");
            var updated = new PromptEntry("test", new PromptTemplate("Body"));

            var result = PromptDiff.CompareEntries(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed && c.Field == "description");
        }

        [Fact]
        public void CompareEntries_NullCategory_DetectsAddition()
        {
            var old = new PromptEntry("test", new PromptTemplate("Body"));
            var updated = new PromptEntry("test", new PromptTemplate("Body"),
                category: "new-cat");

            var result = PromptDiff.CompareEntries(old, updated);

            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "category");
        }

        [Fact]
        public void CompareEntries_IdenticalEntries_AreEqual()
        {
            var entry = new PromptEntry("test", new PromptTemplate("Body"),
                description: "Desc", category: "cat",
                tags: new[] { "tag1", "tag2" });

            var result = PromptDiff.CompareEntries(entry, entry);

            Assert.True(result.AreEqual);
        }

        [Fact]
        public void CompareEntries_NullEntry_Throws()
        {
            var entry = new PromptEntry("test", new PromptTemplate("Body"));

            Assert.Throws<ArgumentNullException>(() => PromptDiff.CompareEntries(null!, entry));
            Assert.Throws<ArgumentNullException>(() => PromptDiff.CompareEntries(entry, null!));
        }

        // ═══════════════════════════════════════
        // CompareLibraries — more scenarios
        // ═══════════════════════════════════════

        [Fact]
        public void CompareLibraries_IdenticalLibraries_AreEqual()
        {
            var lib = new PromptLibrary();
            lib.Add("a", new PromptTemplate("A"));
            lib.Add("b", new PromptTemplate("B"));

            var result = PromptDiff.CompareLibraries(lib, lib);

            Assert.True(result.AreEqual);
            Assert.Equal(1.0, result.Similarity, 4);
        }

        [Fact]
        public void CompareLibraries_EmptyLibraries_AreEqual()
        {
            var lib1 = new PromptLibrary();
            var lib2 = new PromptLibrary();

            var result = PromptDiff.CompareLibraries(lib1, lib2);

            Assert.True(result.AreEqual);
        }

        [Fact]
        public void CompareLibraries_OneEmpty_AllRemoved()
        {
            var lib = new PromptLibrary();
            lib.Add("a", new PromptTemplate("A"));
            lib.Add("b", new PromptTemplate("B"));
            var empty = new PromptLibrary();

            var result = PromptDiff.CompareLibraries(lib, empty);

            Assert.Equal(2, result.Removals);
            Assert.Equal(0, result.Additions);
        }

        [Fact]
        public void CompareLibraries_NullLibrary_Throws()
        {
            var lib = new PromptLibrary();
            Assert.Throws<ArgumentNullException>(() => PromptDiff.CompareLibraries(null!, lib));
            Assert.Throws<ArgumentNullException>(() => PromptDiff.CompareLibraries(lib, null!));
        }

        // ═══════════════════════════════════════
        // ToSummary — edge cases
        // ═══════════════════════════════════════

        [Fact]
        public void ToSummary_IdenticalTemplates_ReturnsIdenticalMessage()
        {
            var t = new PromptTemplate("Hello");
            var result = PromptDiff.Compare(t, t);

            Assert.Equal("Templates are identical.", result.ToSummary());
        }

        [Fact]
        public void ToSummary_WithChanges_IncludesStatistics()
        {
            var old = new PromptTemplate("A {{x}}.");
            var updated = new PromptTemplate("B {{y}}.");

            var result = PromptDiff.Compare(old, updated);
            var summary = result.ToSummary();

            Assert.Contains("added", summary);
            Assert.Contains("removed", summary);
            Assert.Contains("modified", summary);
        }

        // ═══════════════════════════════════════
        // ToUnifiedDiff — modification shows both lines
        // ═══════════════════════════════════════

        [Fact]
        public void ToUnifiedDiff_ModifiedChange_ShowsBothOldAndNew()
        {
            var old = new PromptTemplate("Old body text.");
            var updated = new PromptTemplate("New body text.");

            var result = PromptDiff.Compare(old, updated);
            var unified = result.ToUnifiedDiff();

            // Unified diff should show - for old and + for new
            Assert.Contains("--- old", unified);
            Assert.Contains("+++ new", unified);
            Assert.Contains("@@", unified);
        }

        [Fact]
        public void ToUnifiedDiff_NoChanges_MinimalOutput()
        {
            var t = new PromptTemplate("Hello");
            var result = PromptDiff.Compare(t, t);
            var unified = result.ToUnifiedDiff();

            Assert.Contains("--- old", unified);
            Assert.Contains("+++ new", unified);
            // No + or - lines for changes
            var lines = unified.Split('\n');
            var changeLines = lines.Where(l => l.StartsWith("+ ") || l.StartsWith("- ")).ToList();
            Assert.Empty(changeLines);
        }

        // ═══════════════════════════════════════
        // ToJson — detailed verification
        // ═══════════════════════════════════════

        [Fact]
        public void ToJson_Compact_IsSingleLine()
        {
            var t = new PromptTemplate("Hello");
            var result = PromptDiff.Compare(t, t);
            var json = result.ToJson(indented: false);

            Assert.DoesNotContain("\n", json);
            Assert.Contains("\"areEqual\":true", json);
        }

        [Fact]
        public void ToJson_IncludesChangeDetails()
        {
            var old = new PromptTemplate("A {{x}}.");
            var updated = new PromptTemplate("B {{y}}.");

            var result = PromptDiff.Compare(old, updated);
            var json = result.ToJson();

            Assert.Contains("\"type\"", json);
            Assert.Contains("\"field\"", json);
        }

        // ═══════════════════════════════════════
        // Similarity — clamped to [0, 1]
        // ═══════════════════════════════════════

        [Fact]
        public void Similarity_AlwaysBetweenZeroAndOne()
        {
            var t1 = new PromptTemplate("A");
            var t2 = new PromptTemplate("Completely different text with many more words");

            var result = PromptDiff.Compare(t1, t2);

            Assert.InRange(result.Similarity, 0.0, 1.0);
        }

        // ═══════════════════════════════════════
        // Variables — case insensitive
        // ═══════════════════════════════════════

        [Fact]
        public void Compare_SameVariableDifferentCase_NoChange()
        {
            var old = new PromptTemplate("Use {{Name}}.");
            var updated = new PromptTemplate("Use {{name}}.");

            var result = PromptDiff.Compare(old, updated);

            // Variable comparison is case-insensitive; body still differs
            Assert.DoesNotContain(result.Changes, c =>
                c.Field.StartsWith("variable[") && c.Type == DiffChangeType.Added);
            Assert.DoesNotContain(result.Changes, c =>
                c.Field.StartsWith("variable[") && c.Type == DiffChangeType.Removed);
        }

        // ═══════════════════════════════════════
        // CompareEntries — tag changes with case sensitivity
        // ═══════════════════════════════════════

        [Fact]
        public void CompareEntries_TagsCaseInsensitive()
        {
            var old = new PromptEntry("test", new PromptTemplate("Body"),
                tags: new[] { "Tag1" });
            var updated = new PromptEntry("test", new PromptTemplate("Body"),
                tags: new[] { "tag1" });

            var result = PromptDiff.CompareEntries(old, updated);

            // Tags use OrdinalIgnoreCase — same tag, no change
            var tagChanges = result.Changes.Where(c => c.Field.StartsWith("tag[")).ToList();
            Assert.Empty(tagChanges);
        }

        // ═══════════════════════════════════════
        // CompareEntries — multiple tag additions/removals
        // ═══════════════════════════════════════

        [Fact]
        public void CompareEntries_MultipleTagChanges()
        {
            var old = new PromptEntry("test", new PromptTemplate("Body"),
                tags: new[] { "a", "b", "c" });
            var updated = new PromptEntry("test", new PromptTemplate("Body"),
                tags: new[] { "b", "d", "e" });

            var result = PromptDiff.CompareEntries(old, updated);

            // a and c removed, d and e added
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed && c.Field == "tag[a]");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Removed && c.Field == "tag[c]");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "tag[d]");
            Assert.Contains(result.Changes, c =>
                c.Type == DiffChangeType.Added && c.Field == "tag[e]");
        }

        // ═══════════════════════════════════════
        // Large text — line-similarity fallback
        // ═══════════════════════════════════════

        [Fact]
        public void Compare_LargeText_StillComputesSimilarity()
        {
            // Generate texts > 5000 chars to trigger line-based similarity
            var longLine = new string('A', 100);
            var lines = Enumerable.Range(0, 60).Select(i => $"{longLine} line {i}");
            var body1 = string.Join("\n", lines);
            var body2 = string.Join("\n", lines.Take(50).Concat(
                Enumerable.Range(50, 10).Select(i => $"CHANGED line {i}")));

            var t1 = new PromptTemplate(body1);
            var t2 = new PromptTemplate(body2);

            var result = PromptDiff.Compare(t1, t2);

            Assert.False(result.AreEqual);
            Assert.InRange(result.Similarity, 0.0, 1.0);
            Assert.True(result.Similarity > 0.5); // mostly same
        }
    }
}
