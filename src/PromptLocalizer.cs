namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Manages localized prompt templates, enabling internationalization (i18n)
    /// of prompts. Register templates per locale and render them with the
    /// appropriate language at runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var localizer = new PromptLocalizer();
    /// localizer.Register("greeting", "en",
    ///     new PromptTemplate("Hello, {{name}}! How can I help you today?"));
    /// localizer.Register("greeting", "es",
    ///     new PromptTemplate("¡Hola, {{name}}! ¿Cómo puedo ayudarte hoy?"));
    /// localizer.Register("greeting", "fr",
    ///     new PromptTemplate("Bonjour, {{name}}! Comment puis-je vous aider?"));
    ///
    /// localizer.DefaultLocale = "en";
    ///
    /// string prompt = localizer.Render("greeting", "es",
    ///     new Dictionary&lt;string, string&gt; { ["name"] = "Carlos" });
    /// // → "¡Hola, Carlos! ¿Cómo puedo ayudarte hoy?"
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptLocalizer
    {
        private readonly Dictionary<string, Dictionary<string, PromptTemplate>> _templates = new();
        private string _defaultLocale = "en";

        /// <summary>
        /// Gets or sets the default locale used when a requested locale is not found.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is null or whitespace.</exception>
        public string DefaultLocale
        {
            get => _defaultLocale;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Default locale cannot be null or empty.", nameof(value));
                _defaultLocale = value.Trim().ToLowerInvariant();
            }
        }

        /// <summary>
        /// Gets all registered template keys.
        /// </summary>
        public IReadOnlyCollection<string> Keys => _templates.Keys.ToList().AsReadOnly();

        /// <summary>
        /// Registers a prompt template for a specific key and locale.
        /// </summary>
        /// <param name="key">Template identifier (e.g., "greeting", "error-message").</param>
        /// <param name="locale">Locale code (e.g., "en", "es", "fr", "de", "ja").</param>
        /// <param name="template">The prompt template for this locale.</param>
        /// <exception cref="ArgumentException">Thrown when key or locale is null/empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when template is null.</exception>
        public void Register(string key, string locale, PromptTemplate template)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            if (string.IsNullOrWhiteSpace(locale))
                throw new ArgumentException("Locale cannot be null or empty.", nameof(locale));
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var normalizedLocale = locale.Trim().ToLowerInvariant();

            if (!_templates.ContainsKey(key))
                _templates[key] = new Dictionary<string, PromptTemplate>();

            _templates[key][normalizedLocale] = template;
        }

        /// <summary>
        /// Registers multiple templates for a key at once.
        /// </summary>
        /// <param name="key">Template identifier.</param>
        /// <param name="translations">Dictionary of locale → template pairs.</param>
        public void RegisterAll(string key, Dictionary<string, PromptTemplate> translations)
        {
            if (translations == null) throw new ArgumentNullException(nameof(translations));
            foreach (var (locale, template) in translations)
                Register(key, locale, template);
        }

        /// <summary>
        /// Renders a localized template with the given variables.
        /// Falls back to default locale if the requested locale is not found.
        /// </summary>
        /// <param name="key">Template identifier.</param>
        /// <param name="locale">Desired locale.</param>
        /// <param name="variables">Variables to fill in the template.</param>
        /// <returns>The rendered prompt string.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when the key is not registered, or neither the requested
        /// locale nor the default locale has a template.
        /// </exception>
        public string Render(string key, string locale, Dictionary<string, string>? variables = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));

            if (!_templates.TryGetValue(key, out var locales))
                throw new KeyNotFoundException($"No templates registered for key '{key}'.");

            var normalizedLocale = locale?.Trim().ToLowerInvariant() ?? _defaultLocale;

            // Try exact match, then default locale
            if (!locales.TryGetValue(normalizedLocale, out var template))
            {
                if (!locales.TryGetValue(_defaultLocale, out template))
                    throw new KeyNotFoundException(
                        $"No template for locale '{normalizedLocale}' or default '{_defaultLocale}' under key '{key}'.");
            }

            return template.Render(variables ?? new Dictionary<string, string>());
        }

        /// <summary>
        /// Renders using the default locale.
        /// </summary>
        public string Render(string key, Dictionary<string, string>? variables = null)
            => Render(key, _defaultLocale, variables);

        /// <summary>
        /// Checks if a specific key and locale combination is registered.
        /// </summary>
        public bool HasTranslation(string key, string locale)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(locale))
                return false;
            return _templates.TryGetValue(key, out var locales)
                && locales.ContainsKey(locale.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Gets all registered locales for a given key.
        /// </summary>
        /// <param name="key">Template identifier.</param>
        /// <returns>List of locale codes.</returns>
        public IReadOnlyList<string> GetLocales(string key)
        {
            if (!_templates.TryGetValue(key, out var locales))
                return Array.Empty<string>();
            return locales.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Removes a specific locale translation for a key.
        /// </summary>
        /// <returns>True if the translation was found and removed.</returns>
        public bool Remove(string key, string locale)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(locale))
                return false;
            if (!_templates.TryGetValue(key, out var locales))
                return false;

            var removed = locales.Remove(locale.Trim().ToLowerInvariant());
            if (locales.Count == 0)
                _templates.Remove(key);
            return removed;
        }

        /// <summary>
        /// Removes all translations for a key.
        /// </summary>
        public bool RemoveAll(string key) => _templates.Remove(key);

        /// <summary>
        /// Identifies missing translations across all keys.
        /// Returns a dictionary of key → list of locales that are registered
        /// for other keys but missing for this one.
        /// </summary>
        public Dictionary<string, List<string>> FindMissingTranslations()
        {
            var allLocales = _templates.Values
                .SelectMany(l => l.Keys)
                .Distinct()
                .ToList();

            var missing = new Dictionary<string, List<string>>();
            foreach (var (key, locales) in _templates)
            {
                var gaps = allLocales.Where(l => !locales.ContainsKey(l)).ToList();
                if (gaps.Count > 0)
                    missing[key] = gaps;
            }
            return missing;
        }

        /// <summary>
        /// Gets a coverage report showing how many locales each key supports.
        /// </summary>
        public Dictionary<string, LocalizationCoverage> GetCoverageReport()
        {
            var allLocales = _templates.Values
                .SelectMany(l => l.Keys)
                .Distinct()
                .ToList();

            var report = new Dictionary<string, LocalizationCoverage>();
            foreach (var (key, locales) in _templates)
            {
                report[key] = new LocalizationCoverage
                {
                    RegisteredLocales = locales.Keys.ToList(),
                    MissingLocales = allLocales.Where(l => !locales.ContainsKey(l)).ToList(),
                    CoveragePercent = allLocales.Count > 0
                        ? Math.Round(100.0 * locales.Count / allLocales.Count, 1)
                        : 100.0
                };
            }
            return report;
        }

        /// <summary>
        /// Exports all registrations to a serializable format.
        /// </summary>
        public string ExportToJson()
        {
            var export = new Dictionary<string, Dictionary<string, string>>();
            foreach (var (key, locales) in _templates)
            {
                export[key] = new Dictionary<string, string>();
                foreach (var (locale, template) in locales)
                    export[key][locale] = template.Template;
            }
            return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Imports templates from JSON (exported via <see cref="ExportToJson"/>).
        /// </summary>
        /// <param name="json">JSON string with key → locale → template text mapping.</param>
        /// <param name="overwrite">If true, overwrites existing translations.</param>
        public void ImportFromJson(string json, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or empty.", nameof(json));

            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            if (data == null) return;

            foreach (var (key, locales) in data)
            {
                foreach (var (locale, templateText) in locales)
                {
                    if (!overwrite && HasTranslation(key, locale))
                        continue;
                    Register(key, locale, new PromptTemplate(templateText));
                }
            }
        }
    }

    /// <summary>
    /// Coverage information for a single template key.
    /// </summary>
    public class LocalizationCoverage
    {
        /// <summary>Locales that have translations for this key.</summary>
        [JsonPropertyName("registeredLocales")]
        public List<string> RegisteredLocales { get; set; } = new();

        /// <summary>Locales used elsewhere but missing for this key.</summary>
        [JsonPropertyName("missingLocales")]
        public List<string> MissingLocales { get; set; } = new();

        /// <summary>Percentage of all known locales covered by this key.</summary>
        [JsonPropertyName("coveragePercent")]
        public double CoveragePercent { get; set; }
    }
}
