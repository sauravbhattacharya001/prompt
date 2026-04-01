namespace Prompt.Tests
{
    using Xunit;

    public class PromptRecipeTests
    {
        [Fact]
        public void Builder_Creates_Recipe_With_All_Fields()
        {
            var recipe = new PromptRecipeBuilder("test-recipe")
                .WithDescription("A test recipe")
                .WithSystemPersona("You are helpful.")
                .WithTemplate("Answer about {{topic}}")
                .WithFewShot("What is AI?", "Artificial Intelligence")
                .WithDefault("topic", "science")
                .WithTag("test")
                .WithMetadata("author", "unit-test")
                .Build();

            Assert.Equal("test-recipe", recipe.Name);
            Assert.Equal("A test recipe", recipe.Description);
            Assert.Equal("You are helpful.", recipe.SystemPersona);
            Assert.Single(recipe.FewShotExamples);
            Assert.Single(recipe.Tags);
            Assert.Equal("test", recipe.Tags[0]);
            Assert.Equal("unit-test", recipe.Metadata["author"]);
        }

        [Fact]
        public void Render_Includes_Persona_Examples_And_Template()
        {
            var recipe = new PromptRecipeBuilder("r")
                .WithSystemPersona("Be concise.")
                .WithTemplate("Tell me about {{topic}}")
                .WithFewShot("Dogs?", "Loyal animals.")
                .Build();

            var result = recipe.Render(new Dictionary<string, string>
            {
                ["topic"] = "cats"
            });

            Assert.Contains("Be concise.", result);
            Assert.Contains("Dogs?", result);
            Assert.Contains("Loyal animals.", result);
            Assert.Contains("Tell me about cats", result);
        }

        [Fact]
        public void Render_Uses_Defaults_When_No_Override()
        {
            var recipe = new PromptRecipeBuilder("r")
                .WithTemplate("{{greeting}} {{name}}")
                .WithDefault("greeting", "Hello")
                .Build();

            var result = recipe.Render(new Dictionary<string, string>
            {
                ["name"] = "World"
            });

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void Render_Override_Replaces_Default()
        {
            var recipe = new PromptRecipeBuilder("r")
                .WithTemplate("{{greeting}} {{name}}")
                .WithDefault("greeting", "Hello")
                .Build();

            var result = recipe.Render(new Dictionary<string, string>
            {
                ["greeting"] = "Hi",
                ["name"] = "World"
            });

            Assert.Equal("Hi World", result);
        }

        [Fact]
        public void GetRequiredVariables_Excludes_Defaults()
        {
            var recipe = new PromptRecipeBuilder("r")
                .WithTemplate("{{a}} {{b}} {{c}}")
                .WithDefault("a", "1")
                .Build();

            var required = recipe.GetRequiredVariables();
            Assert.DoesNotContain("a", required);
            Assert.Contains("b", required);
            Assert.Contains("c", required);
        }

        [Fact]
        public void Validate_Returns_Missing_Variables()
        {
            var recipe = new PromptRecipeBuilder("r")
                .WithTemplate("{{x}} {{y}}")
                .Build();

            var missing = recipe.Validate(new Dictionary<string, string>
            {
                ["x"] = "1"
            });

            Assert.Single(missing);
            Assert.Equal("y", missing[0]);
        }

        [Fact]
        public void Validate_Returns_Empty_When_All_Provided()
        {
            var recipe = new PromptRecipeBuilder("r")
                .WithTemplate("{{x}}")
                .WithDefault("x", "1")
                .Build();

            var missing = recipe.Validate();
            Assert.Empty(missing);
        }

        [Fact]
        public void ToJson_And_FromJson_Roundtrip()
        {
            var recipe = new PromptRecipeBuilder("roundtrip")
                .WithDescription("Test roundtrip")
                .WithSystemPersona("Be helpful.")
                .WithTemplate("Do {{thing}}")
                .WithFewShot("input1", "output1")
                .WithDefault("thing", "stuff")
                .WithTag("test")
                .WithMetadata("version", "1.0")
                .Build();

            var json = recipe.ToJson();
            var restored = PromptRecipe.FromJson(json);

            Assert.Equal("roundtrip", restored.Name);
            Assert.Equal("Test roundtrip", restored.Description);
            Assert.Equal("Be helpful.", restored.SystemPersona);
            Assert.Single(restored.FewShotExamples);
            Assert.Equal("input1", restored.FewShotExamples[0].Input);
            Assert.Single(restored.Tags);
            Assert.Equal("1.0", restored.Metadata["version"]);
        }

        [Fact]
        public void FromJson_Throws_On_Empty()
        {
            Assert.Throws<ArgumentException>(() => PromptRecipe.FromJson(""));
        }

        [Fact]
        public void Summarize_Contains_Key_Info()
        {
            var recipe = new PromptRecipeBuilder("summary-test")
                .WithDescription("A summary test")
                .WithTemplate("{{a}} {{b}}")
                .WithDefault("a", "1")
                .WithTag("demo")
                .WithFewShot("in", "out")
                .Build();

            var summary = recipe.Summarize();
            Assert.Contains("summary-test", summary);
            Assert.Contains("A summary test", summary);
            Assert.Contains("demo", summary);
            Assert.Contains("Required:", summary);
            Assert.Contains("b", summary);
            Assert.Contains("Examples: 1", summary);
        }

        [Fact]
        public void Constructor_Throws_On_Null_Name()
        {
            Assert.Throws<ArgumentException>(() =>
                new PromptRecipe(null!, new PromptTemplate("t")));
        }

        [Fact]
        public void Constructor_Throws_On_Null_Template()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PromptRecipe("n", null!));
        }
    }
}
