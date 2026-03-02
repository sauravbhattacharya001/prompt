namespace Prompt.Tests
{
    using Xunit;

    public class PromptLocalizerTests
    {
        [Fact]
        public void Register_And_Render_BasicLocalization()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("greeting", "en", new PromptTemplate("Hello, {{name}}!"));
            localizer.Register("greeting", "es", new PromptTemplate("¡Hola, {{name}}!"));

            var result = localizer.Render("greeting", "es",
                new Dictionary<string, string> { ["name"] = "Carlos" });

            Assert.Equal("¡Hola, Carlos!", result);
        }

        [Fact]
        public void Render_FallsBackToDefaultLocale()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("greeting", "en", new PromptTemplate("Hello!"));

            var result = localizer.Render("greeting", "ja");
            Assert.Equal("Hello!", result);
        }

        [Fact]
        public void Render_ThrowsWhenKeyNotFound()
        {
            var localizer = new PromptLocalizer();
            Assert.Throws<KeyNotFoundException>(() => localizer.Render("missing", "en"));
        }

        [Fact]
        public void Render_ThrowsWhenNoLocaleAndNoDefault()
        {
            var localizer = new PromptLocalizer();
            localizer.DefaultLocale = "en";
            localizer.Register("greeting", "fr", new PromptTemplate("Bonjour!"));

            Assert.Throws<KeyNotFoundException>(() => localizer.Render("greeting", "ja"));
        }

        [Fact]
        public void Render_DefaultLocaleOverload()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("greeting", "en", new PromptTemplate("Hello!"));

            Assert.Equal("Hello!", localizer.Render("greeting"));
        }

        [Fact]
        public void RegisterAll_AddsMultipleLocales()
        {
            var localizer = new PromptLocalizer();
            localizer.RegisterAll("msg", new Dictionary<string, PromptTemplate>
            {
                ["en"] = new PromptTemplate("Hi"),
                ["de"] = new PromptTemplate("Hallo"),
                ["fr"] = new PromptTemplate("Salut"),
            });

            Assert.Equal("Hallo", localizer.Render("msg", "de"));
            Assert.Equal("Salut", localizer.Render("msg", "fr"));
        }

        [Fact]
        public void HasTranslation_ReturnsCorrectly()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("msg", "en", new PromptTemplate("Hi"));

            Assert.True(localizer.HasTranslation("msg", "en"));
            Assert.False(localizer.HasTranslation("msg", "ja"));
            Assert.False(localizer.HasTranslation("nope", "en"));
        }

        [Fact]
        public void GetLocales_ReturnsRegisteredLocales()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("msg", "en", new PromptTemplate("Hi"));
            localizer.Register("msg", "fr", new PromptTemplate("Salut"));

            var locales = localizer.GetLocales("msg");
            Assert.Contains("en", locales);
            Assert.Contains("fr", locales);
            Assert.Equal(2, locales.Count);
        }

        [Fact]
        public void Remove_RemovesSpecificTranslation()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("msg", "en", new PromptTemplate("Hi"));
            localizer.Register("msg", "fr", new PromptTemplate("Salut"));

            Assert.True(localizer.Remove("msg", "fr"));
            Assert.False(localizer.HasTranslation("msg", "fr"));
            Assert.True(localizer.HasTranslation("msg", "en"));
        }

        [Fact]
        public void RemoveAll_RemovesEntireKey()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("msg", "en", new PromptTemplate("Hi"));
            localizer.Register("msg", "fr", new PromptTemplate("Salut"));

            Assert.True(localizer.RemoveAll("msg"));
            Assert.Empty(localizer.Keys);
        }

        [Fact]
        public void FindMissingTranslations_DetectsGaps()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("greeting", "en", new PromptTemplate("Hi"));
            localizer.Register("greeting", "fr", new PromptTemplate("Salut"));
            localizer.Register("farewell", "en", new PromptTemplate("Bye"));
            // "farewell" missing "fr"

            var missing = localizer.FindMissingTranslations();
            Assert.True(missing.ContainsKey("farewell"));
            Assert.Contains("fr", missing["farewell"]);
        }

        [Fact]
        public void GetCoverageReport_ShowsPercentages()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("a", "en", new PromptTemplate("A"));
            localizer.Register("a", "fr", new PromptTemplate("A-fr"));
            localizer.Register("b", "en", new PromptTemplate("B"));

            var report = localizer.GetCoverageReport();
            Assert.Equal(100.0, report["a"].CoveragePercent);
            Assert.Equal(50.0, report["b"].CoveragePercent);
        }

        [Fact]
        public void ExportAndImport_RoundTrips()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("msg", "en", new PromptTemplate("Hello {{name}}"));
            localizer.Register("msg", "es", new PromptTemplate("Hola {{name}}"));

            var json = localizer.ExportToJson();

            var localizer2 = new PromptLocalizer();
            localizer2.ImportFromJson(json);

            Assert.Equal("Hello World",
                localizer2.Render("msg", "en", new Dictionary<string, string> { ["name"] = "World" }));
            Assert.Equal("Hola World",
                localizer2.Render("msg", "es", new Dictionary<string, string> { ["name"] = "World" }));
        }

        [Fact]
        public void Import_WithoutOverwrite_PreservesExisting()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("msg", "en", new PromptTemplate("Original"));

            localizer.ImportFromJson("{\"msg\":{\"en\":\"New\",\"fr\":\"Nouveau\"}}", overwrite: false);

            Assert.Equal("Original", localizer.Render("msg", "en"));
            Assert.Equal("Nouveau", localizer.Render("msg", "fr"));
        }

        [Fact]
        public void LocaleNormalization_CaseInsensitive()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("msg", "EN", new PromptTemplate("Hi"));

            Assert.True(localizer.HasTranslation("msg", "en"));
            Assert.Equal("Hi", localizer.Render("msg", "En"));
        }

        [Fact]
        public void Register_ThrowsOnNullArguments()
        {
            var localizer = new PromptLocalizer();
            Assert.Throws<ArgumentException>(() => localizer.Register("", "en", new PromptTemplate("x")));
            Assert.Throws<ArgumentException>(() => localizer.Register("k", "", new PromptTemplate("x")));
            Assert.Throws<ArgumentNullException>(() => localizer.Register("k", "en", null!));
        }

        [Fact]
        public void DefaultLocale_ThrowsOnEmpty()
        {
            var localizer = new PromptLocalizer();
            Assert.Throws<ArgumentException>(() => localizer.DefaultLocale = "");
        }

        [Fact]
        public void Keys_ReturnsAllRegisteredKeys()
        {
            var localizer = new PromptLocalizer();
            localizer.Register("a", "en", new PromptTemplate("A"));
            localizer.Register("b", "en", new PromptTemplate("B"));

            Assert.Contains("a", localizer.Keys);
            Assert.Contains("b", localizer.Keys);
        }
    }
}
