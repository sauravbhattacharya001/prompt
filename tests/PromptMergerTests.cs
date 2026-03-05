namespace Prompt.Tests
{
    using Xunit;
    using Prompt;

    public class PromptMergerTests
    {
        private readonly PromptMerger _merger = new();

        [Fact]
        public void Merge_SinglePrompt_ReturnsUnchanged()
        {
            var result = _merger.Merge(new[] { "Hello world" });
            Assert.Equal("Hello world", result.MergedText);
            Assert.Equal(1, result.SourceCount);
            Assert.True(result.IsClean);
        }

        [Fact]
        public void Merge_TwoDistinctPrompts_CombinesThem()
        {
            var result = _merger.Merge("## Task\nDo X", "## Context\nKnow Y");
            Assert.Contains("Do X", result.MergedText);
            Assert.Contains("Know Y", result.MergedText);
            Assert.Equal(2, result.SourceCount);
        }

        [Fact]
        public void Merge_EmptyInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => _merger.Merge(Array.Empty<string>()));
        }

        [Fact]
        public void Merge_NullInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => _merger.Merge((IEnumerable<string>)null!));
        }

        [Fact]
        public void Merge_NullElement_Throws()
        {
            Assert.Throws<ArgumentException>(() => _merger.Merge(new string[] { null! }));
        }

        [Fact]
        public void Merge_TooManySources_Throws()
        {
            var sources = Enumerable.Range(0, 21).Select(i => "prompt").ToArray();
            Assert.Throws<ArgumentException>(() => _merger.Merge(sources));
        }

        [Fact]
        public void Merge_TooLongSource_Throws()
        {
            var long_ = new string('x', 100_001);
            Assert.Throws<ArgumentException>(() => _merger.Merge(new[] { long_ }));
        }

        [Fact]
        public void Merge_KeepFirst_UsesFirstSource()
        {
            var result = _merger.Merge(
                new[] { "## Task\nFirst task", "## Task\nSecond task" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.KeepFirst, DeduplicateLines = false });
            Assert.Contains("First task", result.MergedText);
            Assert.DoesNotContain("Second task", result.MergedText);
            Assert.Single(result.Conflicts);
        }

        [Fact]
        public void Merge_KeepLast_UsesLastSource()
        {
            var result = _merger.Merge(
                new[] { "## Task\nFirst task", "## Task\nSecond task" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.KeepLast, DeduplicateLines = false });
            Assert.Contains("Second task", result.MergedText);
            Assert.DoesNotContain("First task", result.MergedText);
        }

        [Fact]
        public void Merge_Concatenate_IncludesBoth()
        {
            var result = _merger.Merge(
                new[] { "## Task\nFirst task", "## Task\nSecond task" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.Concatenate, DeduplicateLines = false });
            Assert.Contains("First task", result.MergedText);
            Assert.Contains("Second task", result.MergedText);
        }

        [Fact]
        public void Merge_Interleave_AlternatesLines()
        {
            var result = _merger.Merge(
                new[] { "## Task\nLine A1\nLine A2", "## Task\nLine B1\nLine B2" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.Interleave, DeduplicateLines = false });
            Assert.Contains("Line A1", result.MergedText);
            Assert.Contains("Line B1", result.MergedText);
            Assert.True(result.MergedText.IndexOf("Line A1") < result.MergedText.IndexOf("Line B1"));
        }

        [Fact]
        public void Merge_MarkConflict_AddsMarkers()
        {
            var result = _merger.Merge(
                new[] { "## Task\nFirst", "## Task\nSecond" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.MarkConflict, DeduplicateLines = false });
            Assert.Contains("<<<<<<<", result.MergedText);
            Assert.Contains("=======", result.MergedText);
            Assert.Contains(">>>>>>>", result.MergedText);
        }

        [Fact]
        public void ParseSections_MarkdownHeadings_Detected()
        {
            var sections = _merger.ParseSections("## Task\nDo this\n## Context\nKnow that");
            Assert.Equal(2, sections.Count);
            Assert.Equal("task", sections[0].Name);
            Assert.Equal("context", sections[1].Name);
        }

        [Fact]
        public void ParseSections_AllCapsHeading_Detected()
        {
            var sections = _merger.ParseSections("INSTRUCTIONS:\nDo this\nCONTEXT:\nKnow that");
            Assert.Equal(2, sections.Count);
            Assert.Equal("instructions", sections[0].Name);
        }

        [Fact]
        public void ParseSections_NoHeadings_SingleDefault()
        {
            var sections = _merger.ParseSections("Just some plain text prompt");
            Assert.Single(sections);
            Assert.Equal("_default", sections[0].Name);
        }

        [Fact]
        public void ParseSections_Preamble_BeforeFirstHeading()
        {
            var sections = _merger.ParseSections("Preamble text\n## Task\nDo this");
            Assert.Equal(2, sections.Count);
            Assert.Equal("_preamble", sections[0].Name);
            Assert.Equal("task", sections[1].Name);
        }

        [Fact]
        public void ParseSections_EmptyPrompt_ReturnsDefault()
        {
            var sections = _merger.ParseSections("");
            Assert.Single(sections);
            Assert.Equal("_default", sections[0].Name);
        }

        [Fact]
        public void ExtractVariables_FindsTemplateVars()
        {
            var vars = _merger.ExtractVariables("Hello {{name}}, you are {{role}}.");
            Assert.Contains("name", vars);
            Assert.Contains("role", vars);
            Assert.Equal(2, vars.Count);
        }

        [Fact]
        public void ExtractVariables_EmptyText_ReturnsEmpty()
        {
            Assert.Empty(_merger.ExtractVariables(""));
        }

        [Fact]
        public void Merge_CollectsVariablesAcrossSources()
        {
            var result = _merger.Merge(new[] { "Hello {{name}}", "Role: {{role}}" });
            Assert.Equal(2, result.Variables.Count);
            Assert.Contains("name", result.Variables.Keys);
            Assert.Contains("role", result.Variables.Keys);
        }

        [Fact]
        public void Merge_SharedVariable_RecordsBothSources()
        {
            var result = _merger.Merge(new[] { "{{name}} is cool", "Dear {{name}}" });
            Assert.Contains("name", result.Variables.Keys);
            Assert.Equal(new List<int> { 0, 1 }, result.Variables["name"]);
        }

        [Fact]
        public void Merge_MergeVariablesDisabled_NoVariables()
        {
            var result = _merger.Merge(new[] { "{{name}}" }, new MergeOptions { MergeVariables = false });
            Assert.Empty(result.Variables);
        }

        [Fact]
        public void Merge_DeduplicateLines_RemovesDuplicates()
        {
            var result = _merger.Merge(
                new[] { "## Rules\nBe concise\nBe helpful", "## Other\nBe concise\nBe creative" },
                new MergeOptions { DeduplicateLines = true });
            var count = result.MergedText.Split('\n').Count(l => l.Trim() == "Be concise");
            Assert.Equal(1, count);
            Assert.True(result.DuplicatesRemoved > 0);
        }

        [Fact]
        public void Merge_DeduplicateDisabled_KeepsDuplicates()
        {
            var result = _merger.Merge(
                new[] { "## A\nBe concise", "## B\nBe concise" },
                new MergeOptions { DeduplicateLines = false });
            var count = result.MergedText.Split('\n').Count(l => l.Trim() == "Be concise");
            Assert.True(count >= 2);
        }

        [Fact]
        public void Merge_MaxLength_Truncates()
        {
            var result = _merger.Merge(new[] { new string('a', 200) }, new MergeOptions { MaxLength = 50 });
            Assert.True(result.MergedText.Length <= 50);
            Assert.True(result.WasTruncated);
        }

        [Fact]
        public void Merge_MaxLengthZero_NoTruncation()
        {
            var text = new string('a', 500);
            var result = _merger.Merge(new[] { text });
            Assert.Equal(500, result.MergedText.Length);
            Assert.False(result.WasTruncated);
        }

        [Fact]
        public void Merge_CustomSectionOrder_Respected()
        {
            var result = _merger.Merge(
                new[] { "## Task\nDo X\n## Context\nKnow Y" },
                new MergeOptions { SectionOrder = new List<string> { "context", "task" } });
            Assert.True(result.MergedText.IndexOf("Know Y") < result.MergedText.IndexOf("Do X"));
        }

        [Fact]
        public void Merge_ExcludeSections_Removes()
        {
            var result = _merger.Merge(
                new[] { "## Task\nDo X\n## Context\nKnow Y" },
                new MergeOptions { ExcludeSections = new HashSet<string> { "context" } });
            Assert.Contains("Do X", result.MergedText);
            Assert.DoesNotContain("Know Y", result.MergedText);
        }

        [Fact]
        public void ComputeSimilarity_Identical_ReturnsOne()
        {
            Assert.Equal(1.0, _merger.ComputeSimilarity("abc", "abc"));
        }

        [Fact]
        public void ComputeSimilarity_Different_ReturnsLow()
        {
            Assert.True(_merger.ComputeSimilarity("hello world", "foo bar baz") < 0.5);
        }

        [Fact]
        public void ComputeSimilarity_BothEmpty_ReturnsOne()
        {
            Assert.Equal(1.0, _merger.ComputeSimilarity("", ""));
        }

        [Fact]
        public void ComputeSimilarity_OneEmpty_ReturnsZero()
        {
            Assert.Equal(0.0, _merger.ComputeSimilarity("hello", ""));
        }

        [Fact]
        public void GenerateContributionReport_ContainsExpectedInfo()
        {
            var prompts = new[] { "## Task\nDo X", "## Context\nKnow Y" };
            var result = _merger.Merge(prompts);
            var report = _merger.GenerateContributionReport(result, prompts);
            Assert.Contains("Merge Contribution Report", report);
            Assert.Contains("Sources: 2", report);
        }

        [Fact]
        public void ToJson_ReturnsValidJson()
        {
            var result = _merger.Merge(new[] { "Hello {{name}}" });
            var json = _merger.ToJson(result);
            Assert.Contains("mergedText", json);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<MergeResult>(json);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void Merge_ThreeSources_AllMerged()
        {
            var result = _merger.Merge(new[] { "## Task\nDo A", "## Context\nKnow B", "## Format\nUse C" });
            Assert.Equal(3, result.SourceCount);
            Assert.Contains("Do A", result.MergedText);
            Assert.Contains("Know B", result.MergedText);
            Assert.Contains("Use C", result.MergedText);
            Assert.True(result.IsClean);
        }

        [Fact]
        public void Merge_IdenticalSections_NoConflict()
        {
            var result = _merger.Merge(
                new[] { "## Task\nDo X", "## Task\nDo X" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.KeepFirst, DeduplicateLines = false });
            Assert.True(result.IsClean);
        }

        [Fact]
        public void Merge_TwoPromptConvenience_Works()
        {
            var result = _merger.Merge("Hello", "World");
            Assert.Equal(2, result.SourceCount);
        }

        [Fact]
        public void Merge_20Sources_AtLimit()
        {
            var sources = Enumerable.Range(0, 20).Select(i => $"Prompt {i}").ToArray();
            var result = _merger.Merge(sources);
            Assert.Equal(20, result.SourceCount);
        }

        [Fact]
        public void Merge_WhitespaceOnly_Handled()
        {
            var result = _merger.Merge(new[] { "   \n  \n  " });
            Assert.Equal(1, result.SourceCount);
        }

        [Fact]
        public void Merge_MultipleHeadingLevels_Detected()
        {
            var sections = _merger.ParseSections("# H1\ncontent1\n## H2\ncontent2\n### H3\ncontent3");
            Assert.Equal(3, sections.Count);
        }

        [Fact]
        public void Merge_CustomConflictMarkerPrefix()
        {
            var result = _merger.Merge(
                new[] { "## Task\nA", "## Task\nB" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.MarkConflict, ConflictMarkerPrefix = "CONFLICT>>> ", DeduplicateLines = false });
            Assert.Contains("CONFLICT>>>", result.MergedText);
        }

        [Fact]
        public void Merge_ExcludeNonexistent_NoEffect()
        {
            var result = _merger.Merge(
                new[] { "## Task\nDo X" },
                new MergeOptions { ExcludeSections = new HashSet<string> { "nonexistent" } });
            Assert.Contains("Do X", result.MergedText);
        }

        [Fact]
        public void Merge_ConflictRecordsSourceIndices()
        {
            var result = _merger.Merge(
                new[] { "## Task\nFirst", "## Task\nSecond" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.KeepFirst, DeduplicateLines = false });
            Assert.Single(result.Conflicts);
            Assert.Contains(0, result.Conflicts[0].SourceIndices);
            Assert.Contains(1, result.Conflicts[0].SourceIndices);
        }

        [Fact]
        public void Merge_ThreeWayConflict_AllRecorded()
        {
            var result = _merger.Merge(
                new[] { "## Task\nA", "## Task\nB", "## Task\nC" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.Concatenate, DeduplicateLines = false });
            Assert.Single(result.Conflicts);
            Assert.Equal(3, result.Conflicts[0].SourceIndices.Count);
        }

        [Fact]
        public void ComputeSimilarity_PartialOverlap_MidRange()
        {
            var score = _merger.ComputeSimilarity("line1\nline2\nline3", "line1\nline2\nline4");
            Assert.True(score > 0.3 && score < 0.9);
        }

        [Fact]
        public void Merge_Concatenate_WithDedup_RemovesDuplicateLines()
        {
            var result = _merger.Merge(
                new[] { "## Rules\nBe concise\nBe helpful", "## Rules\nBe concise\nBe creative" },
                new MergeOptions { ConflictStrategy = MergeConflictStrategy.Concatenate, DeduplicateLines = true });
            var count = result.MergedText.Split('\n').Count(l => l.Trim() == "Be concise");
            Assert.Equal(1, count);
        }

        [Fact]
        public void Merge_Length_MatchesMergedText()
        {
            var result = _merger.Merge(new[] { "Hello world" });
            Assert.Equal(result.MergedText.Length, result.Length);
        }

        [Fact]
        public void ExtractVariables_NullText_ReturnsEmpty()
        {
            Assert.Empty(_merger.ExtractVariables(null!));
        }

        [Fact]
        public void Merge_CustomOrderPartialMatch_RemainingAppended()
        {
            var result = _merger.Merge(
                new[] { "## A\na\n## B\nb\n## C\nc" },
                new MergeOptions { SectionOrder = new List<string> { "c" } });
            Assert.True(result.MergedText.IndexOf("## C") < result.MergedText.IndexOf("## A"));
        }
    }
}
