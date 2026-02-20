namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Metadata wrapper around a <see cref="PromptTemplate"/> that adds
    /// a name, description, category, tags, and timestamps for organizing
    /// templates in a <see cref="PromptLibrary"/>.
    /// </summary>
    public class PromptEntry
    {
        /// <summary>
        /// Creates a new prompt entry.
        /// </summary>
        /// <param name="name">
        /// Unique name for the entry (e.g., "code-review", "summarize-article").
        /// Must be non-empty and contain only letters, digits, hyphens, and underscores.
        /// </param>
        /// <param name="template">The prompt template.</param>
        /// <param name="description">Optional human-readable description.</param>
        /// <param name="category">Optional category (e.g., "coding", "writing", "analysis").</param>
        /// <param name="tags">Optional tags for search and filtering.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> is null, empty, or contains invalid characters.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="template"/> is null.
        /// </exception>
        public PromptEntry(
            string name,
            PromptTemplate template,
            string? description = null,
            string? category = null,
            IEnumerable<string>? tags = null)
        {
            ValidateName(name);
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            Name = name.Trim();
            Template = template;
            Description = description;
            Category = category?.Trim();
            Tags = tags != null
                ? new HashSet<string>(tags.Select(t => t.Trim()),
                    StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CreatedAt = DateTimeOffset.UtcNow;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>Internal constructor for deserialization.</summary>
        internal PromptEntry() { }

        /// <summary>Gets the unique name of this entry.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the prompt template.</summary>
        public PromptTemplate Template { get; internal set; } = null!;

        /// <summary>Gets or sets the human-readable description.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets the category.</summary>
        public string? Category { get; set; }

        /// <summary>Gets the tags for search and filtering.</summary>
        public HashSet<string> Tags { get; internal set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets when the entry was created.</summary>
        public DateTimeOffset CreatedAt { get; internal set; }

        /// <summary>Gets when the entry was last updated.</summary>
        public DateTimeOffset UpdatedAt { get; internal set; }

        /// <summary>
        /// Adds a tag to this entry.
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="tag"/> is null or empty.
        /// </exception>
        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException(
                    "Tag cannot be null or empty.", nameof(tag));
            Tags.Add(tag.Trim());
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Removes a tag from this entry.
        /// </summary>
        /// <param name="tag">The tag to remove.</param>
        /// <returns><c>true</c> if the tag was removed.</returns>
        public bool RemoveTag(string tag) => Tags.Remove(tag);

        /// <summary>
        /// Checks whether this entry has a given tag (case-insensitive).
        /// </summary>
        public bool HasTag(string tag) => Tags.Contains(tag);

        /// <summary>
        /// Validates that a name contains only letters, digits, hyphens,
        /// underscores, and dots.
        /// </summary>
        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Entry name cannot be null or empty.", nameof(name));

            if (!Regex.IsMatch(name.Trim(), @"^[\w\-\.]+$"))
                throw new ArgumentException(
                    $"Entry name '{name}' contains invalid characters. " +
                    "Use only letters, digits, hyphens, underscores, and dots.",
                    nameof(name));
        }
    }

    /// <summary>
    /// A central registry for managing, categorizing, searching, and
    /// persisting reusable <see cref="PromptTemplate"/> instances. Acts
    /// as a prompt template library that can be saved to and loaded from
    /// JSON files for sharing across projects and teams.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var library = new PromptLibrary();
    ///
    /// // Add templates with metadata
    /// library.Add("code-review",
    ///     new PromptTemplate("Review this {{language}} code:\n{{code}}"),
    ///     description: "Reviews code and suggests improvements",
    ///     category: "coding",
    ///     tags: new[] { "review", "quality" });
    ///
    /// library.Add("summarize",
    ///     new PromptTemplate("Summarize in {{style}} style:\n{{text}}",
    ///         new Dictionary&lt;string, string&gt; { ["style"] = "concise" }),
    ///     description: "Summarizes text with configurable style",
    ///     category: "writing");
    ///
    /// // Search and retrieve
    /// var codingTemplates = library.FindByCategory("coding");
    /// var entry = library.Get("code-review");
    /// string prompt = entry.Template.Render(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["language"] = "C#",
    ///     ["code"] = "public void Foo() { }"
    /// });
    ///
    /// // Save and load
    /// await library.SaveToFileAsync("my-prompts.json");
    /// var loaded = await PromptLibrary.LoadFromFileAsync("my-prompts.json");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptLibrary
    {
        /// <summary>
        /// Maximum number of entries allowed when deserializing from JSON.
        /// </summary>
        internal const int MaxDeserializedEntries = 10_000;

        private readonly Dictionary<string, PromptEntry> _entries =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        /// <summary>
        /// Creates a new empty prompt library.
        /// </summary>
        public PromptLibrary() { }

        /// <summary>
        /// Gets the number of entries in the library.
        /// </summary>
        public int Count
        {
            get { lock (_lock) return _entries.Count; }
        }

        /// <summary>
        /// Gets all entry names in the library.
        /// </summary>
        public IReadOnlyList<string> Names
        {
            get
            {
                lock (_lock)
                    return _entries.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        /// <summary>
        /// Gets all entries in the library, ordered by name.
        /// </summary>
        public IReadOnlyList<PromptEntry> Entries
        {
            get
            {
                lock (_lock)
                    return _entries.Values
                        .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
            }
        }

        /// <summary>
        /// Adds a prompt template to the library with metadata.
        /// </summary>
        /// <param name="name">
        /// Unique name for the entry (e.g., "code-review", "summarize").
        /// </param>
        /// <param name="template">The prompt template to store.</param>
        /// <param name="description">Optional description.</param>
        /// <param name="category">Optional category.</param>
        /// <param name="tags">Optional tags for filtering.</param>
        /// <returns>The created <see cref="PromptEntry"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when an entry with the same name already exists.
        /// </exception>
        public PromptEntry Add(
            string name,
            PromptTemplate template,
            string? description = null,
            string? category = null,
            IEnumerable<string>? tags = null)
        {
            var entry = new PromptEntry(name, template, description, category, tags);
            lock (_lock)
            {
                if (_entries.ContainsKey(entry.Name))
                    throw new ArgumentException(
                        $"An entry named '{entry.Name}' already exists. " +
                        "Use Update() to modify it or Remove() first.",
                        nameof(name));
                _entries[entry.Name] = entry;
            }
            return entry;
        }

        /// <summary>
        /// Adds or replaces a prompt template in the library. If an entry
        /// with the same name exists, it is replaced; otherwise a new entry
        /// is created.
        /// </summary>
        /// <param name="name">Name for the entry.</param>
        /// <param name="template">The prompt template.</param>
        /// <param name="description">Optional description.</param>
        /// <param name="category">Optional category.</param>
        /// <param name="tags">Optional tags.</param>
        /// <returns>The created or updated <see cref="PromptEntry"/>.</returns>
        public PromptEntry Set(
            string name,
            PromptTemplate template,
            string? description = null,
            string? category = null,
            IEnumerable<string>? tags = null)
        {
            var entry = new PromptEntry(name, template, description, category, tags);
            lock (_lock)
            {
                _entries[entry.Name] = entry;
            }
            return entry;
        }

        /// <summary>
        /// Updates the metadata (description, category, tags) and/or template
        /// of an existing entry. Only non-null parameters are applied.
        /// </summary>
        /// <param name="name">Name of the entry to update.</param>
        /// <param name="template">New template (null to keep existing).</param>
        /// <param name="description">New description (null to keep existing).</param>
        /// <param name="category">New category (null to keep existing).</param>
        /// <param name="tags">
        /// New tags (null to keep existing). Replaces all tags if provided.
        /// </param>
        /// <returns>The updated <see cref="PromptEntry"/>.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when no entry with the given name exists.
        /// </exception>
        public PromptEntry Update(
            string name,
            PromptTemplate? template = null,
            string? description = null,
            string? category = null,
            IEnumerable<string>? tags = null)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(name, out var entry))
                    throw new KeyNotFoundException(
                        $"No entry named '{name}' found in the library.");

                if (template != null)
                    entry.Template = template;
                if (description != null)
                    entry.Description = description;
                if (category != null)
                    entry.Category = category;
                if (tags != null)
                    entry.Tags = new HashSet<string>(
                        tags.Select(t => t.Trim()),
                        StringComparer.OrdinalIgnoreCase);

                entry.UpdatedAt = DateTimeOffset.UtcNow;
                return entry;
            }
        }

        /// <summary>
        /// Gets an entry by name.
        /// </summary>
        /// <param name="name">The entry name.</param>
        /// <returns>The <see cref="PromptEntry"/> if found.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when no entry with the given name exists.
        /// </exception>
        public PromptEntry Get(string name)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(name, out var entry))
                    throw new KeyNotFoundException(
                        $"No entry named '{name}' found in the library.");
                return entry;
            }
        }

        /// <summary>
        /// Tries to get an entry by name.
        /// </summary>
        /// <param name="name">The entry name.</param>
        /// <param name="entry">The entry if found, null otherwise.</param>
        /// <returns><c>true</c> if the entry was found.</returns>
        public bool TryGet(string name, out PromptEntry? entry)
        {
            lock (_lock)
                return _entries.TryGetValue(name, out entry);
        }

        /// <summary>
        /// Checks whether an entry with the given name exists.
        /// </summary>
        public bool Contains(string name)
        {
            lock (_lock)
                return _entries.ContainsKey(name);
        }

        /// <summary>
        /// Removes an entry by name.
        /// </summary>
        /// <param name="name">The entry name.</param>
        /// <returns><c>true</c> if the entry was removed.</returns>
        public bool Remove(string name)
        {
            lock (_lock)
                return _entries.Remove(name);
        }

        /// <summary>
        /// Removes all entries from the library.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
                _entries.Clear();
        }

        // ──────────────── Search & Filter ────────────────

        /// <summary>
        /// Finds all entries in a given category (case-insensitive).
        /// </summary>
        /// <param name="category">Category to filter by.</param>
        /// <returns>Matching entries, ordered by name.</returns>
        public IReadOnlyList<PromptEntry> FindByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return Array.Empty<PromptEntry>();

            lock (_lock)
                return _entries.Values
                    .Where(e => string.Equals(
                        e.Category, category.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        /// <summary>
        /// Finds all entries that have a given tag (case-insensitive).
        /// </summary>
        /// <param name="tag">Tag to filter by.</param>
        /// <returns>Matching entries, ordered by name.</returns>
        public IReadOnlyList<PromptEntry> FindByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return Array.Empty<PromptEntry>();

            lock (_lock)
                return _entries.Values
                    .Where(e => e.HasTag(tag.Trim()))
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        /// <summary>
        /// Searches entries by a text query. Matches against name,
        /// description, category, tags, and template content
        /// (case-insensitive substring search).
        /// </summary>
        /// <param name="query">Search query text.</param>
        /// <returns>Matching entries, ordered by name.</returns>
        public IReadOnlyList<PromptEntry> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Entries;

            var q = query.Trim();

            lock (_lock)
                return _entries.Values
                    .Where(e => MatchesQuery(e, q))
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        /// <summary>
        /// Gets all distinct categories used in the library, sorted alphabetically.
        /// </summary>
        public IReadOnlyList<string> GetCategories()
        {
            lock (_lock)
                return _entries.Values
                    .Where(e => !string.IsNullOrWhiteSpace(e.Category))
                    .Select(e => e.Category!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        /// <summary>
        /// Gets all distinct tags used across all entries, sorted alphabetically.
        /// </summary>
        public IReadOnlyList<string> GetAllTags()
        {
            lock (_lock)
                return _entries.Values
                    .SelectMany(e => e.Tags)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        /// <summary>
        /// Merges entries from another library into this one. Entries
        /// with conflicting names are handled according to the
        /// <paramref name="overwrite"/> flag.
        /// </summary>
        /// <param name="other">The library to merge from.</param>
        /// <param name="overwrite">
        /// When <c>true</c>, existing entries are overwritten by entries
        /// from <paramref name="other"/>. When <c>false</c> (default),
        /// conflicting entries are skipped.
        /// </param>
        /// <returns>The number of entries added or updated.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="other"/> is null.
        /// </exception>
        public int Merge(PromptLibrary other, bool overwrite = false)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            int count = 0;
            var otherEntries = other.Entries; // snapshot

            lock (_lock)
            {
                foreach (var entry in otherEntries)
                {
                    if (_entries.ContainsKey(entry.Name) && !overwrite)
                        continue;

                    _entries[entry.Name] = entry;
                    count++;
                }
            }

            return count;
        }

        // ──────────────── Serialization ────────────────

        /// <summary>
        /// Serializes the library to a JSON string.
        /// </summary>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <returns>A JSON string representing the library.</returns>
        public string ToJson(bool indented = true)
        {
            List<LibraryEntryData> entries;
            lock (_lock)
            {
                entries = _entries.Values
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(e => new LibraryEntryData
                    {
                        Name = e.Name,
                        Template = e.Template.Template,
                        Defaults = e.Template.Defaults.Count > 0
                            ? new Dictionary<string, string>(
                                (IDictionary<string, string>)e.Template.Defaults)
                            : null,
                        Description = e.Description,
                        Category = e.Category,
                        Tags = e.Tags.Count > 0
                            ? e.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList()
                            : null,
                        CreatedAt = e.CreatedAt,
                        UpdatedAt = e.UpdatedAt
                    })
                    .ToList();
            }

            var data = new LibraryData
            {
                Version = 1,
                Entries = entries
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Deserializes a library from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string.</param>
        /// <returns>A new <see cref="PromptLibrary"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="json"/> is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the JSON is invalid or exceeds security limits.
        /// </exception>
        public static PromptLibrary FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON string cannot be null or empty.", nameof(json));

            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var data = JsonSerializer.Deserialize<LibraryData>(json,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

            if (data?.Entries == null)
                throw new InvalidOperationException(
                    "Invalid library JSON: missing entries array.");

            if (data.Entries.Count > MaxDeserializedEntries)
                throw new InvalidOperationException(
                    $"Library JSON contains {data.Entries.Count} entries, " +
                    $"exceeding the maximum of {MaxDeserializedEntries}.");

            var library = new PromptLibrary();

            foreach (var ed in data.Entries)
            {
                if (string.IsNullOrWhiteSpace(ed.Name)
                    || string.IsNullOrWhiteSpace(ed.Template))
                    continue; // skip invalid entries gracefully

                var template = new PromptTemplate(ed.Template, ed.Defaults);
                var entry = new PromptEntry(
                    ed.Name, template, ed.Description, ed.Category, ed.Tags);

                // Restore timestamps if present
                if (ed.CreatedAt != default)
                    entry.CreatedAt = ed.CreatedAt;
                if (ed.UpdatedAt != default)
                    entry.UpdatedAt = ed.UpdatedAt;

                library._entries[entry.Name] = entry;
            }

            return library;
        }

        /// <summary>
        /// Saves the library to a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the output file.</param>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task SaveToFileAsync(
            string filePath,
            bool indented = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            string json = ToJson(indented);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        /// <summary>
        /// Loads a library from a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the JSON file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A new <see cref="PromptLibrary"/> instance.</returns>
        public static async Task<PromptLibrary> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            filePath = Path.GetFullPath(filePath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Library file not found: {filePath}", filePath);

            SerializationGuards.ThrowIfFileTooLarge(filePath);

            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return FromJson(json);
        }

        // ──────────────── Built-in Library ────────────────

        /// <summary>
        /// Creates a library pre-loaded with useful prompt templates for
        /// common tasks. A starting point for users who want templates
        /// ready to use out of the box.
        /// </summary>
        /// <returns>A new <see cref="PromptLibrary"/> with built-in entries.</returns>
        public static PromptLibrary CreateDefault()
        {
            var lib = new PromptLibrary();

            lib.Add("code-review",
                new PromptTemplate(
                    "Review this {{language}} code for bugs, performance issues, and improvements:\n\n```{{language}}\n{{code}}\n```",
                    new Dictionary<string, string> { ["language"] = "code" }),
                description: "Reviews code and suggests improvements",
                category: "coding",
                tags: new[] { "review", "quality", "best-practices" });

            lib.Add("explain-code",
                new PromptTemplate(
                    "Explain this {{language}} code step by step. Use simple language:\n\n```{{language}}\n{{code}}\n```",
                    new Dictionary<string, string> { ["language"] = "code" }),
                description: "Explains code in simple terms",
                category: "coding",
                tags: new[] { "explain", "learning" });

            lib.Add("summarize",
                new PromptTemplate(
                    "Summarize the following text in {{style}} style. Keep it under {{maxWords}} words:\n\n{{text}}",
                    new Dictionary<string, string>
                    {
                        ["style"] = "concise",
                        ["maxWords"] = "100"
                    }),
                description: "Summarizes text with configurable style and length",
                category: "writing",
                tags: new[] { "summarize", "condense" });

            lib.Add("translate",
                new PromptTemplate(
                    "Translate the following text from {{source}} to {{target}}. Preserve the original tone and meaning:\n\n{{text}}",
                    new Dictionary<string, string> { ["source"] = "English" }),
                description: "Translates text between languages",
                category: "writing",
                tags: new[] { "translate", "language", "i18n" });

            lib.Add("extract-json",
                new PromptTemplate(
                    "Extract the following fields from the text and return valid JSON:\n\nFields: {{fields}}\n\nText:\n{{text}}"),
                description: "Extracts structured data from text as JSON",
                category: "data",
                tags: new[] { "extract", "json", "structured" });

            lib.Add("rewrite",
                new PromptTemplate(
                    "Rewrite the following text in a {{tone}} tone for a {{audience}} audience:\n\n{{text}}",
                    new Dictionary<string, string>
                    {
                        ["tone"] = "professional",
                        ["audience"] = "general"
                    }),
                description: "Rewrites text with configurable tone and audience",
                category: "writing",
                tags: new[] { "rewrite", "tone", "style" });

            lib.Add("debug-error",
                new PromptTemplate(
                    "I got this error in my {{language}} code:\n\nError:\n```\n{{error}}\n```\n\nCode:\n```{{language}}\n{{code}}\n```\n\nExplain what caused the error and how to fix it."),
                description: "Diagnoses and fixes code errors",
                category: "coding",
                tags: new[] { "debug", "error", "fix" });

            lib.Add("generate-tests",
                new PromptTemplate(
                    "Generate unit tests for this {{language}} code using {{framework}}:\n\n```{{language}}\n{{code}}\n```\n\nCover edge cases and common scenarios.",
                    new Dictionary<string, string> { ["framework"] = "the standard testing framework" }),
                description: "Generates unit tests for code",
                category: "coding",
                tags: new[] { "testing", "unit-tests", "quality" });

            return lib;
        }

        // ──────────────── Private Helpers ────────────────

        /// <summary>
        /// Checks whether an entry matches a search query (case-insensitive
        /// substring match against name, description, category, tags, and template).
        /// </summary>
        private static bool MatchesQuery(PromptEntry entry, string query)
        {
            if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
            if (entry.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                return true;
            if (entry.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                return true;
            if (entry.Template.Template.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
            if (entry.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        // ──────────────── Serialization DTOs ────────────────

        internal class LibraryData
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = 1;

            [JsonPropertyName("entries")]
            public List<LibraryEntryData> Entries { get; set; } = new();
        }

        internal class LibraryEntryData
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("template")]
            public string Template { get; set; } = "";

            [JsonPropertyName("defaults")]
            public Dictionary<string, string>? Defaults { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("tags")]
            public List<string>? Tags { get; set; }

            [JsonPropertyName("createdAt")]
            public DateTimeOffset CreatedAt { get; set; }

            [JsonPropertyName("updatedAt")]
            public DateTimeOffset UpdatedAt { get; set; }
        }
    }
}
