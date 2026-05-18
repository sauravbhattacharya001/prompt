namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A glossary term with its canonical form, definition, and known variants.
    /// </summary>
    public class GlossaryTerm
    {
        /// <summary>Gets or sets the canonical (preferred) term.</summary>
        public string Canonical { get; set; } = "";

        /// <summary>Gets or sets the definition or description of the term.</summary>
        public string Definition { get; set; } = "";

        /// <summary>Gets or sets the category/domain this term belongs to.</summary>
        public string Category { get; set; } = "General";

        /// <summary>Gets or sets known variant spellings or synonyms that should map to the canonical form.</summary>
        public List<string> Variants { get; set; } = new();

        /// <summary>Gets or sets optional usage examples.</summary>
        public List<string> Examples { get; set; } = new();
    }

    /// <summary>
    /// A detected terminology inconsistency within a prompt or library.
    /// </summary>
    public class TermInconsistency
    {
        /// <summary>Gets or sets the variant form that was detected.</summary>
        public string Found { get; set; } = "";

        /// <summary>Gets or sets the canonical form it should be replaced with.</summary>
        public string Canonical { get; set; } = "";

        /// <summary>Gets or sets the prompt name or key where the inconsistency was found.</summary>
        public string PromptKey { get; set; } = "";

        /// <summary>Gets or sets the character position within the prompt text.</summary>
        public int Position { get; set; }

        /// <summary>Gets or sets a short context snippet around the inconsistency.</summary>
        public string Context { get; set; } = "";
    }

    /// <summary>
    /// A frequency entry for a detected term or phrase.
    /// </summary>
    public class TermFrequency
    {
        /// <summary>Gets or sets the term or phrase.</summary>
        public string Term { get; set; } = "";

        /// <summary>Gets or sets how many times it appears across the library.</summary>
        public int Count { get; set; }

        /// <summary>Gets or sets which prompt keys contain this term.</summary>
        public List<string> PromptKeys { get; set; } = new();
    }

    /// <summary>
    /// Result of scanning a prompt library for terminology issues.
    /// </summary>
    public class GlossaryScanResult
    {
        /// <summary>Gets or sets the total number of prompts scanned.</summary>
        public int PromptsScanned { get; set; }

        /// <summary>Gets or sets the total number of inconsistencies found.</summary>
        public int TotalInconsistencies { get; set; }

        /// <summary>Gets or sets the list of inconsistencies.</summary>
        public List<TermInconsistency> Inconsistencies { get; set; } = new();

        /// <summary>Gets or sets undefined terms (used but not in glossary).</summary>
        public List<TermFrequency> UndefinedTerms { get; set; } = new();

        /// <summary>Gets or sets the consistency score (0-100).</summary>
        public double ConsistencyScore { get; set; }

        /// <summary>Serializes the result to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Manages a domain glossary for prompt libraries — defines canonical terms,
    /// detects variant/inconsistent usage, extracts key terms, and enforces
    /// terminology standardization across prompts.
    /// </summary>
    /// <remarks>
    /// <para><b>Use cases:</b></para>
    /// <list type="bullet">
    ///   <item>Build and maintain a glossary of domain-specific terms</item>
    ///   <item>Scan prompts for inconsistent terminology</item>
    ///   <item>Auto-standardize prompts to use canonical terms</item>
    ///   <item>Extract key terms and compute frequency across a library</item>
    ///   <item>Export glossary as Markdown, JSON, or CSV</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var glossary = new PromptGlossary();
    /// glossary.AddTerm("LLM", "Large Language Model", "AI",
    ///     variants: new[] { "large language model", "language model" });
    /// glossary.AddTerm("prompt", "Input text sent to an LLM", "Core",
    ///     variants: new[] { "query", "instruction" });
    ///
    /// var result = glossary.Scan(myPromptLibrary);
    /// Console.WriteLine($"Consistency: {result.ConsistencyScore:F1}%");
    ///
    /// string standardized = glossary.Standardize("Send query to the language model");
    /// // → "Send prompt to the LLM"
    /// </code>
    /// </example>
    public class PromptGlossary
    {
        private readonly List<GlossaryTerm> _terms = new();
        private readonly Dictionary<string, GlossaryTerm> _variantIndex = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the number of terms in the glossary.</summary>
        public int Count => _terms.Count;

        /// <summary>Gets a read-only list of all glossary terms.</summary>
        public IReadOnlyList<GlossaryTerm> Terms => _terms.AsReadOnly();

        /// <summary>
        /// Adds a term to the glossary.
        /// </summary>
        /// <param name="canonical">The preferred/canonical form of the term.</param>
        /// <param name="definition">A short definition.</param>
        /// <param name="category">Domain category (e.g. "AI", "Security", "Core").</param>
        /// <param name="variants">Alternative spellings or synonyms.</param>
        /// <param name="examples">Optional usage examples.</param>
        /// <returns>The created <see cref="GlossaryTerm"/>.</returns>
        public GlossaryTerm AddTerm(string canonical, string definition, string category = "General",
            IEnumerable<string>? variants = null, IEnumerable<string>? examples = null)
        {
            if (string.IsNullOrWhiteSpace(canonical))
                throw new ArgumentException("Canonical term cannot be empty.", nameof(canonical));

            var term = new GlossaryTerm
            {
                Canonical = canonical,
                Definition = definition,
                Category = category,
                Variants = variants?.ToList() ?? new List<string>(),
                Examples = examples?.ToList() ?? new List<string>()
            };

            _terms.Add(term);

            // Index the canonical form and all variants
            _variantIndex[canonical] = term;
            foreach (var v in term.Variants)
            {
                _variantIndex[v] = term;
            }

            return term;
        }

        /// <summary>
        /// Removes a term by its canonical name.
        /// </summary>
        /// <returns>True if the term was found and removed.</returns>
        public bool RemoveTerm(string canonical)
        {
            var term = _terms.FirstOrDefault(t => t.Canonical.Equals(canonical, StringComparison.OrdinalIgnoreCase));
            if (term == null) return false;

            _variantIndex.Remove(term.Canonical);
            foreach (var v in term.Variants)
                _variantIndex.Remove(v);

            _terms.Remove(term);
            return true;
        }

        /// <summary>
        /// Looks up a term by canonical name or any variant.
        /// </summary>
        /// <returns>The matching <see cref="GlossaryTerm"/>, or null.</returns>
        public GlossaryTerm? Lookup(string term)
        {
            return _variantIndex.TryGetValue(term, out var result) ? result : null;
        }

        /// <summary>
        /// Returns all terms in a given category.
        /// </summary>
        public IEnumerable<GlossaryTerm> GetByCategory(string category)
        {
            return _terms.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns all distinct categories in the glossary.
        /// </summary>
        public IEnumerable<string> GetCategories()
        {
            return _terms.Select(t => t.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c);
        }

        /// <summary>
        /// Scans a dictionary of prompts (key → text) for terminology inconsistencies.
        /// Detects variant usage where the canonical form should be preferred.
        /// </summary>
        /// <param name="prompts">Dictionary of prompt key → prompt text.</param>
        /// <returns>A <see cref="GlossaryScanResult"/> with findings.</returns>
        public GlossaryScanResult Scan(IDictionary<string, string> prompts)
        {
            var result = new GlossaryScanResult { PromptsScanned = prompts.Count };
            int totalTermOccurrences = 0;
            int canonicalOccurrences = 0;

            foreach (var kvp in prompts)
            {
                var text = kvp.Value;
                foreach (var term in _terms)
                {
                    // Check for variant usage
                    foreach (var variant in term.Variants)
                    {
                        var matches = FindWholeWord(text, variant);
                        foreach (int pos in matches)
                        {
                            totalTermOccurrences++;
                            int contextStart = Math.Max(0, pos - 20);
                            int contextEnd = Math.Min(text.Length, pos + variant.Length + 20);
                            result.Inconsistencies.Add(new TermInconsistency
                            {
                                Found = variant,
                                Canonical = term.Canonical,
                                PromptKey = kvp.Key,
                                Position = pos,
                                Context = text[contextStart..contextEnd]
                            });
                        }
                    }

                    // Count canonical usage
                    var canonicalMatches = FindWholeWord(text, term.Canonical);
                    canonicalOccurrences += canonicalMatches.Count;
                    totalTermOccurrences += canonicalMatches.Count;
                }
            }

            result.TotalInconsistencies = result.Inconsistencies.Count;
            result.ConsistencyScore = totalTermOccurrences > 0
                ? Math.Round((double)canonicalOccurrences / totalTermOccurrences * 100, 1)
                : 100.0;

            return result;
        }

        /// <summary>
        /// Standardizes a prompt text by replacing all known variants with their canonical forms.
        /// </summary>
        /// <param name="text">The prompt text to standardize.</param>
        /// <returns>The standardized text.</returns>
        public string Standardize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Sort variants by length descending to replace longer matches first
            var replacements = _terms
                .SelectMany(t => t.Variants.Select(v => new { Variant = v, t.Canonical }))
                .OrderByDescending(r => r.Variant.Length)
                .ToList();

            foreach (var r in replacements)
            {
                text = ReplaceWholeWord(text, r.Variant, r.Canonical);
            }

            return text;
        }

        /// <summary>
        /// Extracts key terms and their frequencies from a set of prompts.
        /// Returns terms sorted by frequency descending.
        /// </summary>
        /// <param name="prompts">Dictionary of prompt key → prompt text.</param>
        /// <param name="minLength">Minimum word length to consider (default 4).</param>
        /// <param name="topN">Maximum number of terms to return (default 50).</param>
        /// <returns>List of <see cref="TermFrequency"/> entries.</returns>
        public List<TermFrequency> ExtractTerms(IDictionary<string, string> prompts, int minLength = 4, int topN = 50)
        {
            var termMap = new Dictionary<string, TermFrequency>(StringComparer.OrdinalIgnoreCase);
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "that", "this", "with", "from", "will", "have", "been", "were", "they",
                "their", "them", "then", "than", "when", "what", "which", "where", "your",
                "about", "would", "could", "should", "each", "other", "into", "more", "some",
                "also", "only", "just", "such", "very", "most", "does", "here", "there"
            };

            foreach (var kvp in prompts)
            {
                var words = Regex.Matches(kvp.Value, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b")
                    .Cast<Match>()
                    .Select(m => m.Value)
                    .Where(w => w.Length >= minLength && !stopWords.Contains(w));

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var word in words)
                {
                    if (!termMap.TryGetValue(word, out var freq))
                    {
                        freq = new TermFrequency { Term = word, Count = 0, PromptKeys = new() };
                        termMap[word] = freq;
                    }
                    freq.Count++;
                    if (seen.Add(word))
                        freq.PromptKeys.Add(kvp.Key);
                }
            }

            return termMap.Values
                .OrderByDescending(f => f.Count)
                .Take(topN)
                .ToList();
        }

        /// <summary>
        /// Suggests glossary entries from extracted terms that aren't already in the glossary.
        /// </summary>
        /// <param name="prompts">Dictionary of prompt key → prompt text.</param>
        /// <param name="minOccurrences">Minimum occurrence count to suggest (default 3).</param>
        /// <param name="topN">Maximum suggestions (default 20).</param>
        /// <returns>List of suggested terms with frequency data.</returns>
        public List<TermFrequency> SuggestTerms(IDictionary<string, string> prompts, int minOccurrences = 3, int topN = 20)
        {
            return ExtractTerms(prompts, topN: 200)
                .Where(f => f.Count >= minOccurrences && Lookup(f.Term) == null)
                .Take(topN)
                .ToList();
        }

        /// <summary>
        /// Exports the glossary as a Markdown table.
        /// </summary>
        /// <param name="title">Optional title for the document.</param>
        /// <returns>Markdown string.</returns>
        public string ToMarkdown(string title = "Prompt Glossary")
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {title}");
            sb.AppendLine();

            foreach (var category in GetCategories())
            {
                sb.AppendLine($"## {category}");
                sb.AppendLine();
                sb.AppendLine("| Term | Definition | Variants |");
                sb.AppendLine("|------|-----------|----------|");

                foreach (var term in GetByCategory(category).OrderBy(t => t.Canonical))
                {
                    var variants = term.Variants.Count > 0
                        ? string.Join(", ", term.Variants)
                        : "—";
                    sb.AppendLine($"| **{term.Canonical}** | {term.Definition} | {variants} |");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the glossary as a CSV string.
        /// </summary>
        /// <returns>CSV string with headers.</returns>
        public string ToCsv()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Canonical,Definition,Category,Variants");

            foreach (var term in _terms.OrderBy(t => t.Category).ThenBy(t => t.Canonical))
            {
                var def = CsvEscape(term.Definition);
                var variants = CsvEscape(string.Join("; ", term.Variants));
                sb.AppendLine($"{CsvEscape(term.Canonical)},{def},{CsvEscape(term.Category)},{variants}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the glossary as a JSON string.
        /// </summary>
        /// <returns>Formatted JSON.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(_terms, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Imports terms from a JSON string (as exported by <see cref="ToJson"/>).
        /// </summary>
        /// <param name="json">JSON array of glossary terms.</param>
        /// <returns>Number of terms imported.</returns>
        public int ImportJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json);
            var terms = JsonSerializer.Deserialize<List<GlossaryTerm>>(json);
            if (terms == null) return 0;

            int count = 0;
            foreach (var t in terms)
            {
                if (!string.IsNullOrWhiteSpace(t.Canonical) && Lookup(t.Canonical) == null)
                {
                    AddTerm(t.Canonical, t.Definition, t.Category, t.Variants, t.Examples);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Creates a pre-populated glossary with common AI/LLM prompt engineering terms.
        /// </summary>
        /// <returns>A new <see cref="PromptGlossary"/> with standard terms.</returns>
        public static PromptGlossary CreateDefault()
        {
            var g = new PromptGlossary();

            g.AddTerm("LLM", "Large Language Model — a neural network trained on text data", "AI",
                variants: new[] { "large language model", "language model" });
            g.AddTerm("prompt", "Input text or instructions sent to an LLM", "Core",
                variants: new[] { "query text", "input instruction" });
            g.AddTerm("completion", "Text generated by an LLM in response to a prompt", "Core",
                variants: new[] { "response text", "generated output", "model output" });
            g.AddTerm("token", "A sub-word unit used by LLMs for text processing", "Core",
                variants: new[] { "sub-word", "subword" });
            g.AddTerm("system prompt", "Initial instructions that set the LLM's behavior", "Core",
                variants: new[] { "system message", "system instruction", "meta-prompt" });
            g.AddTerm("few-shot", "Providing examples in the prompt to guide the model", "Technique",
                variants: new[] { "few shot", "n-shot", "example-based" });
            g.AddTerm("zero-shot", "Prompting without providing examples", "Technique",
                variants: new[] { "zero shot", "no-example" });
            g.AddTerm("chain-of-thought", "Prompting the model to show reasoning steps", "Technique",
                variants: new[] { "chain of thought", "CoT", "step-by-step reasoning" });
            g.AddTerm("temperature", "Sampling parameter controlling output randomness", "Parameter",
                variants: new[] { "temp", "sampling temperature" });
            g.AddTerm("top-p", "Nucleus sampling threshold for token selection", "Parameter",
                variants: new[] { "nucleus sampling", "top p" });
            g.AddTerm("hallucination", "LLM generating plausible but incorrect information", "Safety",
                variants: new[] { "confabulation", "fabrication" });
            g.AddTerm("guardrail", "Safety mechanism to constrain LLM outputs", "Safety",
                variants: new[] { "guard rail", "safety filter", "content filter" });
            g.AddTerm("RAG", "Retrieval-Augmented Generation — combining search with LLM generation", "Architecture",
                variants: new[] { "retrieval-augmented generation", "retrieval augmented generation" });
            g.AddTerm("embedding", "Dense vector representation of text for similarity search", "Architecture",
                variants: new[] { "text embedding", "vector representation" });
            g.AddTerm("fine-tuning", "Training a pre-trained model on domain-specific data", "Training",
                variants: new[] { "fine tuning", "finetuning", "model tuning" });

            return g;
        }

        // --- Private helpers ---

        private static List<int> FindWholeWord(string text, string word)
        {
            var positions = new List<int>();
            if (string.IsNullOrEmpty(word)) return positions;

            var pattern = $@"\b{Regex.Escape(word)}\b";
            foreach (Match m in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
            {
                positions.Add(m.Index);
            }

            return positions;
        }

        private static string ReplaceWholeWord(string text, string oldWord, string newWord)
        {
            var pattern = $@"\b{Regex.Escape(oldWord)}\b";
            return Regex.Replace(text, pattern, newWord, RegexOptions.IgnoreCase);
        }

        private static string CsvEscape(string value)
        {
            // CWE-1236: prevent CSV formula injection by prefixing
            // formula-trigger characters with a single quote so spreadsheet
            // apps treat the cell as literal text.
            if (value.Length > 0 && (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'))
            {
                value = "'" + value;
            }

            if (value.Contains(',') || value.Contains('"') ||
                value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
