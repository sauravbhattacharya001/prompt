namespace Prompt
{
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a point-in-time snapshot of a <see cref="PromptLibrary"/>.
    /// </summary>
    public class PromptSnapshot
    {
        /// <summary>Gets the unique identifier for this snapshot.</summary>
        public string Id { get; internal set; } = "";

        /// <summary>Gets the user-provided name for this snapshot.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets an optional description of what changed or why the snapshot was taken.</summary>
        public string? Description { get; internal set; }

        /// <summary>Gets when the snapshot was created.</summary>
        public DateTimeOffset CreatedAt { get; internal set; }

        /// <summary>Gets the number of entries in the snapshot.</summary>
        public int EntryCount { get; internal set; }

        /// <summary>Gets the SHA-256 hash of the library content at snapshot time.</summary>
        public string ContentHash { get; internal set; } = "";

        /// <summary>Gets the serialized library JSON.</summary>
        internal string LibraryJson { get; set; } = "";

        /// <summary>
        /// Restores the library from this snapshot.
        /// </summary>
        /// <returns>A new <see cref="PromptLibrary"/> matching the snapshot state.</returns>
        public PromptLibrary Restore() => PromptLibrary.FromJson(LibraryJson);
    }

    /// <summary>
    /// Describes a difference between two snapshots for a single entry.
    /// </summary>
    public class SnapshotEntryDiff
    {
        /// <summary>Gets the entry name.</summary>
        public string EntryName { get; init; } = "";

        /// <summary>Gets the type of change.</summary>
        public SnapshotDiffType DiffType { get; init; }

        /// <summary>Gets a human-readable summary of the change.</summary>
        public string Summary { get; init; } = "";
    }

    /// <summary>
    /// The type of change detected between snapshots.
    /// </summary>
    public enum SnapshotDiffType
    {
        /// <summary>Entry was added.</summary>
        Added,
        /// <summary>Entry was removed.</summary>
        Removed,
        /// <summary>Entry template text changed.</summary>
        TemplateChanged,
        /// <summary>Entry metadata (description, category, tags) changed.</summary>
        MetadataChanged,
        /// <summary>Entry defaults changed.</summary>
        DefaultsChanged
    }

    /// <summary>
    /// Summary of differences between two snapshots.
    /// </summary>
    public class SnapshotDiffReport
    {
        /// <summary>Gets the source snapshot name.</summary>
        public string FromSnapshot { get; init; } = "";

        /// <summary>Gets the target snapshot name.</summary>
        public string ToSnapshot { get; init; } = "";

        /// <summary>Gets the list of individual entry differences.</summary>
        public IReadOnlyList<SnapshotEntryDiff> Diffs { get; init; } = Array.Empty<SnapshotEntryDiff>();

        /// <summary>Gets the count of added entries.</summary>
        public int AddedCount => Diffs.Count(d => d.DiffType == SnapshotDiffType.Added);

        /// <summary>Gets the count of removed entries.</summary>
        public int RemovedCount => Diffs.Count(d => d.DiffType == SnapshotDiffType.Removed);

        /// <summary>Gets the count of modified entries (template, metadata, or defaults changed).</summary>
        public int ModifiedCount => Diffs.Count(d =>
            d.DiffType == SnapshotDiffType.TemplateChanged ||
            d.DiffType == SnapshotDiffType.MetadataChanged ||
            d.DiffType == SnapshotDiffType.DefaultsChanged);

        /// <summary>Gets whether the two snapshots are identical.</summary>
        public bool AreIdentical => Diffs.Count == 0;

        /// <summary>
        /// Generates a human-readable text summary of the diff.
        /// </summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Diff: {FromSnapshot} → {ToSnapshot}");
            sb.AppendLine($"  Added: {AddedCount}  Removed: {RemovedCount}  Modified: {ModifiedCount}");

            if (AreIdentical)
            {
                sb.AppendLine("  No differences found.");
                return sb.ToString();
            }

            sb.AppendLine();
            foreach (var diff in Diffs.OrderBy(d => d.EntryName, StringComparer.OrdinalIgnoreCase))
            {
                var symbol = diff.DiffType switch
                {
                    SnapshotDiffType.Added => "+",
                    SnapshotDiffType.Removed => "-",
                    _ => "~"
                };
                sb.AppendLine($"  [{symbol}] {diff.EntryName}: {diff.Summary}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Manages point-in-time snapshots of a <see cref="PromptLibrary"/>,
    /// enabling versioning, comparison, and rollback of prompt template collections.
    /// </summary>
    /// <remarks>
    /// <para>Example usage:</para>
    /// <code>
    /// var library = PromptLibrary.CreateDefault();
    /// var manager = new PromptSnapshotManager();
    ///
    /// // Take a snapshot before making changes
    /// var snap1 = manager.TakeSnapshot(library, "before-refactor",
    ///     "Baseline before rewriting prompts");
    ///
    /// // Make changes...
    /// library.Update("summarize", template: new PromptTemplate("New template: {{text}}"));
    /// library.Remove("translate");
    ///
    /// // Take another snapshot
    /// var snap2 = manager.TakeSnapshot(library, "after-refactor");
    ///
    /// // Compare snapshots
    /// var diff = manager.Compare("before-refactor", "after-refactor");
    /// Console.WriteLine(diff.ToText());
    ///
    /// // Rollback if needed
    /// var restored = manager.Rollback("before-refactor");
    ///
    /// // Save/load snapshot history
    /// await manager.SaveAsync("snapshots.json");
    /// var loaded = await PromptSnapshotManager.LoadAsync("snapshots.json");
    /// </code>
    /// </remarks>
    public class PromptSnapshotManager
    {
        /// <summary>Maximum number of snapshots allowed.</summary>
        internal const int MaxSnapshots = 1000;

        private readonly Dictionary<string, PromptSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _order = new(); // insertion order
        private readonly object _lock = new();

        /// <summary>Gets the number of stored snapshots.</summary>
        public int Count
        {
            get { lock (_lock) return _snapshots.Count; }
        }

        /// <summary>Gets all snapshot names in chronological order.</summary>
        public IReadOnlyList<string> Names
        {
            get { lock (_lock) return _order.ToList(); }
        }

        /// <summary>
        /// Takes a point-in-time snapshot of the given library.
        /// </summary>
        /// <param name="library">The library to snapshot.</param>
        /// <param name="name">A unique name for the snapshot (e.g., "v1.0", "pre-migration").</param>
        /// <param name="description">Optional description of the snapshot.</param>
        /// <returns>The created <see cref="PromptSnapshot"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="library"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is invalid or already exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the maximum snapshot count is reached.</exception>
        public PromptSnapshot TakeSnapshot(PromptLibrary library, string name, string? description = null)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Snapshot name cannot be null or empty.", nameof(name));

            var trimmedName = name.Trim();

            lock (_lock)
            {
                if (_snapshots.ContainsKey(trimmedName))
                    throw new ArgumentException($"A snapshot named '{trimmedName}' already exists.", nameof(name));
                if (_snapshots.Count >= MaxSnapshots)
                    throw new InvalidOperationException($"Maximum of {MaxSnapshots} snapshots reached. Remove old snapshots first.");

                var json = library.ToJson(false);
                var hash = ComputeHash(json);

                var snapshot = new PromptSnapshot
                {
                    Id = Guid.NewGuid().ToString("N")[..12],
                    Name = trimmedName,
                    Description = description,
                    CreatedAt = DateTimeOffset.UtcNow,
                    EntryCount = library.Count,
                    ContentHash = hash,
                    LibraryJson = json
                };

                _snapshots[trimmedName] = snapshot;
                _order.Add(trimmedName);

                return snapshot;
            }
        }

        /// <summary>
        /// Gets a snapshot by name.
        /// </summary>
        /// <param name="name">The snapshot name.</param>
        /// <returns>The <see cref="PromptSnapshot"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when no snapshot with the given name exists.</exception>
        public PromptSnapshot GetSnapshot(string name)
        {
            lock (_lock)
            {
                if (!_snapshots.TryGetValue(name, out var snapshot))
                    throw new KeyNotFoundException($"No snapshot named '{name}' found.");
                return snapshot;
            }
        }

        /// <summary>
        /// Checks whether a snapshot with the given name exists.
        /// </summary>
        public bool Contains(string name)
        {
            lock (_lock) return _snapshots.ContainsKey(name);
        }

        /// <summary>
        /// Gets the most recently created snapshot.
        /// </summary>
        /// <returns>The latest <see cref="PromptSnapshot"/>, or null if no snapshots exist.</returns>
        public PromptSnapshot? GetLatest()
        {
            lock (_lock)
            {
                if (_order.Count == 0) return null;
                return _snapshots[_order[^1]];
            }
        }

        /// <summary>
        /// Removes a snapshot by name.
        /// </summary>
        /// <param name="name">The snapshot name.</param>
        /// <returns><c>true</c> if the snapshot was removed.</returns>
        public bool Remove(string name)
        {
            lock (_lock)
            {
                if (!_snapshots.Remove(name)) return false;
                _order.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                return true;
            }
        }

        /// <summary>
        /// Removes all snapshots.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _snapshots.Clear();
                _order.Clear();
            }
        }

        /// <summary>
        /// Restores a library from a named snapshot.
        /// </summary>
        /// <param name="name">The snapshot name to rollback to.</param>
        /// <returns>A new <see cref="PromptLibrary"/> matching the snapshot state.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when no snapshot with the given name exists.</exception>
        public PromptLibrary Rollback(string name)
        {
            var snapshot = GetSnapshot(name);
            return snapshot.Restore();
        }

        /// <summary>
        /// Compares two snapshots and returns a detailed diff report.
        /// </summary>
        /// <param name="fromName">The baseline snapshot name.</param>
        /// <param name="toName">The target snapshot name.</param>
        /// <returns>A <see cref="SnapshotDiffReport"/> describing all differences.</returns>
        public SnapshotDiffReport Compare(string fromName, string toName)
        {
            var fromSnapshot = GetSnapshot(fromName);
            var toSnapshot = GetSnapshot(toName);

            var fromLib = fromSnapshot.Restore();
            var toLib = toSnapshot.Restore();

            return CompareLibraries(fromLib, toLib, fromName, toName);
        }

        /// <summary>
        /// Compares a snapshot against the current state of a library.
        /// </summary>
        /// <param name="snapshotName">The snapshot name to compare against.</param>
        /// <param name="currentLibrary">The current library state.</param>
        /// <returns>A <see cref="SnapshotDiffReport"/> describing all differences.</returns>
        public SnapshotDiffReport CompareWithCurrent(string snapshotName, PromptLibrary currentLibrary)
        {
            if (currentLibrary == null) throw new ArgumentNullException(nameof(currentLibrary));
            var snapshot = GetSnapshot(snapshotName);
            var snapshotLib = snapshot.Restore();
            return CompareLibraries(snapshotLib, currentLibrary, snapshotName, "(current)");
        }

        /// <summary>
        /// Lists all snapshots with their metadata.
        /// </summary>
        /// <returns>Snapshot metadata in chronological order.</returns>
        public IReadOnlyList<PromptSnapshot> ListSnapshots()
        {
            lock (_lock)
                return _order.Select(n => _snapshots[n]).ToList();
        }

        /// <summary>
        /// Checks whether the library has changed since a given snapshot.
        /// </summary>
        /// <param name="snapshotName">The snapshot name to compare against.</param>
        /// <param name="library">The current library state.</param>
        /// <returns><c>true</c> if the library differs from the snapshot.</returns>
        public bool HasChanged(string snapshotName, PromptLibrary library)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            var snapshot = GetSnapshot(snapshotName);
            var currentHash = ComputeHash(library.ToJson(false));
            return !string.Equals(snapshot.ContentHash, currentHash, StringComparison.Ordinal);
        }

        // ──────────────── Serialization ────────────────

        /// <summary>
        /// Serializes all snapshots to a JSON string.
        /// </summary>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <returns>A JSON string representing all snapshots.</returns>
        public string ToJson(bool indented = true)
        {
            List<SnapshotData> data;
            lock (_lock)
            {
                data = _order.Select(n => _snapshots[n])
                    .Select(s => new SnapshotData
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                        CreatedAt = s.CreatedAt,
                        EntryCount = s.EntryCount,
                        ContentHash = s.ContentHash,
                        LibraryJson = s.LibraryJson
                    })
                    .ToList();
            }

            var container = new SnapshotContainer
            {
                Version = 1,
                Snapshots = data
            };

            return JsonSerializer.Serialize(container, SerializationGuards.WriteOptions(indented));
        }

        /// <summary>
        /// Deserializes a snapshot manager from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string.</param>
        /// <returns>A new <see cref="PromptSnapshotManager"/> instance.</returns>
        public static PromptSnapshotManager FromJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json);

            var container = JsonSerializer.Deserialize<SnapshotContainer>(json,
                SerializationGuards.ReadCamelCase);

            if (container?.Snapshots == null)
                throw new InvalidOperationException("Invalid snapshot JSON: missing snapshots array.");

            if (container.Snapshots.Count > MaxSnapshots)
                throw new InvalidOperationException(
                    $"Snapshot JSON contains {container.Snapshots.Count} snapshots, exceeding the maximum of {MaxSnapshots}.");

            var manager = new PromptSnapshotManager();

            foreach (var sd in container.Snapshots)
            {
                if (string.IsNullOrWhiteSpace(sd.Name)) continue;

                var snapshot = new PromptSnapshot
                {
                    Id = sd.Id ?? Guid.NewGuid().ToString("N")[..12],
                    Name = sd.Name,
                    Description = sd.Description,
                    CreatedAt = sd.CreatedAt,
                    EntryCount = sd.EntryCount,
                    ContentHash = sd.ContentHash ?? "",
                    LibraryJson = sd.LibraryJson ?? ""
                };

                manager._snapshots[snapshot.Name] = snapshot;
                manager._order.Add(snapshot.Name);
            }

            return manager;
        }

        /// <summary>
        /// Saves all snapshots to a JSON file.
        /// </summary>
        public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            filePath = SerializationGuards.ValidateFilePath(filePath);

            string json = ToJson(true);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        /// <summary>
        /// Loads a snapshot manager from a JSON file.
        /// </summary>
        public static async Task<PromptSnapshotManager> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            filePath = SerializationGuards.ValidateFilePath(filePath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Snapshot file not found: {filePath}", filePath);

            SerializationGuards.ThrowIfFileTooLarge(filePath);

            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return FromJson(json);
        }

        // ──────────────── Private Helpers ────────────────

        private static string ComputeHash(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static SnapshotDiffReport CompareLibraries(
            PromptLibrary fromLib, PromptLibrary toLib,
            string fromName, string toName)
        {
            var diffs = new List<SnapshotEntryDiff>();

            var fromEntries = fromLib.Entries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
            var toEntries = toLib.Entries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            // Find removed and modified entries
            foreach (var (name, fromEntry) in fromEntries)
            {
                if (!toEntries.TryGetValue(name, out var toEntry))
                {
                    diffs.Add(new SnapshotEntryDiff
                    {
                        EntryName = name,
                        DiffType = SnapshotDiffType.Removed,
                        Summary = "Entry removed"
                    });
                    continue;
                }

                // Check template text
                if (!string.Equals(fromEntry.Template.Template, toEntry.Template.Template, StringComparison.Ordinal))
                {
                    diffs.Add(new SnapshotEntryDiff
                    {
                        EntryName = name,
                        DiffType = SnapshotDiffType.TemplateChanged,
                        Summary = $"Template text changed ({fromEntry.Template.Template.Length} → {toEntry.Template.Template.Length} chars)"
                    });
                }

                // Check defaults
                var fromDefaults = fromEntry.Template.Defaults;
                var toDefaults = toEntry.Template.Defaults;
                if (!DefaultsEqual(fromDefaults, toDefaults))
                {
                    diffs.Add(new SnapshotEntryDiff
                    {
                        EntryName = name,
                        DiffType = SnapshotDiffType.DefaultsChanged,
                        Summary = $"Defaults changed ({fromDefaults.Count} → {toDefaults.Count} defaults)"
                    });
                }

                // Check metadata
                var metaChanges = new List<string>();
                if (!string.Equals(fromEntry.Description, toEntry.Description, StringComparison.Ordinal))
                    metaChanges.Add("description");
                if (!string.Equals(fromEntry.Category, toEntry.Category, StringComparison.OrdinalIgnoreCase))
                    metaChanges.Add("category");
                if (!TagsEqual(fromEntry.Tags, toEntry.Tags))
                    metaChanges.Add("tags");

                if (metaChanges.Count > 0)
                {
                    diffs.Add(new SnapshotEntryDiff
                    {
                        EntryName = name,
                        DiffType = SnapshotDiffType.MetadataChanged,
                        Summary = $"Changed: {string.Join(", ", metaChanges)}"
                    });
                }
            }

            // Find added entries
            foreach (var (name, _) in toEntries)
            {
                if (!fromEntries.ContainsKey(name))
                {
                    diffs.Add(new SnapshotEntryDiff
                    {
                        EntryName = name,
                        DiffType = SnapshotDiffType.Added,
                        Summary = "Entry added"
                    });
                }
            }

            return new SnapshotDiffReport
            {
                FromSnapshot = fromName,
                ToSnapshot = toName,
                Diffs = diffs
            };
        }

        private static bool DefaultsEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var (key, value) in a)
            {
                if (!b.TryGetValue(key, out var bValue) || !string.Equals(value, bValue, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static bool TagsEqual(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count != b.Count) return false;
            return a.SetEquals(b);
        }

        // ──────────────── Serialization DTOs ────────────────

        internal class SnapshotContainer
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = 1;

            [JsonPropertyName("snapshots")]
            public List<SnapshotData> Snapshots { get; set; } = new();
        }

        internal class SnapshotData
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("createdAt")]
            public DateTimeOffset CreatedAt { get; set; }

            [JsonPropertyName("entryCount")]
            public int EntryCount { get; set; }

            [JsonPropertyName("contentHash")]
            public string? ContentHash { get; set; }

            [JsonPropertyName("libraryJson")]
            public string? LibraryJson { get; set; }
        }
    }
}
