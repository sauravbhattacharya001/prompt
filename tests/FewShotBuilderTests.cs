namespace Prompt.Tests
{
    using Xunit;
    using System.Text.Json;

    public class FewShotBuilderTests
    {
        // ──────────── Constructor & Defaults ────────────

        [Fact]
        public void Constructor_NoArgs_EmptyBuilder()
        {
            var builder = new FewShotBuilder();
            Assert.Equal(0, builder.ExampleCount);
            Assert.Null(builder.TaskDescription);
            Assert.Equal(FewShotFormat.Labeled, builder.Format);
            Assert.Equal("Input", builder.InputLabel);
            Assert.Equal("Output", builder.OutputLabel);
            Assert.Equal("\n\n", builder.Separator);
            Assert.Null(builder.SystemContext);
        }

        [Fact]
        public void Constructor_WithTaskDescription()
        {
            var builder = new FewShotBuilder("Classify sentiment.");
            Assert.Equal("Classify sentiment.", builder.TaskDescription);
        }

        // ──────────── Example Management ────────────

        [Fact]
        public void AddExample_StringArgs_AddsExample()
        {
            var builder = new FewShotBuilder()
                .AddExample("hello", "world");

            Assert.Equal(1, builder.ExampleCount);
            Assert.Equal("hello", builder.Examples[0].Input);
            Assert.Equal("world", builder.Examples[0].Output);
            Assert.Null(builder.Examples[0].Label);
        }

        [Fact]
        public void AddExample_WithLabel_StoresLabel()
        {
            var builder = new FewShotBuilder()
                .AddExample("hello", "world", "greeting");

            Assert.Equal("greeting", builder.Examples[0].Label);
        }

        [Fact]
        public void AddExample_Object_AddsExample()
        {
            var ex = new FewShotExample("a", "b", "c");
            var builder = new FewShotBuilder().AddExample(ex);

            Assert.Equal(1, builder.ExampleCount);
            Assert.Same(ex, builder.Examples[0]);
        }

        [Fact]
        public void AddExample_NullObject_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentNullException>(() => builder.AddExample((FewShotExample)null!));
        }

        [Fact]
        public void AddExamples_Multiple_AddsAll()
        {
            var examples = new[]
            {
                new FewShotExample("a", "1"),
                new FewShotExample("b", "2"),
                new FewShotExample("c", "3")
            };

            var builder = new FewShotBuilder().AddExamples(examples);
            Assert.Equal(3, builder.ExampleCount);
        }

        [Fact]
        public void AddExamples_Null_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentNullException>(() => builder.AddExamples(null!));
        }

        [Fact]
        public void RemoveExample_ValidIndex_Removes()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2")
                .AddExample("c", "3")
                .RemoveExample(1);

            Assert.Equal(2, builder.ExampleCount);
            Assert.Equal("a", builder.Examples[0].Input);
            Assert.Equal("c", builder.Examples[1].Input);
        }

        [Fact]
        public void RemoveExample_InvalidIndex_Throws()
        {
            var builder = new FewShotBuilder().AddExample("a", "1");
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.RemoveExample(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.RemoveExample(-1));
        }

        [Fact]
        public void ClearExamples_RemovesAll()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2")
                .ClearExamples();

            Assert.Equal(0, builder.ExampleCount);
        }

        [Fact]
        public void ShuffleExamples_WithSeed_Deterministic()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2")
                .AddExample("c", "3")
                .AddExample("d", "4")
                .AddExample("e", "5");

            // Take snapshot of original order
            var original = builder.Examples.Select(e => e.Input).ToList();

            builder.ShuffleExamples(seed: 42);
            var shuffled1 = builder.Examples.Select(e => e.Input).ToList();

            // Reshuffle with same seed should give same order
            // Need to re-add in original order first
            builder.ClearExamples();
            foreach (var input in original)
                builder.AddExample(input, "x");
            builder.ShuffleExamples(seed: 42);
            var shuffled2 = builder.Examples.Select(e => e.Input).ToList();

            Assert.Equal(shuffled1, shuffled2);
        }

        [Fact]
        public void ShuffleExamples_PreservesCount()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2")
                .AddExample("c", "3")
                .ShuffleExamples();

            Assert.Equal(3, builder.ExampleCount);
        }

        [Fact]
        public void ReorderExamples_ReverseOrder()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2")
                .AddExample("c", "3")
                .ReorderExamples(2, 1, 0);

            Assert.Equal("c", builder.Examples[0].Input);
            Assert.Equal("b", builder.Examples[1].Input);
            Assert.Equal("a", builder.Examples[2].Input);
        }

        [Fact]
        public void ReorderExamples_WrongCount_Throws()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2");

            Assert.Throws<ArgumentException>(() => builder.ReorderExamples(0));
        }

        [Fact]
        public void ReorderExamples_DuplicateIndex_Throws()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2");

            Assert.Throws<ArgumentException>(() => builder.ReorderExamples(0, 0));
        }

        [Fact]
        public void ReorderExamples_OutOfRange_Throws()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2");

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.ReorderExamples(0, 5));
        }

        [Fact]
        public void ReorderExamples_Null_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentNullException>(() => builder.ReorderExamples(null!));
        }

        [Fact]
        public void GetExamplesByLabel_FiltersCorrectly()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1", "math")
                .AddExample("b", "2", "science")
                .AddExample("c", "3", "math")
                .AddExample("d", "4");

            var mathExamples = builder.GetExamplesByLabel("math");
            Assert.Equal(2, mathExamples.Count);
            Assert.All(mathExamples, e => Assert.Equal("math", e.Label));
        }

        [Fact]
        public void GetExamplesByLabel_CaseInsensitive()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1", "Math")
                .AddExample("b", "2", "MATH");

            Assert.Equal(2, builder.GetExamplesByLabel("math").Count);
        }

        [Fact]
        public void GetExamplesByLabel_Null_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentNullException>(() => builder.GetExamplesByLabel(null!));
        }

        [Fact]
        public void GetLabels_ReturnsDistinctSorted()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1", "beta")
                .AddExample("b", "2", "alpha")
                .AddExample("c", "3", "beta")
                .AddExample("d", "4"); // no label

            var labels = builder.GetLabels();
            Assert.Equal(2, labels.Count);
            Assert.Equal("alpha", labels[0]);
            Assert.Equal("beta", labels[1]);
        }

        [Fact]
        public void MaxExamples_Enforced()
        {
            var builder = new FewShotBuilder();
            for (int i = 0; i < FewShotBuilder.MaxExamples; i++)
                builder.AddExample($"input{i}", $"output{i}");

            Assert.Throws<InvalidOperationException>(
                () => builder.AddExample("overflow", "boom"));
        }

        // ──────────── Configuration ────────────

        [Fact]
        public void WithTaskDescription_Updates()
        {
            var builder = new FewShotBuilder("old")
                .WithTaskDescription("new");
            Assert.Equal("new", builder.TaskDescription);
        }

        [Fact]
        public void WithTaskDescription_Null_ClearsIt()
        {
            var builder = new FewShotBuilder("old")
                .WithTaskDescription(null);
            Assert.Null(builder.TaskDescription);
        }

        [Fact]
        public void WithInputLabel_Updates()
        {
            var builder = new FewShotBuilder()
                .WithInputLabel("Question");
            Assert.Equal("Question", builder.InputLabel);
        }

        [Fact]
        public void WithInputLabel_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new FewShotBuilder().WithInputLabel(null!));
        }

        [Fact]
        public void WithOutputLabel_Updates()
        {
            var builder = new FewShotBuilder()
                .WithOutputLabel("Answer");
            Assert.Equal("Answer", builder.OutputLabel);
        }

        [Fact]
        public void WithSeparator_Updates()
        {
            var builder = new FewShotBuilder()
                .WithSeparator("---\n");
            Assert.Equal("---\n", builder.Separator);
        }

        [Fact]
        public void WithFormat_Updates()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Xml);
            Assert.Equal(FewShotFormat.Xml, builder.Format);
        }

        [Fact]
        public void WithSystemContext_Updates()
        {
            var builder = new FewShotBuilder()
                .WithSystemContext("You are an expert.");
            Assert.Equal("You are an expert.", builder.SystemContext);
        }

        [Fact]
        public void FluentChaining_AllMethods()
        {
            var builder = new FewShotBuilder("task")
                .WithSystemContext("context")
                .WithInputLabel("Q")
                .WithOutputLabel("A")
                .WithSeparator("\n---\n")
                .WithFormat(FewShotFormat.ChatStyle)
                .AddExample("q1", "a1")
                .AddExample("q2", "a2");

            Assert.Equal("task", builder.TaskDescription);
            Assert.Equal("context", builder.SystemContext);
            Assert.Equal("Q", builder.InputLabel);
            Assert.Equal("A", builder.OutputLabel);
            Assert.Equal(FewShotFormat.ChatStyle, builder.Format);
            Assert.Equal(2, builder.ExampleCount);
        }

        // ──────────── Build — Labeled Format ────────────

        [Fact]
        public void Build_Labeled_NoQuery()
        {
            var builder = new FewShotBuilder("Classify sentiment.")
                .AddExample("I love it!", "positive")
                .AddExample("I hate it.", "negative");

            var result = builder.Build();
            Assert.Contains("Classify sentiment.", result);
            Assert.Contains("Input: I love it!", result);
            Assert.Contains("Output: positive", result);
            Assert.Contains("Input: I hate it.", result);
            Assert.Contains("Output: negative", result);
        }

        [Fact]
        public void Build_Labeled_WithQuery()
        {
            var builder = new FewShotBuilder("Classify sentiment.")
                .AddExample("I love it!", "positive");

            var result = builder.Build("It's okay.");
            Assert.Contains("Input: It's okay.", result);
            Assert.Contains("Output:", result);
            Assert.EndsWith("Output:", result);
        }

        [Fact]
        public void Build_NoExamples_NoQuery()
        {
            var builder = new FewShotBuilder("Just a description.");
            var result = builder.Build();
            Assert.Equal("Just a description.", result);
        }

        [Fact]
        public void Build_NoExamples_WithQuery()
        {
            var builder = new FewShotBuilder("Answer the question.");
            var result = builder.Build("What is 2+2?");
            Assert.Contains("Answer the question.", result);
            Assert.Contains("Input: What is 2+2?", result);
        }

        [Fact]
        public void Build_NoDescription_NoQuery()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1");

            var result = builder.Build();
            Assert.Equal("Input: a\r\nOutput: 1", result);
        }

        [Fact]
        public void Build_WithSystemContext()
        {
            var builder = new FewShotBuilder("Translate to French.")
                .WithSystemContext("You are a professional translator.")
                .AddExample("Hello", "Bonjour");

            var result = builder.Build("Goodbye");
            Assert.StartsWith("You are a professional translator.", result);
            Assert.Contains("Translate to French.", result);
            Assert.Contains("Input: Hello", result);
        }

        [Fact]
        public void Build_WithLabels_ShowsLabels()
        {
            var builder = new FewShotBuilder()
                .AddExample("2+2", "4", "arithmetic")
                .AddExample("dog", "animal", "classification");

            var result = builder.Build();
            Assert.Contains("[arithmetic]", result);
            Assert.Contains("[classification]", result);
        }

        [Fact]
        public void Build_CustomLabels()
        {
            var builder = new FewShotBuilder()
                .WithInputLabel("Question")
                .WithOutputLabel("Answer")
                .AddExample("What is 2+2?", "4");

            var result = builder.Build("What is 3+3?");
            Assert.Contains("Question: What is 2+2?", result);
            Assert.Contains("Answer: 4", result);
            Assert.Contains("Question: What is 3+3?", result);
        }

        // ──────────── Build — Chat Format ────────────

        [Fact]
        public void Build_ChatStyle()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.ChatStyle)
                .AddExample("Hi", "Hello!");

            var result = builder.Build("How are you?");
            Assert.Contains("User: Hi", result);
            Assert.Contains("Assistant: Hello!", result);
            Assert.Contains("User: How are you?", result);
            Assert.EndsWith("Assistant:", result);
        }

        // ──────────── Build — Minimal Format ────────────

        [Fact]
        public void Build_Minimal()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Minimal)
                .AddExample("2+2", "4")
                .AddExample("3+3", "6");

            var result = builder.Build("5+5");
            Assert.Contains("2+2 => 4", result);
            Assert.Contains("3+3 => 6", result);
            Assert.Contains("5+5 =>", result);
        }

        [Fact]
        public void Build_Minimal_WithLabel()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Minimal)
                .AddExample("2+2", "4", "math");

            var result = builder.Build();
            Assert.Contains("[math] 2+2 => 4", result);
        }

        // ──────────── Build — Numbered Format ────────────

        [Fact]
        public void Build_Numbered()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Numbered)
                .AddExample("a", "1")
                .AddExample("b", "2");

            var result = builder.Build("c");
            Assert.Contains("Example 1:", result);
            Assert.Contains("Example 2:", result);
            Assert.Contains("Now answer:", result);
        }

        [Fact]
        public void Build_Numbered_WithLabel()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Numbered)
                .AddExample("a", "1", "category_A");

            var result = builder.Build();
            Assert.Contains("Category: category_A", result);
        }

        // ──────────── Build — XML Format ────────────

        [Fact]
        public void Build_Xml()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Xml)
                .AddExample("Hello", "Bonjour");

            var result = builder.Build("Goodbye");
            Assert.Contains("<example>", result);
            Assert.Contains("<input>Hello</input>", result);
            Assert.Contains("<output>Bonjour</output>", result);
            Assert.Contains("<query>", result);
            Assert.Contains("<input>Goodbye</input>", result);
        }

        [Fact]
        public void Build_Xml_EscapesSpecialChars()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Xml)
                .AddExample("a < b & c > d", "true");

            var result = builder.Build();
            Assert.Contains("a &lt; b &amp; c &gt; d", result);
        }

        [Fact]
        public void Build_Xml_WithLabel()
        {
            var builder = new FewShotBuilder()
                .WithFormat(FewShotFormat.Xml)
                .AddExample("Hello", "Bonjour", "translation");

            var result = builder.Build();
            Assert.Contains("<label>translation</label>", result);
        }

        // ──────────── Token Budget ────────────

        [Fact]
        public void BuildWithTokenLimit_AllFit_ReturnsAll()
        {
            var builder = new FewShotBuilder("Task.")
                .AddExample("a", "1")
                .AddExample("b", "2");

            var result = builder.BuildWithTokenLimit("c", 1000);
            Assert.Contains("a", result);
            Assert.Contains("b", result);
            Assert.Contains("c", result);
        }

        [Fact]
        public void BuildWithTokenLimit_TightBudget_ReducesExamples()
        {
            var builder = new FewShotBuilder("Classify.")
                .AddExample("This is a very long example input that takes many tokens", "output1")
                .AddExample("Another long example with lots of words in it", "output2")
                .AddExample("Short", "ok");

            // Very tight budget — should remove some examples
            var fullTokens = builder.EstimateTokens("query");
            var result = builder.BuildWithTokenLimit("query", fullTokens / 2);

            Assert.Contains("query", result);
            // At minimum should have fewer examples than full build
            var fullBuild = builder.Build("query");
            Assert.True(result.Length <= fullBuild.Length);
        }

        [Fact]
        public void BuildWithTokenLimit_VeryTight_NoExamples()
        {
            var builder = new FewShotBuilder("Classify.")
                .AddExample("This is a very long example input", "long output");

            // Budget too small for any examples
            var result = builder.BuildWithTokenLimit("query", 5);
            Assert.Contains("query", result);
        }

        [Fact]
        public void BuildWithTokenLimit_NullQuery_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentNullException>(
                () => builder.BuildWithTokenLimit(null!, 100));
        }

        [Fact]
        public void BuildWithTokenLimit_ZeroTokens_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => builder.BuildWithTokenLimit("q", 0));
        }

        // ──────────── Build With Labels ────────────

        [Fact]
        public void BuildWithLabels_FiltersCorrectly()
        {
            var builder = new FewShotBuilder("Classify.")
                .AddExample("2+2", "4", "math")
                .AddExample("dog", "animal", "bio")
                .AddExample("3+3", "6", "math");

            var result = builder.BuildWithLabels(new[] { "math" }, "5+5");
            Assert.Contains("2+2", result);
            Assert.Contains("3+3", result);
            Assert.DoesNotContain("dog", result);
        }

        [Fact]
        public void BuildWithLabels_NoMatches_EmptyExamples()
        {
            var builder = new FewShotBuilder("Do the thing.")
                .AddExample("zebra", "1", "math");

            var result = builder.BuildWithLabels(new[] { "nonexistent" }, "query");
            Assert.DoesNotContain("zebra", result);
            Assert.Contains("query", result);
        }

        [Fact]
        public void BuildWithLabels_Null_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentNullException>(
                () => builder.BuildWithLabels(null!));
        }

        // ──────────── Build With Random Selection ────────────

        [Fact]
        public void BuildWithRandomSelection_CorrectCount()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2")
                .AddExample("c", "3")
                .AddExample("d", "4")
                .AddExample("e", "5");

            // Select 2 out of 5, with seed for determinism
            var result = builder.BuildWithRandomSelection(2, seed: 42);
            // Count how many example inputs appear
            int found = 0;
            foreach (var ex in builder.Examples)
                if (result.Contains(ex.Input)) found++;

            Assert.Equal(2, found);
        }

        [Fact]
        public void BuildWithRandomSelection_CountExceedsTotal_ReturnsAll()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2");

            var result = builder.BuildWithRandomSelection(10);
            Assert.Contains("a", result);
            Assert.Contains("b", result);
        }

        [Fact]
        public void BuildWithRandomSelection_WithSeed_Deterministic()
        {
            var builder = new FewShotBuilder()
                .AddExample("a", "1")
                .AddExample("b", "2")
                .AddExample("c", "3")
                .AddExample("d", "4");

            var r1 = builder.BuildWithRandomSelection(2, "q", seed: 123);
            var r2 = builder.BuildWithRandomSelection(2, "q", seed: 123);
            Assert.Equal(r1, r2);
        }

        [Fact]
        public void BuildWithRandomSelection_NegativeCount_Throws()
        {
            var builder = new FewShotBuilder();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => builder.BuildWithRandomSelection(-1));
        }

        // ──────────── Token Estimation ────────────

        [Fact]
        public void EstimateTokens_ReturnsPositive()
        {
            var builder = new FewShotBuilder("Classify sentiment.")
                .AddExample("Great product!", "positive")
                .AddExample("Terrible.", "negative");

            Assert.True(builder.EstimateTokens() > 0);
            Assert.True(builder.EstimateTokens("How is this?") > builder.EstimateTokens());
        }

        [Fact]
        public void FewShotExample_EstimateTokens_ReturnsPositive()
        {
            var ex = new FewShotExample("Hello world", "Bonjour le monde");
            Assert.True(ex.EstimateTokens() > 0);
        }

        // ──────────── FewShotExample Validation ────────────

        [Fact]
        public void FewShotExample_NullInput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FewShotExample(null!, "out"));
        }

        [Fact]
        public void FewShotExample_NullOutput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FewShotExample("in", null!));
        }

        // ──────────── Custom Separator ────────────

        [Fact]
        public void Build_CustomSeparator()
        {
            var builder = new FewShotBuilder()
                .WithSeparator("\n---\n")
                .AddExample("a", "1")
                .AddExample("b", "2");

            var result = builder.Build();
            Assert.Contains("---", result);
        }

        // ──────────── Serialization ────────────

        [Fact]
        public void ToJson_FromJson_RoundTrip()
        {
            var original = new FewShotBuilder("Classify sentiment.")
                .WithSystemContext("You are an expert.")
                .WithInputLabel("Text")
                .WithOutputLabel("Sentiment")
                .WithSeparator("\n---\n")
                .WithFormat(FewShotFormat.Xml)
                .AddExample("Great!", "positive", "review")
                .AddExample("Bad.", "negative");

            var json = original.ToJson();
            var restored = FewShotBuilder.FromJson(json);

            Assert.Equal(original.TaskDescription, restored.TaskDescription);
            Assert.Equal(original.SystemContext, restored.SystemContext);
            Assert.Equal(original.InputLabel, restored.InputLabel);
            Assert.Equal(original.OutputLabel, restored.OutputLabel);
            Assert.Equal(original.Separator, restored.Separator);
            Assert.Equal(original.Format, restored.Format);
            Assert.Equal(original.ExampleCount, restored.ExampleCount);
            Assert.Equal(original.Examples[0].Input, restored.Examples[0].Input);
            Assert.Equal(original.Examples[0].Label, restored.Examples[0].Label);
            Assert.Equal(original.Examples[1].Input, restored.Examples[1].Input);
            Assert.Null(restored.Examples[1].Label);
        }

        [Fact]
        public void ToJson_ValidJson()
        {
            var builder = new FewShotBuilder("task")
                .AddExample("a", "1");

            var json = builder.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal("task", doc.RootElement.GetProperty("taskDescription").GetString());
        }

        [Fact]
        public void FromJson_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => FewShotBuilder.FromJson(null!));
            Assert.Throws<ArgumentException>(() => FewShotBuilder.FromJson(""));
        }

        [Fact]
        public void FromJson_InvalidJson_Throws()
        {
            Assert.Throws<JsonException>(() => FewShotBuilder.FromJson("{invalid"));
        }

        [Fact]
        public void FromJson_TooManyExamples_Throws()
        {
            // Build JSON with more than MaxExamples
            var examples = new List<object>();
            for (int i = 0; i <= FewShotBuilder.MaxExamples; i++)
                examples.Add(new { input = "x", output = "y" });

            var json = JsonSerializer.Serialize(new { examples });
            Assert.Throws<JsonException>(() => FewShotBuilder.FromJson(json));
        }

        [Fact]
        public async Task SaveToFileAsync_LoadFromFileAsync_RoundTrip()
        {
            var builder = new FewShotBuilder("task")
                .AddExample("a", "1", "label");

            var path = Path.GetTempFileName();
            try
            {
                await builder.SaveToFileAsync(path);
                var loaded = await FewShotBuilder.LoadFromFileAsync(path);

                Assert.Equal("task", loaded.TaskDescription);
                Assert.Equal(1, loaded.ExampleCount);
                Assert.Equal("a", loaded.Examples[0].Input);
                Assert.Equal("label", loaded.Examples[0].Label);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task SaveToFileAsync_EmptyPath_Throws()
        {
            var builder = new FewShotBuilder();
            await Assert.ThrowsAsync<ArgumentException>(
                () => builder.SaveToFileAsync(""));
        }

        [Fact]
        public async Task LoadFromFileAsync_EmptyPath_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => FewShotBuilder.LoadFromFileAsync(""));
        }

        // ──────────── Build Output Consistency ────────────

        [Fact]
        public void Build_ProducesConsistentOutput()
        {
            var builder = new FewShotBuilder("Translate English to French.")
                .AddExample("Hello", "Bonjour")
                .AddExample("Thank you", "Merci");

            var r1 = builder.Build("Goodbye");
            var r2 = builder.Build("Goodbye");
            Assert.Equal(r1, r2);
        }

        [Fact]
        public void Build_EmptyBuilderNoDescription()
        {
            var builder = new FewShotBuilder();
            var result = builder.Build();
            Assert.Equal("", result);
        }

        [Fact]
        public void Build_EmptyBuilderWithQuery()
        {
            var builder = new FewShotBuilder();
            var result = builder.Build("test");
            Assert.Contains("test", result);
        }

        // ──────────── Edge Cases ────────────

        [Fact]
        public void Build_MultiLineInputOutput()
        {
            var builder = new FewShotBuilder()
                .AddExample("line1\nline2", "out1\nout2");

            var result = builder.Build();
            Assert.Contains("line1\nline2", result);
            Assert.Contains("out1\nout2", result);
        }

        [Fact]
        public void Build_SpecialCharacters()
        {
            var builder = new FewShotBuilder()
                .AddExample("What's 2+2?", "It's 4!");

            var result = builder.Build();
            Assert.Contains("What's 2+2?", result);
            Assert.Contains("It's 4!", result);
        }

        [Fact]
        public void Build_UnicodeContent()
        {
            var builder = new FewShotBuilder()
                .AddExample("日本語", "Japanese");

            var result = builder.Build();
            Assert.Contains("日本語", result);
        }

        [Fact]
        public void Build_AllFormats_NonEmpty()
        {
            foreach (FewShotFormat fmt in Enum.GetValues<FewShotFormat>())
            {
                var builder = new FewShotBuilder("task")
                    .WithFormat(fmt)
                    .AddExample("in", "out");

                var result = builder.Build("query");
                Assert.False(string.IsNullOrEmpty(result), $"Format {fmt} produced empty output");
                Assert.Contains("in", result);
                Assert.Contains("out", result);
            }
        }

        [Fact]
        public void Build_Separator_AppliedBetweenExamples()
        {
            var builder = new FewShotBuilder()
                .WithSeparator("|||")
                .AddExample("a", "1")
                .AddExample("b", "2");

            var result = builder.Build();
            Assert.Contains("|||", result);
        }

        [Fact]
        public void BuildWithRandomSelection_Zero_NoExamples()
        {
            var builder = new FewShotBuilder("Do the thing.")
                .AddExample("zebra", "1")
                .AddExample("giraffe", "2");

            var result = builder.BuildWithRandomSelection(0, "myquery");
            Assert.DoesNotContain("zebra", result);
            Assert.DoesNotContain("giraffe", result);
            Assert.Contains("myquery", result);
        }

        // ──────────── End-to-End Scenario ────────────

        [Fact]
        public void EndToEnd_SentimentClassifier()
        {
            var builder = new FewShotBuilder("Classify the sentiment as positive, negative, or neutral.")
                .WithSystemContext("You are a sentiment analysis model.")
                .WithFormat(FewShotFormat.Labeled)
                .AddExample("This product is amazing!", "positive", "product")
                .AddExample("Worst purchase ever.", "negative", "product")
                .AddExample("It works as expected.", "neutral", "product")
                .AddExample("The movie was incredible!", "positive", "movie")
                .AddExample("Boring film.", "negative", "movie");

            // Full build
            var full = builder.Build("The restaurant was decent.");
            Assert.Contains("sentiment analysis", full);
            Assert.Contains("Classify the sentiment", full);
            Assert.Contains("amazing", full);
            Assert.Contains("The restaurant was decent.", full);

            // Build with only "product" examples
            var productOnly = builder.BuildWithLabels(new[] { "product" }, "Nice quality.");
            Assert.Contains("amazing", productOnly);
            Assert.DoesNotContain("movie", productOnly);
            Assert.DoesNotContain("film", productOnly);

            // Token estimation
            Assert.True(builder.EstimateTokens("test") > 0);

            // Serialization round-trip
            var json = builder.ToJson();
            var restored = FewShotBuilder.FromJson(json);
            Assert.Equal(5, restored.ExampleCount);
            Assert.Equal(full, restored.Build("The restaurant was decent."));
        }

        [Fact]
        public void EndToEnd_CodeTranslation_XmlFormat()
        {
            var builder = new FewShotBuilder("Translate Python to JavaScript.")
                .WithFormat(FewShotFormat.Xml)
                .AddExample("print('hello')", "console.log('hello')")
                .AddExample("len(arr)", "arr.length");

            var result = builder.Build("x = [1, 2, 3]");
            Assert.Contains("<example>", result);
            Assert.Contains("<query>", result);
            Assert.Contains("x = [1, 2, 3]", result);
        }
    }
}
