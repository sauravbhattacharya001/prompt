namespace Prompt.Tests
{
    using System;
    using Xunit;

    public class PromptComparatorTests
    {
        [Fact]
        public void Compare_NullPromptA_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PromptComparator.Compare(null!, "x"));
        }

        [Fact]
        public void Compare_NullPromptB_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PromptComparator.Compare("x", null!));
        }

        [Fact]
        public void Compare_IdenticalPrompts_HighSimilarity()
        {
            var prompt = "Summarize the following:\n{{input}}";
            var result = PromptComparator.Compare(prompt, prompt);

            Assert.Equal(1.0, result.Similarity, 5);
            Assert.Empty(result.VariablesOnlyInA);
            Assert.Empty(result.VariablesOnlyInB);
            Assert.Contains("input", result.SharedVariables);
            Assert.Contains("very similar", result.Summary);
        }

        [Fact]
        public void Compare_EmptyPrompts_DoesNotThrow_ReturnsTies()
        {
            var result = PromptComparator.Compare("", "");
            // Empty word sets are treated as fully similar
            Assert.Equal(1.0, result.Similarity, 5);
            Assert.Empty(result.SharedVariables);
            Assert.Empty(result.SharedSections);
            // Every numeric dimension whose values are equal should report Tie or Similar.
            Assert.All(result.Dimensions, d =>
                Assert.True(d.Verdict == "Tie" || d.Verdict == "Similar" || d.Verdict == "Same"
                    || d.Verdict == "N/A",
                    $"Unexpected verdict '{d.Verdict}' for dimension '{d.Name}'"));
        }

        [Fact]
        public void Compare_DifferentVariables_ReportedCorrectly()
        {
            var a = "Hello {{name}}, please {{action}}.";
            var b = "Hello {{name}}, your {{role}} is ready.";
            var result = PromptComparator.Compare(a, b);

            Assert.Contains("name", result.SharedVariables);
            Assert.Contains("action", result.VariablesOnlyInA);
            Assert.Contains("role", result.VariablesOnlyInB);
            Assert.DoesNotContain("name", result.VariablesOnlyInA);
            Assert.DoesNotContain("name", result.VariablesOnlyInB);
        }

        [Fact]
        public void Compare_SectionDetection_SharedAndUnique()
        {
            var a = "# Intro\nHello\n## Steps\nDo things\n";
            var b = "# Intro\nHello\n## Outputs\nReturn things\n";
            var result = PromptComparator.Compare(a, b);

            Assert.Contains("Intro", result.SharedSections, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Steps", result.SectionsOnlyInA, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Outputs", result.SectionsOnlyInB, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Compare_TokenSavings_MentionedInSummary()
        {
            var a = string.Concat("Word ", string.Join(" ", System.Linq.Enumerable.Repeat("alpha", 500)));
            var b = "Word alpha";
            var result = PromptComparator.Compare(a, b);

            Assert.Contains("fewer estimated tokens", result.Summary);
            // B should win on conciseness for words/chars/tokens dimensions
            var tokenDim = result.Dimensions.Find(d => d.Name == "Est. Tokens");
            Assert.NotNull(tokenDim);
            Assert.Equal("B more concise", tokenDim!.Verdict);
        }

        [Fact]
        public void Compare_StructureType_DetectsDocumentVsProse()
        {
            var doc = "# H1\nbody\n## H2\nbody\n### H3\nbody";
            var prose = "Just a plain sentence with no structure at all.";
            var result = PromptComparator.Compare(doc, prose);

            var structDim = result.Dimensions.Find(d => d.Name == "Structure");
            Assert.NotNull(structDim);
            Assert.Equal("Different", structDim!.Verdict);
            Assert.Equal("Document", structDim.ValueA);
            Assert.Equal("Prose", structDim.ValueB);
            Assert.Contains("different structures", result.Summary);
        }

        [Fact]
        public void Compare_StructureType_DetectsXmlAndCode()
        {
            var xml = "<role>x</role><task>y</task><output>z</output>";
            var code = "```c#\nvar a=1;\n```\n```c#\nvar b=2;\n```";
            var resultXml = PromptComparator.Compare(xml, xml);
            var resultCode = PromptComparator.Compare(code, code);

            Assert.Equal("XML-structured", resultXml.Dimensions.Find(d => d.Name == "Structure")!.ValueA);
            Assert.Equal("Code-heavy", resultCode.Dimensions.Find(d => d.Name == "Structure")!.ValueA);
        }

        [Fact]
        public void Compare_BulletAndCodeCounts_Populated()
        {
            var a = "- one\n- two\n- three\n```py\nprint(1)\n```";
            var b = "1) one\n2) two";
            var result = PromptComparator.Compare(a, b);

            var bullets = result.Dimensions.Find(d => d.Name == "Bullet Points");
            var code = result.Dimensions.Find(d => d.Name == "Code Blocks");
            Assert.NotNull(bullets);
            Assert.NotNull(code);
            Assert.Equal("3", bullets!.ValueA);
            Assert.Equal("2", bullets.ValueB);
            Assert.Equal("1", code!.ValueA);
            Assert.Equal("0", code.ValueB);
        }

        [Fact]
        public void Compare_VeryDifferentPrompts_LowSimilarity()
        {
            var a = "Translate the following English text into French.";
            var b = "Write a Python function that computes Fibonacci numbers recursively.";
            var result = PromptComparator.Compare(a, b);

            Assert.True(result.Similarity < 0.6,
                $"Expected low similarity, got {result.Similarity:F3}");
            Assert.DoesNotContain("very similar", result.Summary);
        }

        [Fact]
        public void Compare_Similarity_AlwaysInZeroToOne()
        {
            string[] samples = {
                "",
                "hello",
                "# A\n- one\n- two\n",
                "<x>y</x>",
                "Translate {{foo}} to {{bar}}.",
                string.Join(" ", System.Linq.Enumerable.Repeat("noise", 200))
            };

            foreach (var a in samples)
            {
                foreach (var b in samples)
                {
                    var r = PromptComparator.Compare(a, b);
                    Assert.InRange(r.Similarity, 0.0, 1.0);
                    Assert.NotNull(r.Summary);
                }
            }
        }

        [Fact]
        public void ToReport_RendersAllSections()
        {
            var a = "# Intro\nHello {{name}}";
            var b = "# Intro\nHi {{name}}, please {{action}}";
            var result = PromptComparator.Compare(a, b);

            var report = result.ToReport("Original", "Variant");

            Assert.Contains("Prompt Comparison Report", report);
            Assert.Contains("Original", report);
            Assert.Contains("Variant", report);
            Assert.Contains("Overall Similarity", report);
            Assert.Contains("Variables:", report);
            Assert.Contains("Sections:", report);
            // Shared variable "name" should appear under "Shared:"
            Assert.Contains("name", report);
            Assert.Contains("action", report);
        }

        [Fact]
        public void ToReport_DefaultLabels_UsedWhenNotProvided()
        {
            var result = PromptComparator.Compare("a", "b");
            var report = result.ToReport();
            Assert.Contains("Prompt A", report);
            Assert.Contains("Prompt B", report);
        }
    }
}
