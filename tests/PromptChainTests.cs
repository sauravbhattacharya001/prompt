namespace Prompt.Tests
{
    using System.Text.Json;
    using Xunit;

    public class ChainStepTests
    {
        [Fact]
        public void Constructor_ValidArgs_SetsProperties()
        {
            var template = new PromptTemplate("Hello {{name}}");
            var step = new ChainStep("greet", template, "greeting");

            Assert.Equal("greet", step.Name);
            Assert.Same(template, step.Template);
            Assert.Equal("greeting", step.OutputVariable);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_NullOrEmptyName_Throws(string? name)
        {
            var template = new PromptTemplate("test");
            Assert.Throws<ArgumentException>(
                () => new ChainStep(name!, template, "out"));
        }

        [Fact]
        public void Constructor_NullTemplate_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ChainStep("step", null!, "out"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_NullOrEmptyOutputVariable_Throws(string? outVar)
        {
            var template = new PromptTemplate("test");
            Assert.Throws<ArgumentException>(
                () => new ChainStep("step", template, outVar!));
        }
    }

    public class PromptChainTests
    {
        [Fact]
        public void NewChain_HasZeroSteps()
        {
            var chain = new PromptChain();
            Assert.Equal(0, chain.StepCount);
            Assert.Empty(chain.Steps);
        }

        [Fact]
        public void AddStep_IncrementsCount()
        {
            var chain = new PromptChain()
                .AddStep("s1", new PromptTemplate("t1"), "out1")
                .AddStep("s2", new PromptTemplate("t2"), "out2");

            Assert.Equal(2, chain.StepCount);
        }

        [Fact]
        public void AddStep_ReturnsSameInstance_ForFluentChaining()
        {
            var chain = new PromptChain();
            var result = chain.AddStep("s1", new PromptTemplate("t1"), "out1");
            Assert.Same(chain, result);
        }

        [Fact]
        public void AddStep_DuplicateOutputVariable_Throws()
        {
            var chain = new PromptChain()
                .AddStep("s1", new PromptTemplate("t1"), "output");

            Assert.Throws<ArgumentException>(() =>
                chain.AddStep("s2", new PromptTemplate("t2"), "output"));
        }

        [Fact]
        public void AddStep_DuplicateOutputVariable_CaseInsensitive_Throws()
        {
            var chain = new PromptChain()
                .AddStep("s1", new PromptTemplate("t1"), "Output");

            Assert.Throws<ArgumentException>(() =>
                chain.AddStep("s2", new PromptTemplate("t2"), "OUTPUT"));
        }

        [Fact]
        public void WithSystemPrompt_ReturnsSameInstance()
        {
            var chain = new PromptChain();
            var result = chain.WithSystemPrompt("You are helpful.");
            Assert.Same(chain, result);
        }

        [Fact]
        public void WithMaxRetries_ReturnsSameInstance()
        {
            var chain = new PromptChain();
            var result = chain.WithMaxRetries(5);
            Assert.Same(chain, result);
        }

        [Fact]
        public void WithMaxRetries_Negative_Throws()
        {
            var chain = new PromptChain();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => chain.WithMaxRetries(-1));
        }

        [Fact]
        public async Task RunAsync_EmptyChain_Throws()
        {
            var chain = new PromptChain();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => chain.RunAsync());
        }

        [Fact]
        public void Steps_ReturnsReadOnlyView()
        {
            var chain = new PromptChain()
                .AddStep("s1", new PromptTemplate("t1"), "out1");

            var steps = chain.Steps;
            Assert.Equal(1, steps.Count);
            Assert.Equal("s1", steps[0].Name);
        }

        // ──────────────── Validation ────────────────

        [Fact]
        public void Validate_EmptyChain_ReturnsError()
        {
            var chain = new PromptChain();
            var errors = chain.Validate();
            Assert.Single(errors);
            Assert.Contains("no steps", errors[0]);
        }

        [Fact]
        public void Validate_AllVariablesSatisfied_ReturnsEmpty()
        {
            var chain = new PromptChain()
                .AddStep("s1",
                    new PromptTemplate("Process: {{input}}"),
                    "result")
                .AddStep("s2",
                    new PromptTemplate("Refine: {{result}}"),
                    "final");

            var errors = chain.Validate(
                new Dictionary<string, string> { ["input"] = "test" });

            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MissingInitialVariable_ReturnsError()
        {
            var chain = new PromptChain()
                .AddStep("s1",
                    new PromptTemplate("Process: {{input}}"),
                    "result");

            var errors = chain.Validate();
            Assert.Single(errors);
            Assert.Contains("input", errors[0]);
        }

        [Fact]
        public void Validate_UpstreamOutputSatisfiesDownstream()
        {
            var chain = new PromptChain()
                .AddStep("s1",
                    new PromptTemplate("Summarize: {{text}}"),
                    "summary")
                .AddStep("s2",
                    new PromptTemplate("Translate: {{summary}}"),
                    "translation");

            var errors = chain.Validate(
                new Dictionary<string, string> { ["text"] = "..." });

            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_DownstreamCantUseUpstreamOutput_ReturnsError()
        {
            // Step 1 needs "future" which is only produced by step 2
            var chain = new PromptChain()
                .AddStep("s1",
                    new PromptTemplate("Use: {{future}}"),
                    "result")
                .AddStep("s2",
                    new PromptTemplate("Generate: {{result}}"),
                    "future");

            var errors = chain.Validate();
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("future"));
        }

        [Fact]
        public void Validate_DefaultsSatisfyRequirements()
        {
            var template = new PromptTemplate(
                "{{greeting}} {{name}}",
                new Dictionary<string, string> { ["greeting"] = "Hello" });

            var chain = new PromptChain()
                .AddStep("s1", template, "result");

            // "name" is required but "greeting" has a default
            var errors = chain.Validate();
            Assert.Single(errors);
            Assert.Contains("name", errors[0]);
            Assert.DoesNotContain(errors, e => e.Contains("greeting"));
        }

        [Fact]
        public void Validate_NullInitialVariables_StillWorks()
        {
            var chain = new PromptChain()
                .AddStep("s1",
                    new PromptTemplate(
                        "Hello",
                        new Dictionary<string, string>()),
                    "result");

            var errors = chain.Validate(null);
            Assert.Empty(errors);
        }

        // ──────────────── Serialization ────────────────

        [Fact]
        public void ToJson_RoundTrips()
        {
            var chain = new PromptChain()
                .WithSystemPrompt("Be helpful")
                .WithMaxRetries(5)
                .AddStep("summarize",
                    new PromptTemplate(
                        "Summarize: {{text}}",
                        new Dictionary<string, string> { ["text"] = "default text" }),
                    "summary")
                .AddStep("translate",
                    new PromptTemplate("Translate: {{summary}}"),
                    "translation");

            string json = chain.ToJson();
            var restored = PromptChain.FromJson(json);

            Assert.Equal(2, restored.StepCount);
            Assert.Equal("summarize", restored.Steps[0].Name);
            Assert.Equal("summary", restored.Steps[0].OutputVariable);
            Assert.Equal("translate", restored.Steps[1].Name);
            Assert.Equal("translation", restored.Steps[1].OutputVariable);
        }

        [Fact]
        public void ToJson_ContainsExpectedFields()
        {
            var chain = new PromptChain()
                .WithSystemPrompt("test prompt")
                .AddStep("s1", new PromptTemplate("Hello {{name}}"), "result");

            string json = chain.ToJson();

            Assert.Contains("\"systemPrompt\"", json);
            Assert.Contains("test prompt", json);
            Assert.Contains("\"steps\"", json);
            Assert.Contains("\"name\"", json);
            Assert.Contains("\"template\"", json);
            Assert.Contains("\"outputVariable\"", json);
        }

        [Fact]
        public void ToJson_Compact_NoIndentation()
        {
            var chain = new PromptChain()
                .AddStep("s1", new PromptTemplate("test"), "out");

            string json = chain.ToJson(indented: false);
            Assert.DoesNotContain("\n", json);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FromJson_NullOrEmpty_Throws(string? json)
        {
            Assert.Throws<ArgumentException>(
                () => PromptChain.FromJson(json!));
        }

        [Fact]
        public void FromJson_EmptySteps_Throws()
        {
            var json = "{\"steps\":[]}";
            Assert.Throws<InvalidOperationException>(
                () => PromptChain.FromJson(json));
        }

        [Fact]
        public void FromJson_MissingStepFields_Throws()
        {
            var json = "{\"steps\":[{\"name\":\"\",\"template\":\"\",\"outputVariable\":\"\"}]}";
            Assert.Throws<InvalidOperationException>(
                () => PromptChain.FromJson(json));
        }

        [Fact]
        public void FromJson_WithDefaults_RestoresDefaults()
        {
            var json = @"{
                ""steps"": [{
                    ""name"": ""s1"",
                    ""template"": ""Hello {{name}}"",
                    ""defaults"": { ""name"": ""World"" },
                    ""outputVariable"": ""result""
                }]
            }";

            var chain = PromptChain.FromJson(json);
            Assert.Equal("World", chain.Steps[0].Template.Defaults["name"]);
        }

        [Fact]
        public void FromJson_NoSystemPrompt_DefaultsToNull()
        {
            var json = @"{
                ""steps"": [{
                    ""name"": ""s1"",
                    ""template"": ""test"",
                    ""outputVariable"": ""result""
                }]
            }";

            var chain = PromptChain.FromJson(json);
            Assert.Equal(1, chain.StepCount);
        }

        // ──────────────── File I/O ────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SaveToFileAsync_NullOrEmptyPath_Throws(string? path)
        {
            var chain = new PromptChain()
                .AddStep("s1", new PromptTemplate("test"), "out");

            await Assert.ThrowsAsync<ArgumentException>(
                () => chain.SaveToFileAsync(path!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task LoadFromFileAsync_NullOrEmptyPath_Throws(string? path)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => PromptChain.LoadFromFileAsync(path!));
        }

        [Fact]
        public async Task LoadFromFileAsync_FileNotFound_Throws()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => PromptChain.LoadFromFileAsync("nonexistent_chain.json"));
        }

        [Fact]
        public async Task SaveAndLoadFile_RoundTrips()
        {
            var chain = new PromptChain()
                .WithSystemPrompt("helpful")
                .AddStep("step1",
                    new PromptTemplate("Process {{input}}"),
                    "processed")
                .AddStep("step2",
                    new PromptTemplate("Refine {{processed}}"),
                    "refined");

            var path = Path.GetTempFileName();
            try
            {
                await chain.SaveToFileAsync(path);
                var loaded = await PromptChain.LoadFromFileAsync(path);

                Assert.Equal(2, loaded.StepCount);
                Assert.Equal("step1", loaded.Steps[0].Name);
                Assert.Equal("step2", loaded.Steps[1].Name);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    public class ChainResultTests
    {
        [Fact]
        public void FinalResponse_WithSteps_ReturnsLastResponse()
        {
            var steps = new List<StepResult>
            {
                new StepResult("s1", "out1", "prompt1", "response1", TimeSpan.FromMilliseconds(100)),
                new StepResult("s2", "out2", "prompt2", "response2", TimeSpan.FromMilliseconds(200))
            };

            var variables = new Dictionary<string, string>
            {
                ["out1"] = "response1",
                ["out2"] = "response2"
            };

            var result = new ChainResult(steps, variables, TimeSpan.FromMilliseconds(300));

            Assert.Equal("response2", result.FinalResponse);
        }

        [Fact]
        public void FinalResponse_EmptySteps_ReturnsNull()
        {
            var result = new ChainResult(
                new List<StepResult>(),
                new Dictionary<string, string>(),
                TimeSpan.Zero);

            Assert.Null(result.FinalResponse);
        }

        [Fact]
        public void GetOutput_ExistingVariable_ReturnsValue()
        {
            var steps = new List<StepResult>
            {
                new StepResult("s1", "summary", "prompt", "A short summary.", TimeSpan.FromMilliseconds(50))
            };

            var variables = new Dictionary<string, string>
            {
                ["summary"] = "A short summary."
            };

            var result = new ChainResult(steps, variables, TimeSpan.FromMilliseconds(50));

            Assert.Equal("A short summary.", result.GetOutput("summary"));
        }

        [Fact]
        public void GetOutput_NonExistentVariable_ReturnsNull()
        {
            var result = new ChainResult(
                new List<StepResult>(),
                new Dictionary<string, string>(),
                TimeSpan.Zero);

            Assert.Null(result.GetOutput("nonexistent"));
        }

        [Fact]
        public void GetOutput_CaseInsensitive()
        {
            var variables = new Dictionary<string, string>
            {
                ["Summary"] = "test value"
            };

            var result = new ChainResult(
                new List<StepResult>(),
                variables,
                TimeSpan.Zero);

            Assert.Equal("test value", result.GetOutput("SUMMARY"));
            Assert.Equal("test value", result.GetOutput("summary"));
        }

        [Fact]
        public void ToJson_ContainsAllFields()
        {
            var steps = new List<StepResult>
            {
                new StepResult("summarize", "summary", "Summarize: hello",
                    "A summary", TimeSpan.FromMilliseconds(150))
            };

            var variables = new Dictionary<string, string>
            {
                ["input"] = "hello",
                ["summary"] = "A summary"
            };

            var result = new ChainResult(steps, variables, TimeSpan.FromMilliseconds(150));
            string json = result.ToJson();

            Assert.Contains("\"totalElapsedMs\"", json);
            Assert.Contains("\"steps\"", json);
            Assert.Contains("\"stepName\"", json);
            Assert.Contains("\"outputVariable\"", json);
            Assert.Contains("\"renderedPrompt\"", json);
            Assert.Contains("\"response\"", json);
            Assert.Contains("\"elapsedMs\"", json);
            Assert.Contains("\"variables\"", json);
            Assert.Contains("summarize", json);
            Assert.Contains("A summary", json);
        }

        [Fact]
        public void ToJson_Compact_NoIndentation()
        {
            var result = new ChainResult(
                new List<StepResult>(),
                new Dictionary<string, string>(),
                TimeSpan.Zero);

            string json = result.ToJson(indented: false);
            Assert.DoesNotContain("\n", json);
        }

        [Fact]
        public void Properties_MatchConstructorArgs()
        {
            var steps = new List<StepResult>
            {
                new StepResult("s1", "o1", "p1", "r1", TimeSpan.FromSeconds(1)),
                new StepResult("s2", "o2", "p2", "r2", TimeSpan.FromSeconds(2))
            };

            var vars = new Dictionary<string, string> { ["o1"] = "r1", ["o2"] = "r2" };
            var elapsed = TimeSpan.FromSeconds(3);

            var result = new ChainResult(steps, vars, elapsed);

            Assert.Equal(2, result.Steps.Count);
            Assert.Equal(elapsed, result.TotalElapsed);
            Assert.Equal("r1", result.Variables["o1"]);
            Assert.Equal("r2", result.Variables["o2"]);
        }
    }

    public class StepResultTests
    {
        [Fact]
        public void Properties_AreSetCorrectly()
        {
            var elapsed = TimeSpan.FromMilliseconds(250);
            var result = new StepResult(
                "analyze", "analysis", "Analyze this text", "The analysis", elapsed);

            Assert.Equal("analyze", result.StepName);
            Assert.Equal("analysis", result.OutputVariable);
            Assert.Equal("Analyze this text", result.RenderedPrompt);
            Assert.Equal("The analysis", result.Response);
            Assert.Equal(elapsed, result.Elapsed);
        }

        [Fact]
        public void NullResponse_IsAllowed()
        {
            var result = new StepResult(
                "step", "out", "prompt", null, TimeSpan.Zero);

            Assert.Null(result.Response);
        }
    }
}
