namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptInheritanceTests
    {
        // ── Construction ──

        [Fact]
        public void Constructor_WithValidTemplate_Succeeds()
        {
            var prompt = new InheritablePrompt("Hello {% block name %}world{% endblock %}!");
            Assert.NotNull(prompt);
            Assert.Equal(0, prompt.Depth);
            Assert.Null(prompt.Parent);
        }

        [Fact]
        public void Constructor_NullTemplate_Throws()
        {
            Assert.Throws<ArgumentException>(() => new InheritablePrompt(null!));
        }

        [Fact]
        public void Constructor_EmptyTemplate_Throws()
        {
            Assert.Throws<ArgumentException>(() => new InheritablePrompt("  "));
        }

        [Fact]
        public void Constructor_DuplicateBlockNames_Throws()
        {
            Assert.Throws<ArgumentException>(() => new InheritablePrompt(
                "{% block a %}x{% endblock %} {% block a %}y{% endblock %}"));
        }

        [Fact]
        public void Constructor_NoBlocks_Succeeds()
        {
            var prompt = new InheritablePrompt("Just plain text, no blocks.");
            Assert.Empty(prompt.BlockNames);
            Assert.Equal("Just plain text, no blocks.", prompt.Render());
        }

        // ── Block Discovery ──

        [Fact]
        public void BlockNames_ReturnsAllBlocks()
        {
            var prompt = new InheritablePrompt(
                "{% block a %}x{% endblock %} {% block b %}y{% endblock %} {% block c %}z{% endblock %}");
            Assert.Equal(3, prompt.BlockNames.Count);
            Assert.Contains("a", prompt.BlockNames);
            Assert.Contains("b", prompt.BlockNames);
            Assert.Contains("c", prompt.BlockNames);
        }

        [Fact]
        public void GetBlockDefault_ReturnsContent()
        {
            var prompt = new InheritablePrompt("{% block greeting %}Hello{% endblock %}");
            Assert.Equal("Hello", prompt.GetBlockDefault("greeting"));
        }

        [Fact]
        public void GetBlockDefault_UnknownBlock_ReturnsNull()
        {
            var prompt = new InheritablePrompt("{% block a %}x{% endblock %}");
            Assert.Null(prompt.GetBlockDefault("nonexistent"));
        }

        // ── Rendering (no inheritance) ──

        [Fact]
        public void Render_NoOverrides_UsesDefaults()
        {
            var prompt = new InheritablePrompt(
                "You are a {% block role %}helpful assistant{% endblock %}.");
            Assert.Equal("You are a helpful assistant.", prompt.Render());
        }

        [Fact]
        public void Render_MultipleBlocks_AllResolved()
        {
            var prompt = new InheritablePrompt(
                "{% block greeting %}Hi{% endblock %}, {% block name %}user{% endblock %}!");
            Assert.Equal("Hi, user!", prompt.Render());
        }

        // ── Child Creation ──

        [Fact]
        public void CreateChild_OverridesBlock()
        {
            var parent = new InheritablePrompt(
                "{% block role %}assistant{% endblock %} says {% block msg %}hello{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["role"] = "code reviewer"
            });

            Assert.Equal("code reviewer says hello", child.Render());
        }

        [Fact]
        public void CreateChild_InvalidBlockName_Throws()
        {
            var parent = new InheritablePrompt("{% block a %}x{% endblock %}");
            Assert.Throws<ArgumentException>(() =>
                parent.CreateChild(new Dictionary<string, string> { ["b"] = "y" }));
        }

        [Fact]
        public void CreateChild_NullOverrides_Throws()
        {
            var parent = new InheritablePrompt("{% block a %}x{% endblock %}");
            Assert.Throws<ArgumentNullException>(() => parent.CreateChild(null!));
        }

        [Fact]
        public void CreateChild_SetsDepthAndParent()
        {
            var parent = new InheritablePrompt("{% block a %}x{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string> { ["a"] = "y" });
            Assert.Equal(1, child.Depth);
            Assert.Same(parent, child.Parent);
        }

        // ── Super ──

        [Fact]
        public void Render_SuperInOverride_IncludesParentContent()
        {
            var parent = new InheritablePrompt(
                "{% block rules %}Be helpful.{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["rules"] = "{{super}} Also be concise."
            });

            Assert.Equal("Be helpful. Also be concise.", child.Render());
        }

        [Fact]
        public void Render_SuperAtEnd_AppendsParentContent()
        {
            var parent = new InheritablePrompt("{% block msg %}World{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["msg"] = "Hello {{super}}"
            });

            Assert.Equal("Hello World", child.Render());
        }

        // ── Multi-level Inheritance ──

        [Fact]
        public void ThreeLevelInheritance_MostDerivedWins()
        {
            var grandparent = new InheritablePrompt(
                "{% block a %}GP{% endblock %} {% block b %}GP{% endblock %}");
            var parent = grandparent.CreateChild(new Dictionary<string, string>
            {
                ["a"] = "Parent"
            });
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["a"] = "Child"
            });

            Assert.Equal("Child GP", child.Render());
            Assert.Equal(2, child.Depth);
        }

        [Fact]
        public void MaxDepth_Exceeded_Throws()
        {
            var current = new InheritablePrompt("{% block a %}x{% endblock %}");
            for (int i = 0; i < InheritablePrompt.MaxDepth - 1; i++)
            {
                current = current.CreateChild(new Dictionary<string, string>
                {
                    ["a"] = $"level{i}"
                });
            }

            Assert.Throws<InvalidOperationException>(() =>
                current.CreateChild(new Dictionary<string, string> { ["a"] = "too deep" }));
        }

        // ── WithBlock Shorthand ──

        [Fact]
        public void WithBlock_CreatesChildWithSingleOverride()
        {
            var parent = new InheritablePrompt("{% block a %}x{% endblock %}");
            var child = parent.WithBlock("a", "y");
            Assert.Equal("y", child.Render());
            Assert.Equal(1, child.Depth);
        }

        // ── RenderDetailed ──

        [Fact]
        public void RenderDetailed_ReportsBlockResolution()
        {
            var parent = new InheritablePrompt(
                "{% block a %}default_a{% endblock %} {% block b %}default_b{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["a"] = "overridden_a"
            });

            var result = child.RenderDetailed();
            Assert.Equal("overridden_a default_b", result.Text);
            Assert.Contains("a", result.OverriddenBlocks);
            Assert.Contains("b", result.ParentBlocks);
            Assert.Equal(2, result.AllBlocks.Count);
            Assert.Equal(1, result.Depth);
        }

        [Fact]
        public void RenderDetailed_OverrideEqualToDefault_StillReportedAsOverridden()
        {
            // A child that overrides a block with content identical to the default
            // has still overridden it. RenderDetailed must classify by whether an
            // override exists in the chain, not by string equality with the default.
            var parent = new InheritablePrompt(
                "{% block a %}same{% endblock %} {% block b %}default_b{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["a"] = "same"
            });

            var result = child.RenderDetailed();
            Assert.Equal("same default_b", result.Text);
            Assert.Contains("a", result.OverriddenBlocks);
            Assert.DoesNotContain("a", result.ParentBlocks);
            Assert.Contains("b", result.ParentBlocks);
        }

        [Fact]
        public void RenderDetailed_SuperOnlyOverride_ReportedAsOverridden()
        {
            // {{super}}-only override resolves back to the parent's content but is
            // a genuine override, so it must be reported in OverriddenBlocks.
            var parent = new InheritablePrompt("{% block msg %}World{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["msg"] = "{{super}}"
            });

            var result = child.RenderDetailed();
            Assert.Equal("World", result.Text);
            Assert.Contains("msg", result.OverriddenBlocks);
            Assert.DoesNotContain("msg", result.ParentBlocks);
        }

        [Fact]
        public void RenderDetailed_NoOverrides_AllReportedAsParent()
        {
            var prompt = new InheritablePrompt(
                "{% block a %}x{% endblock %} {% block b %}y{% endblock %}");

            var result = prompt.RenderDetailed();
            Assert.Equal("x y", result.Text);
            Assert.Empty(result.OverriddenBlocks);
            Assert.Contains("a", result.ParentBlocks);
            Assert.Contains("b", result.ParentBlocks);
        }

        // ── OverriddenBlockNames ──

        [Fact]
        public void OverriddenBlockNames_ReturnsOnlyOverrides()
        {
            var parent = new InheritablePrompt(
                "{% block a %}x{% endblock %} {% block b %}y{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["b"] = "z"
            });

            Assert.Single(child.OverriddenBlockNames);
            Assert.Equal("b", child.OverriddenBlockNames[0]);
        }

        // ── Describe ──

        [Fact]
        public void Describe_ShowsBlockInfo()
        {
            var parent = new InheritablePrompt("{% block role %}assistant{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["role"] = "reviewer"
            });

            string desc = child.Describe();
            Assert.Contains("Block: role", desc);
            Assert.Contains("OVERRIDDEN", desc);
            Assert.Contains("depth=1", desc);
        }

        // ── JSON Serialization ──

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var prompt = new InheritablePrompt(
                "{% block a %}hello{% endblock %} {% block b %}world{% endblock %}");
            string json = prompt.ToJson();
            Assert.Contains("\"template\"", json);
            Assert.Contains("\"blocks\"", json);
        }

        [Fact]
        public void FromJson_RoundTrips()
        {
            var original = new InheritablePrompt("{% block a %}hello{% endblock %}");
            string json = original.ToJson();
            var restored = InheritablePrompt.FromJson(json);

            Assert.Equal(original.Render(), restored.Render());
            Assert.Single(restored.BlockNames);
        }

        [Fact]
        public void FromJson_WithOverrides_Restores()
        {
            var parent = new InheritablePrompt("{% block a %}x{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string> { ["a"] = "y" });
            string json = child.ToJson();
            var restored = InheritablePrompt.FromJson(json);

            Assert.Equal("y", restored.Render());
        }

        [Fact]
        public void FromJson_NullInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => InheritablePrompt.FromJson(null!));
        }

        [Fact]
        public void FromJson_EmptyInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => InheritablePrompt.FromJson("  "));
        }

        // ── Block Name Validation ──

        [Fact]
        public void Block_WithUnderscores_Works()
        {
            var prompt = new InheritablePrompt("{% block my_block %}ok{% endblock %}");
            Assert.Equal("ok", prompt.Render());
        }

        [Fact]
        public void PromptBlock_InvalidName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PromptBlock("invalid-name!", "content"));
        }

        [Fact]
        public void PromptBlock_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PromptBlock("", "content"));
        }

        [Fact]
        public void PromptBlock_ValidName_Succeeds()
        {
            var block = new PromptBlock("valid_name", "content");
            Assert.Equal("valid_name", block.Name);
            Assert.Equal("content", block.Content);
        }

        // ── Case Insensitivity ──

        [Fact]
        public void BlockOverride_CaseInsensitive()
        {
            var parent = new InheritablePrompt("{% block Role %}assistant{% endblock %}");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["role"] = "reviewer"
            });
            Assert.Equal("reviewer", child.Render());
        }

        // ── Complex Scenarios ──

        [Fact]
        public void RealWorldPrompt_SystemPromptInheritance()
        {
            var baseSystem = new InheritablePrompt(
                "You are a {% block role %}helpful AI assistant{% endblock %}.\n\n" +
                "## Guidelines\n{% block guidelines %}Be clear and concise.{% endblock %}\n\n" +
                "## Output Format\n{% block format %}Respond in plain text.{% endblock %}");

            var codeReviewer = baseSystem.CreateChild(new Dictionary<string, string>
            {
                ["role"] = "senior code reviewer specializing in C#",
                ["guidelines"] = "{{super}}\n- Check for SOLID violations\n- Flag security issues",
                ["format"] = "Use markdown with ```csharp code blocks."
            });

            string rendered = codeReviewer.Render();
            Assert.Contains("senior code reviewer specializing in C#", rendered);
            Assert.Contains("Be clear and concise.", rendered); // from {{super}}
            Assert.Contains("Check for SOLID violations", rendered);
            Assert.Contains("```csharp", rendered);
        }

        [Fact]
        public void EmptyOverride_ClearsBlock()
        {
            var parent = new InheritablePrompt(
                "prefix {% block optional %}some content{% endblock %} suffix");
            var child = parent.CreateChild(new Dictionary<string, string>
            {
                ["optional"] = ""
            });
            Assert.Equal("prefix  suffix", child.Render());
        }
    }
}
