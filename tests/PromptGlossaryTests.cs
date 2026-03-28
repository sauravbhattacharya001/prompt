namespace Prompt.Tests
{
    using Xunit;
    using System.Collections.Generic;

    public class PromptGlossaryTests
    {
        // ──────────── AddTerm / Count ────────────

        [Fact]
        public void AddTerm_IncreasesCount()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "Large Language Model", "AI");
            Assert.Equal(1, g.Count);
        }

        [Fact]
        public void AddTerm_EmptyCanonical_Throws()
        {
            var g = new PromptGlossary();
            Assert.Throws<System.ArgumentException>(() => g.AddTerm("", "def"));
        }

        // ──────────── Lookup ────────────

        [Fact]
        public void Lookup_FindsByCanonical()
        {
            var g = new PromptGlossary();
            g.AddTerm("prompt", "Input text", "Core");
            Assert.NotNull(g.Lookup("prompt"));
            Assert.NotNull(g.Lookup("PROMPT")); // case-insensitive
        }

        [Fact]
        public void Lookup_FindsByVariant()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "Large Language Model", "AI",
                variants: new[] { "language model" });
            var result = g.Lookup("language model");
            Assert.NotNull(result);
            Assert.Equal("LLM", result!.Canonical);
        }

        [Fact]
        public void Lookup_ReturnsNull_WhenNotFound()
        {
            var g = new PromptGlossary();
            Assert.Null(g.Lookup("nonexistent"));
        }

        // ──────────── RemoveTerm ────────────

        [Fact]
        public void RemoveTerm_RemovesCanonicalAndVariants()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "def", "AI", variants: new[] { "language model" });
            Assert.True(g.RemoveTerm("LLM"));
            Assert.Equal(0, g.Count);
            Assert.Null(g.Lookup("language model"));
        }

        // ──────────── Scan ────────────

        [Fact]
        public void Scan_DetectsVariantUsage()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "Large Language Model", "AI",
                variants: new[] { "language model" });

            var prompts = new Dictionary<string, string>
            {
                ["p1"] = "Send this to the language model for processing"
            };

            var result = g.Scan(prompts);
            Assert.Equal(1, result.TotalInconsistencies);
            Assert.Equal("language model", result.Inconsistencies[0].Found);
            Assert.Equal("LLM", result.Inconsistencies[0].Canonical);
        }

        [Fact]
        public void Scan_PerfectConsistency_Returns100()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "Large Language Model", "AI",
                variants: new[] { "language model" });

            var prompts = new Dictionary<string, string>
            {
                ["p1"] = "Send this to the LLM for processing"
            };

            var result = g.Scan(prompts);
            Assert.Equal(0, result.TotalInconsistencies);
            Assert.Equal(100.0, result.ConsistencyScore);
        }

        // ──────────── Standardize ────────────

        [Fact]
        public void Standardize_ReplacesVariants()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "Large Language Model", "AI",
                variants: new[] { "language model" });
            g.AddTerm("prompt", "Input text", "Core",
                variants: new[] { "query" });

            var result = g.Standardize("Send query to the language model");
            Assert.Contains("prompt", result);
            Assert.Contains("LLM", result);
        }

        [Fact]
        public void Standardize_EmptyText_ReturnsEmpty()
        {
            var g = new PromptGlossary();
            Assert.Equal("", g.Standardize(""));
        }

        // ──────────── ExtractTerms ────────────

        [Fact]
        public void ExtractTerms_ReturnsSortedByFrequency()
        {
            var g = new PromptGlossary();
            var prompts = new Dictionary<string, string>
            {
                ["p1"] = "model model model token token",
                ["p2"] = "model token"
            };

            var terms = g.ExtractTerms(prompts);
            Assert.True(terms.Count > 0);
            Assert.Equal("model", terms[0].Term);
            Assert.Equal(4, terms[0].Count);
        }

        // ──────────── SuggestTerms ────────────

        [Fact]
        public void SuggestTerms_ExcludesExistingGlossaryTerms()
        {
            var g = new PromptGlossary();
            g.AddTerm("model", "An LLM", "AI");

            var prompts = new Dictionary<string, string>
            {
                ["p1"] = "model model model token token token"
            };

            var suggestions = g.SuggestTerms(prompts, minOccurrences: 3);
            Assert.DoesNotContain(suggestions, s => s.Term.Equals("model", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(suggestions, s => s.Term.Equals("token", System.StringComparison.OrdinalIgnoreCase));
        }

        // ──────────── Export ────────────

        [Fact]
        public void ToMarkdown_ContainsCategoryHeaders()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "Large Language Model", "AI");
            g.AddTerm("prompt", "Input text", "Core");

            var md = g.ToMarkdown();
            Assert.Contains("## AI", md);
            Assert.Contains("## Core", md);
            Assert.Contains("**LLM**", md);
        }

        [Fact]
        public void ToCsv_HasHeaders()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "def", "AI");
            var csv = g.ToCsv();
            Assert.StartsWith("Canonical,Definition,Category,Variants", csv);
        }

        [Fact]
        public void ToJson_RoundTrips()
        {
            var g = new PromptGlossary();
            g.AddTerm("LLM", "Large Language Model", "AI", variants: new[] { "language model" });

            var json = g.ToJson();
            var g2 = new PromptGlossary();
            int imported = g2.ImportJson(json);

            Assert.Equal(1, imported);
            Assert.NotNull(g2.Lookup("language model"));
        }

        // ──────────── CreateDefault ────────────

        [Fact]
        public void CreateDefault_HasStandardTerms()
        {
            var g = PromptGlossary.CreateDefault();
            Assert.True(g.Count >= 10);
            Assert.NotNull(g.Lookup("LLM"));
            Assert.NotNull(g.Lookup("chain of thought")); // variant of chain-of-thought
            Assert.NotNull(g.Lookup("RAG"));
        }

        // ──────────── Categories ────────────

        [Fact]
        public void GetCategories_ReturnsDistinctSorted()
        {
            var g = new PromptGlossary();
            g.AddTerm("a", "def", "Zebra");
            g.AddTerm("b", "def", "Alpha");
            g.AddTerm("c", "def", "Alpha");

            var cats = new List<string>(g.GetCategories());
            Assert.Equal(2, cats.Count);
            Assert.Equal("Alpha", cats[0]);
            Assert.Equal("Zebra", cats[1]);
        }
    }
}
