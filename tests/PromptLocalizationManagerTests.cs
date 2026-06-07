namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class PromptLocalizationManagerTests
    {
        [Fact]
        public void AddAndGetTranslation_ReturnsCorrectTemplate()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello!");
            mgr.AddTranslation("greet", "es", "¡Hola!");

            Assert.Equal("Hello!", mgr.GetPrompt("greet", "en"));
            Assert.Equal("¡Hola!", mgr.GetPrompt("greet", "es"));
        }

        [Fact]
        public void GetPrompt_FallsBackToDefaultLocale()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello!");

            Assert.Equal("Hello!", mgr.GetPrompt("greet", "fr"));
        }

        [Fact]
        public void GetPrompt_LanguageOnlyFallback()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "fr", "Bonjour !");
            mgr.AddTranslation("greet", "en", "Hello!");

            Assert.Equal("Bonjour !", mgr.GetPrompt("greet", "fr-CA"));
        }

        [Fact]
        public void CustomFallbackChain_IsRespected()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "es", "¡Hola!");
            mgr.AddTranslation("greet", "en", "Hello!");
            mgr.SetFallbackChain("pt-BR", new[] { "pt", "es", "en" });

            Assert.Equal("¡Hola!", mgr.GetPrompt("greet", "pt-BR"));
        }

        [Fact]
        public void RenderPrompt_SubstitutesVariables()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello, {{name}}! You have {{count}} messages.");

            var result = mgr.RenderPrompt("greet", "en", new Dictionary<string, string>
            {
                ["name"] = "Alice",
                ["count"] = "5"
            });

            Assert.Equal("Hello, Alice! You have 5 messages.", result);
        }

        [Fact]
        public void RenderPrompt_ThrowsWhenKeyNotFound()
        {
            var mgr = new PromptLocalizationManager("en");
            Assert.Throws<KeyNotFoundException>(() => mgr.RenderPrompt("missing", "en"));
        }

        [Fact]
        public void FindMissingTranslations_ReturnsCorrectKeys()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello!");
            mgr.AddTranslation("greet", "es", "¡Hola!");
            mgr.AddTranslation("bye", "en", "Goodbye!");

            var missing = mgr.FindMissingTranslations("es");
            Assert.Single(missing);
            Assert.Contains("bye", missing);
        }

        [Fact]
        public void CoverageReport_CalculatesCorrectly()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("a", "en", "A");
            mgr.AddTranslation("a", "es", "A-es");
            mgr.AddTranslation("b", "en", "B");

            var report = mgr.GetCoverageReport();
            Assert.Equal(100.0, report["en"]);
            Assert.Equal(50.0, report["es"]);
        }

        [Fact]
        public void ExportImportJson_RoundTrips()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello!");
            mgr.AddTranslation("greet", "ja", "こんにちは！");

            var json = mgr.ExportJson();

            var mgr2 = new PromptLocalizationManager("en");
            var count = mgr2.ImportJson(json);

            Assert.Equal(2, count);
            Assert.Equal("こんにちは！", mgr2.GetPrompt("greet", "ja"));
        }

        [Fact]
        public void RemoveTranslation_Works()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello!");
            mgr.AddTranslation("greet", "es", "¡Hola!");

            Assert.True(mgr.RemoveTranslation("greet", "es"));
            // After removal, the exact "es" translation should be gone
            Assert.False(mgr.HasTranslation("greet", "es"));
            // The "en" translation should still exist
            Assert.Equal("Hello!", mgr.GetPrompt("greet", "en"));
        }

        [Fact]
        public void CloneKey_CopiesAllTranslations()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello!");
            mgr.AddTranslation("greet", "de", "Hallo!");

            mgr.CloneKey("greet", "welcome");

            Assert.Equal("Hello!", mgr.GetPrompt("welcome", "en"));
            Assert.Equal("Hallo!", mgr.GetPrompt("welcome", "de"));
        }

        [Fact]
        public void HasTranslation_ExactMatchOnly()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("greet", "en", "Hello!");

            Assert.True(mgr.HasTranslation("greet", "en"));
            Assert.False(mgr.HasTranslation("greet", "fr"));
        }

        [Fact]
        public void AddTranslation_ThrowsOnNullKey()
        {
            var mgr = new PromptLocalizationManager();
            Assert.Throws<ArgumentException>(() => mgr.AddTranslation(null, "en", "test"));
        }

        [Fact]
        public void Locales_ReturnsAllDistinctLocales()
        {
            var mgr = new PromptLocalizationManager("en");
            mgr.AddTranslation("a", "en", "A");
            mgr.AddTranslation("a", "fr", "A-fr");
            mgr.AddTranslation("b", "en", "B");
            mgr.AddTranslation("b", "de", "B-de");

            var locales = mgr.Locales;
            Assert.Contains("en", locales);
            Assert.Contains("fr", locales);
            Assert.Contains("de", locales);
            Assert.Equal(3, locales.Count);
        }
    }
}
