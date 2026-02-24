namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// An immutable snapshot of a prompt template at a specific version.
    /// </summary>
    public class PromptVersion
    {
        /// <summary>
        /// Creates a new prompt version snapshot.
        /// </summary>
        /// <param name="versionNumber">The version number (1-based).</param>
        /// <param name="templateText">The raw template string.</param>
        /// <param name="description">Optional change description.</param>
        /// <param name="createdAt">When this version was created.</param>
        /// <param name="author">Who made the change.</param>
        /// <param name="defaultValues">Optional default variable values.</param>
        public PromptVersion(
            int versionNumber,
            string templateText,
            string? description = null,
            DateTimeOffset? createdAt = null,
            string? author = null,
            IReadOnlyDictionary<string, string>? defaultValues = null)
        {
            VersionNumber = versionNumber;
            TemplateText = templateText;
            Description = description;
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
            Author = author;
            DefaultValues = defaultValues != null
                ? new Dictionary<string, string>(defaultValues).AsReadOnly()
                : null;
        }

        /// <summary>Internal constructor for deserialization.</summary>
        internal PromptVersion() { }

        /// <summary>Gets the version number.</summary>
        public int VersionNumber { get; internal set; }

        /// <summary>Gets the raw template string.</summary>
        public string TemplateText { get; internal set; } = "";

        /// <summary>Gets the optional change description.</summary>
        public string? Description { get; internal set; }

        /// <summary>Gets when this version was created.</summary>
        public DateTimeOffset CreatedAt { get; internal set; }

        /// <summary>Gets who made the change.</summary>
        public string? Author { get; internal set; }

        /// <summary>Gets the default variable values at this version.</summary>
        public IReadOnlyDictionary<string, string>? DefaultValues { get; internal set; }
    }

    /// <summary>
    /// Tracks version history for prompt templates. Supports creating versions,
    /// comparing diffs between versions, and rolling back to previous versions.
    /// </summary>
    public class PromptVersionManager
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, List<PromptVersion>> _history;

        /// <summary>Maximum versions stored per template before pruning.</summary>
        public const int MaxVersionsPerTemplate = 100;

        /// <summary>Maximum number of tracked templates.</summary>
        public const int MaxTemplates = 500;

        /// <summary>
        /// Creates a new, empty version manager.
        /// </summary>
        public PromptVersionManager()
        {
            _history = new Dictionary<string, List<PromptVersion>>(
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Internal constructor for deserialization.
        /// </summary>
        internal PromptVersionManager(
            Dictionary<string, List<PromptVersion>> history)
        {
            _history = history;
        }

        /// <summary>
        /// Create a new version for a template.
        /// </summary>
        /// <param name="templateName">Unique template identifier.</param>
        /// <param name="templateText">The template content.</param>
        /// <param name="description">Optional change description.</param>
        /// <param name="author">Optional author name.</param>
        /// <param name="defaults">Optional default variable values.</param>
        /// <returns>The created PromptVersion.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="templateName"/> or <paramref name="templateText"/>
        /// is null, empty, or contains invalid characters.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the maximum number of templates has been reached.
        /// </exception>
        public PromptVersion CreateVersion(
            string templateName,
            string templateText,
            string? description = null,
            string? author = null,
            Dictionary<string, string>? defaults = null)
        {
            ValidateName(templateName);
            if (string.IsNullOrEmpty(templateText))
                throw new ArgumentException(
                    "Template text cannot be null or empty.",
                    nameof(templateText));

            var trimmedName = templateName.Trim();

            lock (_lock)
            {
                if (!_history.ContainsKey(trimmedName)
                    && _history.Count >= MaxTemplates)
                {
                    throw new InvalidOperationException(
                        $"Cannot track more than {MaxTemplates} templates. " +
                        "Delete unused template history first.");
                }

                if (!_history.TryGetValue(trimmedName, out var versions))
                {
                    versions = new List<PromptVersion>();
                    _history[trimmedName] = versions;
                }

                int nextVersion = versions.Count > 0
                    ? versions[versions.Count - 1].VersionNumber + 1
                    : 1;

                IReadOnlyDictionary<string, string>? readOnlyDefaults = defaults != null
                    ? new Dictionary<string, string>(defaults).AsReadOnly()
                    : null;

                var version = new PromptVersion(
                    nextVersion,
                    templateText,
                    description,
                    DateTimeOffset.UtcNow,
                    author,
                    readOnlyDefaults);

                versions.Add(version);

                // Prune oldest if over limit
                while (versions.Count > MaxVersionsPerTemplate)
                    versions.RemoveAt(0);

                return version;
            }
        }

        /// <summary>
        /// Get the latest version of a template.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <returns>The latest version, or null if not found.</returns>
        public PromptVersion? GetLatest(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return null;

            lock (_lock)
            {
                if (_history.TryGetValue(templateName.Trim(), out var versions)
                    && versions.Count > 0)
                    return versions[versions.Count - 1];
                return null;
            }
        }

        /// <summary>
        /// Get a specific version of a template.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <param name="versionNumber">The version number to retrieve.</param>
        /// <returns>The version, or null if not found.</returns>
        public PromptVersion? GetVersion(string templateName, int versionNumber)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return null;

            lock (_lock)
            {
                if (_history.TryGetValue(templateName.Trim(), out var versions))
                    return versions.FirstOrDefault(
                        v => v.VersionNumber == versionNumber);
                return null;
            }
        }

        /// <summary>
        /// Get all versions for a template, ordered by version number.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <returns>Ordered list of versions (empty if template not found).</returns>
        public IReadOnlyList<PromptVersion> GetHistory(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return Array.Empty<PromptVersion>();

            lock (_lock)
            {
                if (_history.TryGetValue(templateName.Trim(), out var versions))
                    return versions
                        .OrderBy(v => v.VersionNumber)
                        .ToList()
                        .AsReadOnly();
                return Array.Empty<PromptVersion>();
            }
        }

        /// <summary>
        /// Get the number of versions stored for a template.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <returns>Number of versions (0 if template not found).</returns>
        public int GetVersionCount(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return 0;

            lock (_lock)
            {
                if (_history.TryGetValue(templateName.Trim(), out var versions))
                    return versions.Count;
                return 0;
            }
        }

        /// <summary>
        /// Get all tracked template names.
        /// </summary>
        /// <returns>List of template names.</returns>
        public IReadOnlyList<string> GetTrackedTemplates()
        {
            lock (_lock)
            {
                return _history.Keys
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>
        /// Compare two versions and return a diff summary.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <param name="fromVersion">Source version number.</param>
        /// <param name="toVersion">Target version number.</param>
        /// <returns>A <see cref="VersionDiff"/> describing the changes.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the template name is invalid or versions are not found.
        /// </exception>
        public VersionDiff Compare(
            string templateName, int fromVersion, int toVersion)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException(
                    "Template name cannot be null or empty.",
                    nameof(templateName));

            var trimmed = templateName.Trim();

            lock (_lock)
            {
                if (!_history.TryGetValue(trimmed, out var versions))
                    throw new ArgumentException(
                        $"No history found for template '{trimmed}'.",
                        nameof(templateName));

                var from = versions.FirstOrDefault(
                    v => v.VersionNumber == fromVersion);
                if (from == null)
                    throw new ArgumentException(
                        $"Version {fromVersion} not found for template '{trimmed}'.",
                        nameof(fromVersion));

                var to = versions.FirstOrDefault(
                    v => v.VersionNumber == toVersion);
                if (to == null)
                    throw new ArgumentException(
                        $"Version {toVersion} not found for template '{trimmed}'.",
                        nameof(toVersion));

                return VersionDiff.Create(trimmed, from, to);
            }
        }

        /// <summary>
        /// Compare a template with the latest version to check for changes.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <param name="currentText">The current template text.</param>
        /// <returns>
        /// <c>true</c> if the text differs from the latest version,
        /// or if no history exists (template is "new").
        /// </returns>
        public bool HasChanges(string templateName, string currentText)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return true;

            lock (_lock)
            {
                if (!_history.TryGetValue(templateName.Trim(), out var versions)
                    || versions.Count == 0)
                    return true;

                var latest = versions[versions.Count - 1];
                return !string.Equals(
                    latest.TemplateText, currentText, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Restore a template to a specific version. Creates a new version
        /// with the content from the specified version (doesn't delete history).
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <param name="targetVersion">The version number to roll back to.</param>
        /// <param name="author">Optional author name.</param>
        /// <returns>The newly created version with rolled-back content.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the template or version is not found.
        /// </exception>
        public PromptVersion Rollback(
            string templateName, int targetVersion, string? author = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException(
                    "Template name cannot be null or empty.",
                    nameof(templateName));

            var trimmed = templateName.Trim();

            lock (_lock)
            {
                if (!_history.TryGetValue(trimmed, out var versions)
                    || versions.Count == 0)
                    throw new ArgumentException(
                        $"No history found for template '{trimmed}'.",
                        nameof(templateName));

                var target = versions.FirstOrDefault(
                    v => v.VersionNumber == targetVersion);
                if (target == null)
                    throw new ArgumentException(
                        $"Version {targetVersion} not found for template '{trimmed}'.",
                        nameof(targetVersion));

                int nextVersion = versions[versions.Count - 1].VersionNumber + 1;

                Dictionary<string, string>? defaultsCopy = null;
                if (target.DefaultValues != null)
                    defaultsCopy = new Dictionary<string, string>(target.DefaultValues);

                var rollback = new PromptVersion(
                    nextVersion,
                    target.TemplateText,
                    $"Rollback to v{targetVersion}",
                    DateTimeOffset.UtcNow,
                    author,
                    defaultsCopy?.AsReadOnly());

                versions.Add(rollback);

                while (versions.Count > MaxVersionsPerTemplate)
                    versions.RemoveAt(0);

                return rollback;
            }
        }

        /// <summary>
        /// Delete all version history for a template.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <returns><c>true</c> if the template was found and removed.</returns>
        public bool DeleteHistory(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return false;

            lock (_lock)
            {
                return _history.Remove(templateName.Trim());
            }
        }

        /// <summary>
        /// Clear all version history.
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }

        /// <summary>
        /// Total number of tracked templates.
        /// </summary>
        public int TemplateCount
        {
            get
            {
                lock (_lock)
                {
                    return _history.Count;
                }
            }
        }

        /// <summary>
        /// Total versions across all templates.
        /// </summary>
        public int TotalVersionCount
        {
            get
            {
                lock (_lock)
                {
                    return _history.Values.Sum(v => v.Count);
                }
            }
        }

        /// <summary>
        /// Serializes the version manager to a JSON string.
        /// </summary>
        /// <returns>A JSON string representing all version history.</returns>
        public string ToJson()
        {
            List<TemplateHistoryData> data;
            lock (_lock)
            {
                data = _history
                    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => new TemplateHistoryData
                    {
                        TemplateName = kv.Key,
                        Versions = kv.Value.Select(v => new VersionData
                        {
                            VersionNumber = v.VersionNumber,
                            TemplateText = v.TemplateText,
                            Description = v.Description,
                            CreatedAt = v.CreatedAt,
                            Author = v.Author,
                            DefaultValues = v.DefaultValues != null
                                ? new Dictionary<string, string>(v.DefaultValues)
                                : null
                        }).ToList()
                    })
                    .ToList();
            }

            var wrapper = new VersionManagerData
            {
                Version = 1,
                Templates = data
            };

            return JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Deserializes a version manager from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string.</param>
        /// <returns>A new <see cref="PromptVersionManager"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="json"/> is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the JSON is invalid or exceeds security limits.
        /// </exception>
        public static PromptVersionManager FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON string cannot be null or empty.", nameof(json));

            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var wrapper = JsonSerializer.Deserialize<VersionManagerData>(json,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

            if (wrapper?.Templates == null)
                throw new InvalidOperationException(
                    "Invalid version manager JSON: missing templates array.");

            if (wrapper.Templates.Count > MaxTemplates)
                throw new InvalidOperationException(
                    $"JSON contains {wrapper.Templates.Count} templates, " +
                    $"exceeding the maximum of {MaxTemplates}.");

            var history = new Dictionary<string, List<PromptVersion>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var tmpl in wrapper.Templates)
            {
                if (string.IsNullOrWhiteSpace(tmpl.TemplateName))
                    continue;

                var versions = new List<PromptVersion>();
                if (tmpl.Versions != null)
                {
                    foreach (var vd in tmpl.Versions)
                    {
                        IReadOnlyDictionary<string, string>? defaults = null;
                        if (vd.DefaultValues != null)
                            defaults = new Dictionary<string, string>(
                                vd.DefaultValues).AsReadOnly();

                        versions.Add(new PromptVersion(
                            vd.VersionNumber,
                            vd.TemplateText ?? "",
                            vd.Description,
                            vd.CreatedAt,
                            vd.Author,
                            defaults));
                    }
                }

                history[tmpl.TemplateName] = versions;
            }

            return new PromptVersionManager(history);
        }

        // --- Validation ---

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Template name cannot be null or empty.", nameof(name));

            if (!Regex.IsMatch(name.Trim(), @"^[\w\-\.]+$"))
                throw new ArgumentException(
                    $"Template name '{name}' contains invalid characters. " +
                    "Use only letters, digits, hyphens, underscores, and dots.",
                    nameof(name));
        }

        // --- Serialization DTOs ---

        internal class VersionManagerData
        {
            public int Version { get; set; }
            public List<TemplateHistoryData> Templates { get; set; } = new();
        }

        internal class TemplateHistoryData
        {
            public string TemplateName { get; set; } = "";
            public List<VersionData> Versions { get; set; } = new();
        }

        internal class VersionData
        {
            public int VersionNumber { get; set; }
            public string TemplateText { get; set; } = "";
            public string? Description { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public string? Author { get; set; }
            public Dictionary<string, string>? DefaultValues { get; set; }
        }
    }

    /// <summary>
    /// Result of comparing two prompt versions.
    /// </summary>
    public class VersionDiff
    {
        /// <summary>Gets the template name.</summary>
        public string TemplateName { get; }

        /// <summary>Gets the source version number.</summary>
        public int FromVersion { get; }

        /// <summary>Gets the target version number.</summary>
        public int ToVersion { get; }

        /// <summary>Gets whether the template text changed.</summary>
        public bool HasTextChanges { get; }

        /// <summary>Gets whether default values changed.</summary>
        public bool HasDefaultChanges { get; }

        /// <summary>Gets lines present in 'to' but not in 'from'.</summary>
        public IReadOnlyList<string> AddedLines { get; }

        /// <summary>Gets lines present in 'from' but not in 'to'.</summary>
        public IReadOnlyList<string> RemovedLines { get; }

        /// <summary>Gets default keys that were added.</summary>
        public IReadOnlyList<string> AddedDefaults { get; }

        /// <summary>Gets default keys that were removed.</summary>
        public IReadOnlyList<string> RemovedDefaults { get; }

        /// <summary>Gets default keys whose values changed.</summary>
        public IReadOnlyList<string> ChangedDefaults { get; }

        /// <summary>Gets the number of added lines.</summary>
        public int AddedLineCount => AddedLines.Count;

        /// <summary>Gets the number of removed lines.</summary>
        public int RemovedLineCount => RemovedLines.Count;

        internal VersionDiff(
            string templateName,
            int fromVersion,
            int toVersion,
            bool hasTextChanges,
            bool hasDefaultChanges,
            IReadOnlyList<string> addedLines,
            IReadOnlyList<string> removedLines,
            IReadOnlyList<string> addedDefaults,
            IReadOnlyList<string> removedDefaults,
            IReadOnlyList<string> changedDefaults)
        {
            TemplateName = templateName;
            FromVersion = fromVersion;
            ToVersion = toVersion;
            HasTextChanges = hasTextChanges;
            HasDefaultChanges = hasDefaultChanges;
            AddedLines = addedLines;
            RemovedLines = removedLines;
            AddedDefaults = addedDefaults;
            RemovedDefaults = removedDefaults;
            ChangedDefaults = changedDefaults;
        }

        /// <summary>
        /// Creates a diff by comparing two prompt versions.
        /// </summary>
        internal static VersionDiff Create(
            string templateName, PromptVersion from, PromptVersion to)
        {
            // Line diff
            var fromLines = from.TemplateText.Split('\n');
            var toLines = to.TemplateText.Split('\n');

            var fromSet = new HashSet<string>(fromLines);
            var toSet = new HashSet<string>(toLines);

            var addedLines = toLines.Where(l => !fromSet.Contains(l)).ToList();
            var removedLines = fromLines.Where(l => !toSet.Contains(l)).ToList();

            bool hasTextChanges = !string.Equals(
                from.TemplateText, to.TemplateText, StringComparison.Ordinal);

            // Default diff
            var fromDefaults = from.DefaultValues
                ?? (IReadOnlyDictionary<string, string>)
                    new Dictionary<string, string>();
            var toDefaults = to.DefaultValues
                ?? (IReadOnlyDictionary<string, string>)
                    new Dictionary<string, string>();

            var addedDefaults = toDefaults.Keys
                .Where(k => !fromDefaults.ContainsKey(k)).ToList();
            var removedDefaults = fromDefaults.Keys
                .Where(k => !toDefaults.ContainsKey(k)).ToList();
            var changedDefaults = fromDefaults.Keys
                .Where(k => toDefaults.ContainsKey(k)
                    && !string.Equals(fromDefaults[k], toDefaults[k],
                        StringComparison.Ordinal))
                .ToList();

            bool hasDefaultChanges = addedDefaults.Count > 0
                || removedDefaults.Count > 0
                || changedDefaults.Count > 0;

            return new VersionDiff(
                templateName,
                from.VersionNumber,
                to.VersionNumber,
                hasTextChanges,
                hasDefaultChanges,
                addedLines.AsReadOnly(),
                removedLines.AsReadOnly(),
                addedDefaults.AsReadOnly(),
                removedDefaults.AsReadOnly(),
                changedDefaults.AsReadOnly());
        }

        /// <summary>
        /// Human-readable summary of changes.
        /// </summary>
        /// <returns>A string describing the changes between versions.</returns>
        public string GetSummary()
        {
            var parts = new List<string>();

            if (AddedLineCount > 0)
                parts.Add($"+{AddedLineCount} line{(AddedLineCount == 1 ? "" : "s")}");
            if (RemovedLineCount > 0)
                parts.Add($"-{RemovedLineCount} line{(RemovedLineCount == 1 ? "" : "s")}");

            int defaultChanges = AddedDefaults.Count
                + RemovedDefaults.Count + ChangedDefaults.Count;
            if (defaultChanges > 0)
                parts.Add($"{defaultChanges} default{(defaultChanges == 1 ? "" : "s")} changed");

            if (parts.Count == 0)
                return "No changes";

            return string.Join(", ", parts);
        }
    }
}
