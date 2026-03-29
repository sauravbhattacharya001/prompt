namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Translates prompt templates between natural languages while preserving
    /// placeholder tokens (e.g., {{name}}, {role}), code blocks, and structural
    /// formatting. Provides a translation-memory cache so repeated segments
    /// are translated consistently across a project.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PromptTranslator is designed for offline/batch translation of prompt
    /// libraries. It does NOT call an external API — instead it accepts a
    /// user-supplied translation function, making it pluggable with any
    /// translation backend (DeepL, Google, GPT, or even a dictionary).
    /// </para>
    /// <para>
    /// Key features:
    /// - Placeholder preservation: tokens like {{variable}} are shielded
    ///   during translation and restored afterward.
    /// - Code block preservation: fenced code blocks (```) are kept verbatim.
    /// - Translation memory: previously translated segments are cached and
    ///   reused for consistency and speed.
    /// - Glossary support: terms that must not be translated (brand names,
    ///   technical terms) can be pinned.
    /// - Batch mode: translate an entire prompt library at once.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var translator = new PromptTranslator(async (text, from, to) =>
    /// {
    ///     // Call your translation API here
    ///     return await MyTranslationService.TranslateAsync(text, from, to);
    /// });
    ///
    /// translator.AddGlossaryTerm("AgentBox");
    /// translator.AddGlossaryTerm("OpenAI");
    ///
    /// string translated = await translator.TranslateAsync(
    ///     "You are a helpful {{role}}. Respond in {{language}}.",
    ///     fromLanguage: "en",
    ///     toLanguage: "es"
    /// );
    /// // => "Eres un {{role}} útil. Responde en {{language}}."
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptTranslator
    {
        private readonly Func<string, string, string, System.Threading.Tasks.Task<string>> _translateFunc;
        private readonly Dictionary<string, Dictionary<string, string>> _memory = new();
        private readonly HashSet<string> _glossary = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Regex> _placeholderPatterns = new();

        private static readonly Regex DefaultPlaceholderPattern =
            new(@"\{\{?\w+\}?\}", RegexOptions.Compiled);

        private static readonly Regex CodeBlockPattern =
            new(@"```[\s\S]*?```", RegexOptions.Compiled);

        private static readonly Regex InlineCodePattern =
            new(@"`[^`]+`", RegexOptions.Compiled);

        /// <summary>
        /// Creates a new PromptTranslator with the given translation function.
        /// The function receives (text, fromLanguage, toLanguage) and returns translated text.
        /// </summary>
        public PromptTranslator(Func<string, string, string, System.Threading.Tasks.Task<string>> translateFunc)
        {
            _translateFunc = translateFunc ?? throw new ArgumentNullException(nameof(translateFunc));
            _placeholderPatterns.Add(DefaultPlaceholderPattern);
        }

        /// <summary>
        /// Adds a custom placeholder pattern to preserve during translation.
        /// </summary>
        public PromptTranslator AddPlaceholderPattern(string regexPattern)
        {
            _placeholderPatterns.Add(new Regex(regexPattern, RegexOptions.Compiled));
            return this;
        }

        /// <summary>
        /// Adds a glossary term that should never be translated.
        /// </summary>
        public PromptTranslator AddGlossaryTerm(string term)
        {
            if (!string.IsNullOrWhiteSpace(term))
                _glossary.Add(term.Trim());
            return this;
        }

        /// <summary>
        /// Adds multiple glossary terms at once.
        /// </summary>
        public PromptTranslator AddGlossaryTerms(IEnumerable<string> terms)
        {
            foreach (var t in terms) AddGlossaryTerm(t);
            return this;
        }

        /// <summary>
        /// Translates a prompt template, preserving placeholders, code blocks,
        /// and glossary terms.
        /// </summary>
        public async System.Threading.Tasks.Task<string> TranslateAsync(
            string promptText, string fromLanguage, string toLanguage)
        {
            if (string.IsNullOrWhiteSpace(promptText)) return promptText ?? string.Empty;
            if (fromLanguage == toLanguage) return promptText;

            var shields = new Dictionary<string, string>();
            int counter = 0;

            string shielded = promptText;

            // Shield code blocks first (they may contain placeholders)
            shielded = CodeBlockPattern.Replace(shielded, m =>
            {
                var token = $"__SHIELD_{counter++}__";
                shields[token] = m.Value;
                return token;
            });

            shielded = InlineCodePattern.Replace(shielded, m =>
            {
                var token = $"__SHIELD_{counter++}__";
                shields[token] = m.Value;
                return token;
            });

            // Shield placeholders
            foreach (var pattern in _placeholderPatterns)
            {
                shielded = pattern.Replace(shielded, m =>
                {
                    var token = $"__SHIELD_{counter++}__";
                    shields[token] = m.Value;
                    return token;
                });
            }

            // Shield glossary terms
            foreach (var term in _glossary)
            {
                var escaped = Regex.Escape(term);
                shielded = Regex.Replace(shielded, $@"\b{escaped}\b", m =>
                {
                    var token = $"__SHIELD_{counter++}__";
                    shields[token] = m.Value;
                    return token;
                }, RegexOptions.IgnoreCase);
            }

            // Translate segments (split by lines to leverage memory)
            var lines = shielded.Split('\n');
            var translatedLines = new string[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                // Skip empty lines and lines that are only shield tokens
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    Regex.IsMatch(trimmed, @"^(__SHIELD_\d+__\s*)+$"))
                {
                    translatedLines[i] = line;
                    continue;
                }

                var memoryKey = $"{fromLanguage}>{toLanguage}:{trimmed}";
                if (_memory.TryGetValue(memoryKey, out var cached) &&
                    cached.TryGetValue(toLanguage, out var hit))
                {
                    // Preserve leading whitespace
                    var leading = line.Length - line.TrimStart().Length;
                    translatedLines[i] = line.Substring(0, leading) + hit;
                }
                else
                {
                    var leading = line.Length - line.TrimStart().Length;
                    var translated = await _translateFunc(trimmed, fromLanguage, toLanguage);

                    // Cache it
                    if (!_memory.ContainsKey(memoryKey))
                        _memory[memoryKey] = new Dictionary<string, string>();
                    _memory[memoryKey][toLanguage] = translated;

                    translatedLines[i] = line.Substring(0, leading) + translated;
                }
            }

            var result = string.Join("\n", translatedLines);

            // Restore shields in reverse order
            foreach (var kvp in shields.Reverse())
            {
                result = result.Replace(kvp.Key, kvp.Value);
            }

            return result;
        }

        /// <summary>
        /// Translates multiple prompts in batch, sharing the translation memory
        /// across all of them for consistency.
        /// </summary>
        public async System.Threading.Tasks.Task<Dictionary<string, string>> TranslateBatchAsync(
            IEnumerable<KeyValuePair<string, string>> prompts,
            string fromLanguage, string toLanguage)
        {
            var results = new Dictionary<string, string>();
            foreach (var kvp in prompts)
            {
                results[kvp.Key] = await TranslateAsync(kvp.Value, fromLanguage, toLanguage);
            }
            return results;
        }

        /// <summary>
        /// Returns the number of cached translation segments in memory.
        /// </summary>
        public int MemorySize => _memory.Count;

        /// <summary>
        /// Clears the translation memory cache.
        /// </summary>
        public void ClearMemory() => _memory.Clear();

        /// <summary>
        /// Exports the translation memory as a list of (source, target) pairs
        /// for persistence or review.
        /// </summary>
        public List<TranslationMemoryEntry> ExportMemory()
        {
            var entries = new List<TranslationMemoryEntry>();
            foreach (var kvp in _memory)
            {
                var parts = kvp.Key.Split(':', 2);
                var langPair = parts[0];
                var source = parts.Length > 1 ? parts[1] : "";
                var langs = langPair.Split('>');

                foreach (var translation in kvp.Value)
                {
                    entries.Add(new TranslationMemoryEntry
                    {
                        SourceLanguage = langs.Length > 0 ? langs[0] : "",
                        TargetLanguage = translation.Key,
                        SourceText = source,
                        TargetText = translation.Value
                    });
                }
            }
            return entries;
        }

        /// <summary>
        /// Imports translation memory entries, pre-populating the cache.
        /// </summary>
        public void ImportMemory(IEnumerable<TranslationMemoryEntry> entries)
        {
            foreach (var entry in entries)
            {
                var key = $"{entry.SourceLanguage}>{entry.TargetLanguage}:{entry.SourceText}";
                if (!_memory.ContainsKey(key))
                    _memory[key] = new Dictionary<string, string>();
                _memory[key][entry.TargetLanguage] = entry.TargetText;
            }
        }

        /// <summary>
        /// Returns the current glossary terms.
        /// </summary>
        public IReadOnlyCollection<string> GlossaryTerms => _glossary;
    }

    /// <summary>
    /// Represents a single entry in translation memory.
    /// </summary>
    public class TranslationMemoryEntry
    {
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public string SourceText { get; set; } = "";
        public string TargetText { get; set; } = "";
    }
}
