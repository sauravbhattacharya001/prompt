namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Manages prompt templates across multiple locales, enabling easy
    /// localization of AI prompts for international applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports locale fallback chains (e.g. "fr-CA" → "fr" → default),
    /// missing translation detection, and bulk export/import of translation bundles.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var manager = new PromptLocalizationManager("en");
    /// manager.AddTranslation("greeting", "en", "Hello, {{name}}!");
    /// manager.AddTranslation("greeting", "es", "¡Hola, {{name}}!");
    /// manager.AddTranslation("greeting", "fr", "Bonjour, {{name}} !");
    ///
    /// string prompt = manager.GetPrompt("greeting", "es");
    /// // → "¡Hola, {{name}}!"
    ///
    /// // With variable rendering:
    /// string rendered = manager.RenderPrompt("greeting", "fr",
    ///     new Dictionary&lt;string, string&gt; { ["name"] = "Claude" });
    /// // → "Bonjour, Claude !"
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptLocalizationManager
    {
        private readonly string _defaultLocale;
        private readonly Dictionary<string, Dictionary<string, string>> _translations;
        private readonly Dictionary<string, List<string>> _fallbackChains;

        /// <summary>
        /// Creates a new localization manager with the specified default locale.
        /// </summary>
        /// <param name="defaultLocale">The fallback locale when a translation is not found.</param>
        public PromptLocalizationManager(string defaultLocale = "en")
        {
            _defaultLocale = NormalizeLocale(defaultLocale);
            _translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            _fallbackChains = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the default locale.</summary>
        public string DefaultLocale => _defaultLocale;

        /// <summary>Gets all registered prompt keys.</summary>
        public IReadOnlyCollection<string> PromptKeys => _translations.Keys.ToList().AsReadOnly();

        /// <summary>Gets all locales that have at least one translation.</summary>
        public IReadOnlyCollection<string> Locales =>
            _translations.Values
                .SelectMany(d => d.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();

        /// <summary>
        /// Adds or updates a translation for a prompt key in a specific locale.
        /// </summary>
        public void AddTranslation(string key, string locale, string template)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            if (string.IsNullOrWhiteSpace(locale))
                throw new ArgumentException("Locale cannot be null or empty.", nameof(locale));
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            locale = NormalizeLocale(locale);

            if (!_translations.TryGetValue(key, out var localeMap))
            {
                localeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _translations[key] = localeMap;
            }

            localeMap[locale] = template;
        }

        /// <summary>
        /// Adds multiple translations at once from a dictionary.
        /// </summary>
        public void AddTranslations(string key, IDictionary<string, string> localeTemplates)
        {
            if (localeTemplates == null) throw new ArgumentNullException(nameof(localeTemplates));
            foreach (var (locale, template) in localeTemplates)
            {
                AddTranslation(key, locale, template);
            }
        }

        /// <summary>
        /// Sets a custom fallback chain for a locale (e.g. "pt-BR" → ["pt", "es", "en"]).
        /// </summary>
        public void SetFallbackChain(string locale, IEnumerable<string> fallbacks)
        {
            locale = NormalizeLocale(locale);
            _fallbackChains[locale] = fallbacks.Select(NormalizeLocale).ToList();
        }

        /// <summary>
        /// Gets the prompt template for a key in the specified locale,
        /// following the fallback chain if needed.
        /// </summary>
        /// <returns>The template string, or null if not found in any fallback.</returns>
        public string GetPrompt(string key, string locale = null)
        {
            locale = NormalizeLocale(locale ?? _defaultLocale);

            if (!_translations.TryGetValue(key, out var localeMap))
                return null;

            // Try exact match
            if (localeMap.TryGetValue(locale, out var template))
                return template;

            // Try custom fallback chain
            if (_fallbackChains.TryGetValue(locale, out var chain))
            {
                foreach (var fb in chain)
                {
                    if (localeMap.TryGetValue(fb, out template))
                        return template;
                }
            }

            // Try language-only fallback (e.g. "fr-CA" → "fr")
            var dashIndex = locale.IndexOf('-');
            if (dashIndex > 0)
            {
                var langOnly = locale.Substring(0, dashIndex);
                if (localeMap.TryGetValue(langOnly, out template))
                    return template;
            }

            // Fall back to default locale
            if (!string.Equals(locale, _defaultLocale, StringComparison.OrdinalIgnoreCase)
                && localeMap.TryGetValue(_defaultLocale, out template))
                return template;

            return null;
        }

        /// <summary>
        /// Gets and renders a prompt with variable substitution.
        /// Uses <c>{{variable}}</c> syntax.
        /// </summary>
        public string RenderPrompt(string key, string locale, IDictionary<string, string> variables = null)
        {
            var template = GetPrompt(key, locale);
            if (template == null)
                throw new KeyNotFoundException($"No translation found for key '{key}' in locale '{locale}' or any fallback.");

            if (variables == null || variables.Count == 0)
                return template;

            var result = template;
            foreach (var (varName, value) in variables)
            {
                result = result.Replace("{{" + varName + "}}", value ?? string.Empty);
            }
            return result;
        }

        /// <summary>
        /// Checks if a translation exists for a key in a specific locale (exact match, no fallback).
        /// </summary>
        public bool HasTranslation(string key, string locale)
        {
            locale = NormalizeLocale(locale);
            return _translations.TryGetValue(key, out var map) && map.ContainsKey(locale);
        }

        /// <summary>
        /// Removes a translation for a specific key and locale.
        /// </summary>
        public bool RemoveTranslation(string key, string locale)
        {
            locale = NormalizeLocale(locale);
            if (_translations.TryGetValue(key, out var map))
            {
                var removed = map.Remove(locale);
                if (map.Count == 0) _translations.Remove(key);
                return removed;
            }
            return false;
        }

        /// <summary>
        /// Removes all translations for a prompt key.
        /// </summary>
        public bool RemoveKey(string key) => _translations.Remove(key);

        /// <summary>
        /// Finds prompt keys that are missing translations for a given locale.
        /// </summary>
        public IReadOnlyList<string> FindMissingTranslations(string locale)
        {
            locale = NormalizeLocale(locale);
            return _translations
                .Where(kv => !kv.Value.ContainsKey(locale))
                .Select(kv => kv.Key)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Returns a coverage report: for each locale, what percentage of keys have translations.
        /// </summary>
        public Dictionary<string, double> GetCoverageReport()
        {
            var allLocales = Locales;
            var totalKeys = _translations.Count;
            if (totalKeys == 0) return new Dictionary<string, double>();

            var report = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var locale in allLocales)
            {
                var count = _translations.Values.Count(m => m.ContainsKey(locale));
                report[locale] = Math.Round((double)count / totalKeys * 100, 1);
            }
            return report;
        }

        /// <summary>
        /// Exports all translations as a JSON string for sharing or backup.
        /// </summary>
        public string ExportJson(bool indented = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(_translations, options);
        }

        /// <summary>
        /// Imports translations from a JSON string (merges with existing).
        /// </summary>
        public int ImportJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or empty.", nameof(json));

            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            if (data == null) return 0;

            int count = 0;
            foreach (var (key, localeMap) in data)
            {
                foreach (var (locale, template) in localeMap)
                {
                    AddTranslation(key, locale, template);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Clones a prompt key's translations to a new key name.
        /// </summary>
        public void CloneKey(string sourceKey, string targetKey)
        {
            if (!_translations.TryGetValue(sourceKey, out var map))
                throw new KeyNotFoundException($"Source key '{sourceKey}' not found.");

            foreach (var (locale, template) in map)
            {
                AddTranslation(targetKey, locale, template);
            }
        }

        /// <summary>
        /// Gets a summary of all translations for a key across locales.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAllTranslations(string key)
        {
            if (_translations.TryGetValue(key, out var map))
                return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
            return new Dictionary<string, string>();
        }

        private static string NormalizeLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return locale;
            return locale.Trim().ToLowerInvariant();
        }
    }
}
