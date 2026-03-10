using Xunit;
using Prompt;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prompt.Tests
{
    public class PromptDatasetBuilderTests
    {
        // --- Add / Count ---

        [Fact]
        public void Add_SingleExample_IncreasesCount()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "Hello", "World"));
            Assert.Equal(1, builder.Count);
        }

        [Fact]
        public void Add_InputOutput_AutoGeneratesId()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("input", "output");
            Assert.Equal(1, builder.Count);
            Assert.Equal("ex-00001", builder.Examples[0].Id);
        }

        [Fact]
        public void Add_WithSystemPrompt_SetsAllFields()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("system", "input", "output", ExampleQuality.Good);
            var ex = builder.Examples[0];
            Assert.Equal("system", ex.SystemPrompt);
            Assert.Equal("input", ex.Input);
            Assert.Equal("output", ex.Output);
            Assert.Equal(ExampleQuality.Good, ex.Quality);
        }

        [Fact]
        public void Add_DuplicateId_Throws()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "Hello"));
            Assert.Throws<InvalidOperationException>(() => builder.Add(new DatasetExample("e1", "World")));
        }

        [Fact]
        public void Add_NullExample_Throws()
        {
            var builder = new PromptDatasetBuilder();
            Assert.Throws<ArgumentNullException>(() => builder.Add((DatasetExample)null!));
        }

        [Fact]
        public void AddRange_MultipleExamples()
        {
            var builder = new PromptDatasetBuilder();
            var examples = new[]
            {
                new DatasetExample("a", "input1", "out1"),
                new DatasetExample("b", "input2", "out2"),
                new DatasetExample("c", "input3", "out3")
            };
            builder.AddRange(examples);
            Assert.Equal(3, builder.Count);
        }

        [Fact]
        public void Add_Chaining_Works()
        {
            var builder = new PromptDatasetBuilder()
                .Add("i1", "o1")
                .Add("i2", "o2");
            Assert.Equal(2, builder.Count);
        }

        // --- Get / Remove ---

        [Fact]
        public void Get_ExistingId_ReturnsExample()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "hello", "world"));
            var ex = builder.Get("e1");
            Assert.NotNull(ex);
            Assert.Equal("hello", ex!.Input);
        }

        [Fact]
        public void Get_NonExistentId_ReturnsNull()
        {
            var builder = new PromptDatasetBuilder();
            Assert.Null(builder.Get("nope"));
        }

        [Fact]
        public void Remove_ExistingId_DecreasesCount()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "hello"));
            Assert.True(builder.Remove("e1"));
            Assert.Equal(0, builder.Count);
        }

        [Fact]
        public void Remove_NonExistentId_ReturnsFalse()
        {
            var builder = new PromptDatasetBuilder();
            Assert.False(builder.Remove("nope"));
        }

        [Fact]
        public void RemoveWhere_FiltersCorrectly()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("short", "a").Add("this is a longer input", "b");
            int removed = builder.RemoveWhere(e => e.Input.Length < 10);
            Assert.Equal(1, removed);
            Assert.Equal(1, builder.Count);
        }

        // --- Quality Filtering ---

        [Fact]
        public void FilterByQuality_RemovesBelowThreshold()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("i1", "o1", ExampleQuality.Poor);
            builder.Add("i2", "o2", ExampleQuality.Good);
            builder.Add("i3", "o3", ExampleQuality.Excellent);
            int removed = builder.FilterByQuality(ExampleQuality.Good);
            Assert.Equal(1, removed);
            Assert.Equal(2, builder.Count);
        }

        [Fact]
        public void FilterByQuality_KeepsUnrated()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("i1", "o1"); // Unrated
            builder.Add("i2", "o2", ExampleQuality.Poor);
            builder.FilterByQuality(ExampleQuality.Good);
            Assert.Equal(1, builder.Count); // Unrated kept
            Assert.Equal("i1", builder.Examples[0].Input);
        }

        [Fact]
        public void RemoveEmptyOutputs_RemovesCorrectly()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "input1", "output1"));
            builder.Add(new DatasetExample("e2", "input2", ""));
            builder.Add(new DatasetExample("e3", "input3", "  "));
            int removed = builder.RemoveEmptyOutputs();
            Assert.Equal(2, removed);
            Assert.Equal(1, builder.Count);
        }

        // --- Tag Filtering ---

        [Fact]
        public void FilterByTags_KeepsMatchingExamples()
        {
            var builder = new PromptDatasetBuilder();
            var e1 = new DatasetExample("e1", "a", "b");
            e1.Tags.Add("math");
            var e2 = new DatasetExample("e2", "c", "d");
            e2.Tags.Add("code");
            var e3 = new DatasetExample("e3", "e", "f");
            e3.Tags.Add("math");
            e3.Tags.Add("code");
            builder.Add(e1).Add(e2).Add(e3);

            builder.FilterByTags(new[] { "math" });
            Assert.Equal(2, builder.Count);
        }

        [Fact]
        public void FilterByInputLength_RemovesLongInputs()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("short", "a").Add("this is a very long input string that exceeds the limit", "b");
            builder.FilterByInputLength(10);
            Assert.Equal(1, builder.Count);
        }

        // --- Deduplication ---

        [Fact]
        public void Deduplicate_RemovesExactDuplicates()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "hello world", "out1"));
            builder.Add(new DatasetExample("e2", "hello world", "out2"));
            var result = builder.Deduplicate();
            Assert.Equal(1, result.Removed);
            Assert.Equal(1, result.Remaining);
        }

        [Fact]
        public void Deduplicate_NormalizesWhitespace()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "hello   world", "out1"));
            builder.Add(new DatasetExample("e2", "Hello World", "out2"));
            var result = builder.Deduplicate();
            Assert.Equal(1, result.Removed);
        }

        [Fact]
        public void Deduplicate_KeepsHighestQuality()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "same input", "out1") { Quality = ExampleQuality.Poor });
            builder.Add(new DatasetExample("e2", "same input", "out2") { Quality = ExampleQuality.Excellent });
            builder.Deduplicate();
            Assert.Equal("e2", builder.Examples[0].Id);
        }

        [Fact]
        public void Deduplicate_NoDuplicates_NoChange()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("unique1", "o1").Add("unique2", "o2");
            var result = builder.Deduplicate();
            Assert.Equal(0, result.Removed);
            Assert.Equal(2, result.Remaining);
        }

        // --- Sampling ---

        [Fact]
        public void Sample_ReducesToRequestedCount()
        {
            var builder = new PromptDatasetBuilder();
            for (int i = 0; i < 20; i++) builder.Add($"input {i}", $"output {i}");
            builder.Sample(5, seed: 42);
            Assert.Equal(5, builder.Count);
        }

        [Fact]
        public void Sample_LargerThanCount_KeepsAll()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("a", "b").Add("c", "d");
            builder.Sample(10);
            Assert.Equal(2, builder.Count);
        }

        [Fact]
        public void Sample_Seeded_IsDeterministic()
        {
            var b1 = new PromptDatasetBuilder();
            var b2 = new PromptDatasetBuilder();
            for (int i = 0; i < 50; i++)
            {
                b1.Add(new DatasetExample($"e{i}", $"input {i}", $"output {i}"));
                b2.Add(new DatasetExample($"e{i}", $"input {i}", $"output {i}"));
            }
            b1.Sample(10, seed: 99);
            b2.Sample(10, seed: 99);
            Assert.Equal(b1.Examples.Select(e => e.Id), b2.Examples.Select(e => e.Id));
        }

        // --- Splitting ---

        [Fact]
        public void Split_AssignsAllExamples()
        {
            var builder = new PromptDatasetBuilder();
            for (int i = 0; i < 100; i++) builder.Add($"input {i}", $"output {i}");
            builder.Split(new SplitConfig { Seed = 42 });
            Assert.All(builder.Examples, e => Assert.NotNull(e.Split));
        }

        [Fact]
        public void Split_RespectRatios()
        {
            var builder = new PromptDatasetBuilder();
            for (int i = 0; i < 100; i++) builder.Add($"input {i}", $"output {i}");
            builder.Split(new SplitConfig { TrainRatio = 0.8, ValidationRatio = 0.1, TestRatio = 0.1, Seed = 42 });

            var counts = builder.Examples.GroupBy(e => e.Split).ToDictionary(g => g.Key, g => g.Count());
            Assert.InRange(counts[DatasetSplit.Train], 75, 85);
            Assert.InRange(counts[DatasetSplit.Validation], 5, 15);
            Assert.InRange(counts[DatasetSplit.Test], 5, 15);
        }

        [Fact]
        public void Split_InvalidRatios_Throws()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("a", "b");
            Assert.Throws<ArgumentException>(() => builder.Split(new SplitConfig { TrainRatio = 0.5, ValidationRatio = 0.1, TestRatio = 0.1 }));
        }

        [Fact]
        public void Split_Stratified_DistributesByQuality()
        {
            var builder = new PromptDatasetBuilder();
            for (int i = 0; i < 50; i++) builder.Add($"good {i}", $"o{i}", ExampleQuality.Good);
            for (int i = 0; i < 50; i++) builder.Add($"poor {i}", $"o{i}", ExampleQuality.Poor);
            builder.Split(new SplitConfig { Stratify = true, Seed = 42 });

            var trainGood = builder.Examples.Count(e => e.Split == DatasetSplit.Train && e.Quality == ExampleQuality.Good);
            var trainPoor = builder.Examples.Count(e => e.Split == DatasetSplit.Train && e.Quality == ExampleQuality.Poor);
            // Both should be roughly 80% of their respective groups
            Assert.InRange(trainGood, 35, 45);
            Assert.InRange(trainPoor, 35, 45);
        }

        [Fact]
        public void SplitConfig_IsValid_ChecksRatios()
        {
            Assert.True(new SplitConfig().IsValid);
            Assert.False(new SplitConfig { TrainRatio = 0.5 }.IsValid);
            Assert.False(new SplitConfig { TrainRatio = -0.1, ValidationRatio = 0.6, TestRatio = 0.5 }.IsValid);
        }

        // --- Token Estimation ---

        [Fact]
        public void EstimateTokens_SetsValues()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("four char", "also four char test");
            builder.EstimateTokens();
            Assert.True(builder.Examples[0].EstimatedTokens > 0);
        }

        // --- Stats ---

        [Fact]
        public void GetStats_EmptyDataset()
        {
            var stats = new PromptDatasetBuilder().GetStats();
            Assert.Equal(0, stats.TotalExamples);
        }

        [Fact]
        public void GetStats_ReturnsCorrectCounts()
        {
            var builder = new PromptDatasetBuilder();
            var e1 = new DatasetExample("e1", "hello", "world") { Quality = ExampleQuality.Good };
            e1.Tags.Add("math");
            var e2 = new DatasetExample("e2", "foo", "bar") { Quality = ExampleQuality.Good, Source = "test.jsonl" };
            e2.Tags.Add("math");
            e2.Tags.Add("code");
            builder.Add(e1).Add(e2);

            var stats = builder.GetStats();
            Assert.Equal(2, stats.TotalExamples);
            Assert.Equal(2, stats.QualityCounts[ExampleQuality.Good]);
            Assert.Equal(2, stats.TagCounts["math"]);
            Assert.Equal(1, stats.TagCounts["code"]);
            Assert.Equal(1, stats.UniqueSourceCount);
        }

        // --- Export Formats ---

        [Fact]
        public void Export_Json_ValidOutput()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("input1", "output1");
            var json = builder.Export(DatasetFormat.Json);
            Assert.Contains("input1", json);
            Assert.Contains("output1", json);
            Assert.StartsWith("[", json);
        }

        [Fact]
        public void Export_Jsonl_OnePerLine()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("i1", "o1").Add("i2", "o2");
            var jsonl = builder.Export(DatasetFormat.Jsonl);
            var lines = jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
        }

        [Fact]
        public void Export_Csv_HasHeaders()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("i1", "o1");
            var csv = builder.Export(DatasetFormat.Csv);
            Assert.StartsWith("id,input,output,system_prompt,quality,tags,source", csv);
        }

        [Fact]
        public void Export_ChatJsonl_HasMessages()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("You are helpful", "What is 2+2?", "4", ExampleQuality.Good);
            var output = builder.Export(DatasetFormat.ChatJsonl);
            Assert.Contains("\"role\":\"system\"", output);
            Assert.Contains("\"role\":\"user\"", output);
            Assert.Contains("\"role\":\"assistant\"", output);
        }

        [Fact]
        public void Export_Alpaca_HasInstructionFormat()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("Be concise", "What is AI?", "Artificial intelligence", ExampleQuality.Good);
            var output = builder.Export(DatasetFormat.Alpaca);
            Assert.Contains("instruction", output);
            Assert.Contains("input", output);
            Assert.Contains("output", output);
        }

        [Fact]
        public void Export_BySplit_FiltersCorrectly()
        {
            var builder = new PromptDatasetBuilder();
            for (int i = 0; i < 20; i++) builder.Add($"input {i}", $"output {i}");
            builder.Split(new SplitConfig { TrainRatio = 0.5, ValidationRatio = 0.25, TestRatio = 0.25, Seed = 42 });

            var trainJsonl = builder.Export(DatasetFormat.Jsonl, DatasetSplit.Train);
            var trainLines = trainJsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(builder.Examples.Count(e => e.Split == DatasetSplit.Train), trainLines);
        }

        // --- Import ---

        [Fact]
        public void ImportJsonl_ParsesCorrectly()
        {
            var jsonl = "{\"id\":\"t1\",\"input\":\"hello\",\"output\":\"world\",\"quality\":\"Good\",\"tags\":[\"math\"]}\n{\"input\":\"foo\",\"output\":\"bar\"}";
            var builder = new PromptDatasetBuilder().ImportJsonl(jsonl);
            Assert.Equal(2, builder.Count);
            Assert.Equal(ExampleQuality.Good, builder.Get("t1")!.Quality);
            Assert.Contains("math", builder.Get("t1")!.Tags);
        }

        [Fact]
        public void ImportJsonl_SkipsEmptyInputs()
        {
            var jsonl = "{\"input\":\"\",\"output\":\"bar\"}\n{\"input\":\"valid\",\"output\":\"ok\"}";
            var builder = new PromptDatasetBuilder().ImportJsonl(jsonl);
            Assert.Equal(1, builder.Count);
        }

        [Fact]
        public void ImportJsonl_EmptyString_NoEffect()
        {
            var builder = new PromptDatasetBuilder().ImportJsonl("");
            Assert.Equal(0, builder.Count);
        }

        // --- Serialize ---

        [Fact]
        public void Serialize_RoundTrips()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add("sys", "in", "out", ExampleQuality.Good);
            var serialized = builder.Serialize();
            Assert.Contains("\"input\": \"in\"", serialized);
            Assert.Contains("\"quality\": \"Good\"", serialized);
        }

        // --- Report ---

        [Fact]
        public void Report_EmptyDataset()
        {
            var report = new PromptDatasetBuilder().Report();
            Assert.Contains("Total examples: 0", report);
            Assert.Contains("(empty dataset)", report);
        }

        [Fact]
        public void Report_WithData_IncludesAllSections()
        {
            var builder = new PromptDatasetBuilder();
            var e1 = new DatasetExample("e1", "hello world", "response here") { Quality = ExampleQuality.Good, Source = "test" };
            e1.Tags.Add("math");
            builder.Add(e1);
            builder.EstimateTokens();
            builder.Split(new SplitConfig { TrainRatio = 1.0, ValidationRatio = 0, TestRatio = 0, Seed = 1 });

            var report = builder.Report();
            Assert.Contains("Dataset Report", report);
            Assert.Contains("Quality Distribution", report);
            Assert.Contains("Good: 1", report);
            Assert.Contains("Split Distribution", report);
            Assert.Contains("Tags", report);
            Assert.Contains("math", report);
            Assert.Contains("Length Statistics", report);
        }

        // --- DatasetExample ---

        [Fact]
        public void DatasetExample_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DatasetExample("", "input"));
        }

        [Fact]
        public void DatasetExample_EmptyInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DatasetExample("e1", ""));
        }

        [Fact]
        public void DatasetExample_DefaultValues()
        {
            var ex = new DatasetExample("e1", "hello");
            Assert.Equal("", ex.Output);
            Assert.Equal("", ex.SystemPrompt);
            Assert.Equal(ExampleQuality.Unrated, ex.Quality);
            Assert.Empty(ex.Tags);
            Assert.Empty(ex.Metadata);
            Assert.Null(ex.Split);
        }

        // --- CSV Escaping ---

        [Fact]
        public void Export_Csv_EscapesQuotesAndCommas()
        {
            var builder = new PromptDatasetBuilder();
            builder.Add(new DatasetExample("e1", "hello, world", "say \"hi\""));
            var csv = builder.Export(DatasetFormat.Csv);
            Assert.Contains("\"hello, world\"", csv);
            Assert.Contains("\"say \"\"hi\"\"\"", csv);
        }

        // --- End-to-end pipeline ---

        [Fact]
        public void FullPipeline_AddFilterDeduplicateSplitExport()
        {
            var builder = new PromptDatasetBuilder();

            // Add examples of varying quality
            builder.Add("What is 2+2?", "4", ExampleQuality.Excellent);
            builder.Add("What is 2+2?", "Four", ExampleQuality.Good); // exact duplicate input
            builder.Add("Capital of France?", "Paris", ExampleQuality.Good);
            builder.Add("bad example", "", ExampleQuality.Poor);
            builder.Add("another bad", "  ", ExampleQuality.Poor);
            builder.Add("Explain gravity", "Force of attraction between masses", ExampleQuality.Excellent);

            // Pipeline
            builder.RemoveEmptyOutputs();
            Assert.Equal(4, builder.Count);

            builder.Deduplicate();
            Assert.Equal(3, builder.Count);

            builder.EstimateTokens();
            Assert.All(builder.Examples, e => Assert.True(e.EstimatedTokens > 0));

            builder.Split(new SplitConfig { TrainRatio = 0.7, ValidationRatio = 0.15, TestRatio = 0.15, Seed = 42 });
            Assert.All(builder.Examples, e => Assert.NotNull(e.Split));

            var jsonl = builder.Export(DatasetFormat.Jsonl);
            Assert.False(string.IsNullOrWhiteSpace(jsonl));

            var report = builder.Report();
            Assert.Contains("Dataset Report", report);
        }
    }
}
