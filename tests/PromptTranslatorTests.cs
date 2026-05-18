namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class PromptTranslatorTests
    {
        /// <summary>
        /// A simple "translator" that upper-cases text and records every call.
        /// This lets us assert what was/wasn't sent to the backend.
        /// </summary>
        private sealed class FakeTranslator
        {
            public readonly List<string> Calls = new();

            public Task<string> Translate(string text, string from, string to)
            {
                Calls.Add(text);
                return Task.FromResult($"[{to}]{text.ToUpperInvariant()}");
            }
        }

        [Fact]
        public void Ctor_NullFunc_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptTranslator(null!));
        }

        [Fact]
        public async Task TranslateAsync_NullOrEmpty_ReturnsAsIs()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            Assert.Equal(string.Empty, await t.TranslateAsync(null!, "en", "es"));
            Assert.Equal("", await t.TranslateAsync("", "en", "es"));
            Assert.Equal("   ", await t.TranslateAsync("   ", "en", "es"));
            Assert.Empty(fake.Calls);
        }

        [Fact]
        public async Task TranslateAsync_SameLanguage_NoCall()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            var result = await t.TranslateAsync("hello world", "en", "en");
            Assert.Equal("hello world", result);
            Assert.Empty(fake.Calls);
        }

        [Fact]
        public async Task TranslateAsync_PreservesDoubleBracePlaceholders()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            var result = await t.TranslateAsync("Hello {{name}}, your {{role}} awaits.", "en", "es");

            Assert.Contains("{{name}}", result);
            Assert.Contains("{{role}}", result);
            // The backend should never see the raw placeholder text
            Assert.All(fake.Calls, c =>
            {
                Assert.DoesNotContain("{{name}}", c);
                Assert.DoesNotContain("{{role}}", c);
            });
        }

        [Fact]
        public async Task TranslateAsync_PreservesFencedCodeBlocks()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            var prompt = "Run this:\n```python\nprint('hi {{name}}')\n```\nThanks.";
            var result = await t.TranslateAsync(prompt, "en", "es");

            // Fenced block must be preserved verbatim, including the {{name}} inside it.
            Assert.Contains("```python\nprint('hi {{name}}')\n```", result);
            // The backend never receives the code block.
            Assert.All(fake.Calls, c => Assert.DoesNotContain("print(", c));
        }

        [Fact]
        public async Task TranslateAsync_PreservesInlineCode()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            var result = await t.TranslateAsync("Call the `Translate` method.", "en", "es");

            Assert.Contains("`Translate`", result);
            Assert.All(fake.Calls, c => Assert.DoesNotContain("`Translate`", c));
        }

        [Fact]
        public async Task TranslateAsync_GlossaryTerms_NeverTranslated()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate)
                .AddGlossaryTerm("AgentBox")
                .AddGlossaryTerm("OpenAI");

            var result = await t.TranslateAsync("AgentBox runs on OpenAI.", "en", "es");

            Assert.Contains("AgentBox", result);
            Assert.Contains("OpenAI", result);
            Assert.All(fake.Calls, c =>
            {
                Assert.DoesNotContain("AgentBox", c, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("OpenAI", c, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task TranslateAsync_GlossaryTerms_BulkAdd()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate)
                .AddGlossaryTerms(new[] { "Foo", "Bar", "" });

            Assert.Contains("Foo", t.GlossaryTerms);
            Assert.Contains("Bar", t.GlossaryTerms);
            Assert.DoesNotContain("", t.GlossaryTerms);

            var result = await t.TranslateAsync("Foo and Bar are tools.", "en", "es");
            Assert.Contains("Foo", result);
            Assert.Contains("Bar", result);
        }

        [Fact]
        public async Task TranslateAsync_CustomPlaceholderPattern_Honored()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate)
                .AddPlaceholderPattern(@"\$\w+");

            var result = await t.TranslateAsync("Hi $name, welcome.", "en", "es");

            Assert.Contains("$name", result);
            Assert.All(fake.Calls, c => Assert.DoesNotContain("$name", c));
        }

        [Fact]
        public async Task TranslateAsync_RepeatedLine_ReusesMemoryCache()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            var prompt = "Hello.\nHello.\nHello.";
            var result = await t.TranslateAsync(prompt, "en", "es");

            // Only one backend call despite three identical lines
            Assert.Single(fake.Calls);
            Assert.Equal(3, result.Split('\n').Length);
            Assert.True(t.MemorySize > 0);
        }

        [Fact]
        public async Task TranslateAsync_PreservesLeadingWhitespace()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            var prompt = "  - item one\n    - nested";
            var result = await t.TranslateAsync(prompt, "en", "es");

            var lines = result.Split('\n');
            Assert.StartsWith("  ", lines[0]);
            Assert.StartsWith("    ", lines[1]);
        }

        [Fact]
        public async Task TranslateBatchAsync_TranslatesAllAndSharesCache()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            var prompts = new Dictionary<string, string>
            {
                ["greet"] = "Hello.",
                ["farewell"] = "Goodbye.",
                ["greet2"] = "Hello.", // duplicate text under different key
            };

            var results = await t.TranslateBatchAsync(prompts, "en", "es");

            Assert.Equal(3, results.Count);
            Assert.Equal(results["greet"], results["greet2"]);
            // Only two unique backend calls
            Assert.Equal(2, fake.Calls.Count);
        }

        [Fact]
        public async Task ClearMemory_RemovesAllEntries()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            await t.TranslateAsync("Hello.", "en", "es");
            Assert.True(t.MemorySize > 0);

            t.ClearMemory();
            Assert.Equal(0, t.MemorySize);
        }

        [Fact]
        public async Task ExportImportMemory_RoundTrips()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate);

            await t.TranslateAsync("Hello world.", "en", "es");
            await t.TranslateAsync("Goodbye.", "en", "fr");

            var exported = t.ExportMemory();
            Assert.Equal(2, exported.Count);
            Assert.All(exported, e =>
            {
                Assert.False(string.IsNullOrEmpty(e.SourceText));
                Assert.False(string.IsNullOrEmpty(e.TargetText));
                Assert.False(string.IsNullOrEmpty(e.SourceLanguage));
                Assert.False(string.IsNullOrEmpty(e.TargetLanguage));
            });

            // Import into a fresh translator with a tracking backend
            var fake2 = new FakeTranslator();
            var t2 = new PromptTranslator(fake2.Translate);
            t2.ImportMemory(exported);

            // Same text should now hit the cache, no backend calls
            var hello = await t2.TranslateAsync("Hello world.", "en", "es");
            Assert.Empty(fake2.Calls);
            Assert.Contains("HELLO", hello, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ImportMemory_Duplicates_DoesNotThrow()
        {
            var t = new PromptTranslator((s, f, to) => Task.FromResult(s));
            var entries = new[]
            {
                new TranslationMemoryEntry { SourceLanguage="en", TargetLanguage="es", SourceText="a", TargetText="x" },
                new TranslationMemoryEntry { SourceLanguage="en", TargetLanguage="es", SourceText="a", TargetText="y" }, // dup key, newer wins
            };
            t.ImportMemory(entries);
            // Latest wins for duplicates
            var ex = t.ExportMemory();
            Assert.Single(ex);
            Assert.Equal("y", ex[0].TargetText);
        }

        [Fact]
        public async Task TranslateAsync_ShieldTokensFullyRestored_NoLeak()
        {
            var fake = new FakeTranslator();
            var t = new PromptTranslator(fake.Translate)
                .AddGlossaryTerm("Brand");

            var prompt = "Use {{var}} with Brand and `code` here.";
            var result = await t.TranslateAsync(prompt, "en", "es");

            Assert.DoesNotContain("__SHIELD_", result);
            Assert.Contains("{{var}}", result);
            Assert.Contains("Brand", result);
            Assert.Contains("`code`", result);
        }
    }
}
