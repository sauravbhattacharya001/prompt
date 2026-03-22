namespace Prompt
{
    using System.Text.Json;

    /// <summary>
    /// Represents a reusable prompt profile with configurable parameters
    /// that can be quickly switched between for different use cases.
    /// </summary>
    public record PromptProfile(
        string Name,
        string Description,
        string? SystemPrompt = null,
        double Temperature = 0.7,
        int? MaxTokens = null,
        double TopP = 1.0,
        double FrequencyPenalty = 0.0,
        double PresencePenalty = 0.0,
        string? StopSequence = null,
        string? ParentProfile = null,
        Dictionary<string, string>? Metadata = null
    );

    /// <summary>
    /// Manages prompt profiles, allowing users to define, switch between,
    /// and compose different prompt configurations for various tasks.
    /// Supports profile inheritance, quick switching, and profile comparison.
    /// </summary>
    public class PromptProfileSwitcher
    {
        private readonly Dictionary<string, PromptProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
        private string? _activeProfileName;

        /// <summary>Built-in profiles for common use cases.</summary>
        public static readonly IReadOnlyDictionary<string, PromptProfile> BuiltInProfiles = new Dictionary<string, PromptProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["creative"] = new PromptProfile(
                "Creative",
                "High creativity, varied outputs — ideal for brainstorming and writing",
                Temperature: 1.0, TopP: 0.95, FrequencyPenalty: 0.3, PresencePenalty: 0.3),
            ["precise"] = new PromptProfile(
                "Precise",
                "Low temperature, deterministic outputs — ideal for code and data extraction",
                Temperature: 0.1, TopP: 0.9, FrequencyPenalty: 0.0, PresencePenalty: 0.0),
            ["concise"] = new PromptProfile(
                "Concise",
                "Short, focused responses with strict token limits",
                Temperature: 0.5, MaxTokens: 256, FrequencyPenalty: 0.5, PresencePenalty: 0.0),
            ["balanced"] = new PromptProfile(
                "Balanced",
                "General-purpose defaults for everyday tasks",
                Temperature: 0.7, TopP: 1.0, FrequencyPenalty: 0.0, PresencePenalty: 0.0),
            ["conversational"] = new PromptProfile(
                "Conversational",
                "Natural, varied dialogue — ideal for chatbots and assistants",
                SystemPrompt: "You are a helpful, friendly assistant.",
                Temperature: 0.8, TopP: 0.95, FrequencyPenalty: 0.2, PresencePenalty: 0.6),
        };

        /// <summary>The currently active profile, or null if none is set.</summary>
        public PromptProfile? ActiveProfile => _activeProfileName != null && _profiles.ContainsKey(_activeProfileName)
            ? _profiles[_activeProfileName]
            : null;

        /// <summary>Name of the currently active profile.</summary>
        public string? ActiveProfileName => _activeProfileName;

        /// <summary>All registered profile names.</summary>
        public IReadOnlyCollection<string> ProfileNames => _profiles.Keys;

        /// <summary>Number of registered profiles.</summary>
        public int Count => _profiles.Count;

        /// <summary>
        /// Creates a new switcher, optionally loading built-in profiles.
        /// </summary>
        public PromptProfileSwitcher(bool loadBuiltIns = true)
        {
            if (loadBuiltIns)
            {
                foreach (var kvp in BuiltInProfiles)
                    _profiles[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>Register a custom profile.</summary>
        public void Register(string key, PromptProfile profile)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(profile);
            _profiles[key] = profile;
        }

        /// <summary>Remove a profile by key.</summary>
        public bool Remove(string key)
        {
            if (_activeProfileName?.Equals(key, StringComparison.OrdinalIgnoreCase) == true)
                _activeProfileName = null;
            return _profiles.Remove(key);
        }

        /// <summary>Switch to a profile by key.</summary>
        public PromptProfile SwitchTo(string key)
        {
            if (!_profiles.TryGetValue(key, out var profile))
                throw new KeyNotFoundException($"Profile '{key}' not found. Available: {string.Join(", ", _profiles.Keys)}");
            _activeProfileName = key;
            return profile;
        }

        /// <summary>Get a profile by key without switching.</summary>
        public PromptProfile Get(string key)
        {
            if (!_profiles.TryGetValue(key, out var profile))
                throw new KeyNotFoundException($"Profile '{key}' not found.");
            return profile;
        }

        /// <summary>Check if a profile exists.</summary>
        public bool HasProfile(string key) => _profiles.ContainsKey(key);

        /// <summary>
        /// Resolve a profile, applying parent profile inheritance.
        /// Child values override parent values; null/default child values inherit from parent.
        /// </summary>
        public PromptProfile Resolve(string key, int maxDepth = 10)
        {
            var profile = Get(key);
            if (profile.ParentProfile == null)
                return profile;

            var chain = new List<PromptProfile> { profile };
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key };
            var current = profile;

            while (current.ParentProfile != null && chain.Count < maxDepth)
            {
                if (!visited.Add(current.ParentProfile))
                    throw new InvalidOperationException($"Circular profile inheritance detected at '{current.ParentProfile}'.");
                current = Get(current.ParentProfile);
                chain.Add(current);
            }

            // Merge from root (last) to leaf (first)
            chain.Reverse();
            var merged = chain[0];
            for (int i = 1; i < chain.Count; i++)
            {
                var child = chain[i];
                merged = merged with
                {
                    Name = child.Name,
                    Description = child.Description,
                    SystemPrompt = child.SystemPrompt ?? merged.SystemPrompt,
                    Temperature = child.Temperature != 0.7 ? child.Temperature : merged.Temperature,
                    MaxTokens = child.MaxTokens ?? merged.MaxTokens,
                    TopP = child.TopP != 1.0 ? child.TopP : merged.TopP,
                    FrequencyPenalty = child.FrequencyPenalty != 0.0 ? child.FrequencyPenalty : merged.FrequencyPenalty,
                    PresencePenalty = child.PresencePenalty != 0.0 ? child.PresencePenalty : merged.PresencePenalty,
                    StopSequence = child.StopSequence ?? merged.StopSequence,
                    Metadata = MergeMetadata(merged.Metadata, child.Metadata),
                };
            }

            return merged;
        }

        /// <summary>
        /// Compare two profiles side-by-side, returning a list of differences.
        /// </summary>
        public IReadOnlyList<ProfileDifference> Compare(string keyA, string keyB)
        {
            var a = Get(keyA);
            var b = Get(keyB);
            var diffs = new List<ProfileDifference>();

            void Check(string prop, object? valA, object? valB)
            {
                var sA = valA?.ToString() ?? "(null)";
                var sB = valB?.ToString() ?? "(null)";
                if (sA != sB)
                    diffs.Add(new ProfileDifference(prop, sA, sB));
            }

            Check("SystemPrompt", a.SystemPrompt, b.SystemPrompt);
            Check("Temperature", a.Temperature, b.Temperature);
            Check("MaxTokens", a.MaxTokens, b.MaxTokens);
            Check("TopP", a.TopP, b.TopP);
            Check("FrequencyPenalty", a.FrequencyPenalty, b.FrequencyPenalty);
            Check("PresencePenalty", a.PresencePenalty, b.PresencePenalty);
            Check("StopSequence", a.StopSequence, b.StopSequence);

            return diffs;
        }

        /// <summary>
        /// Create a new profile by blending two profiles with a weight (0.0 = all A, 1.0 = all B).
        /// Numeric parameters are interpolated; non-numeric take from whichever side the weight favours.
        /// </summary>
        public PromptProfile Blend(string keyA, string keyB, double weight = 0.5, string? newName = null)
        {
            if (weight < 0 || weight > 1)
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be between 0.0 and 1.0");

            var a = Get(keyA);
            var b = Get(keyB);

            double Lerp(double x, double y) => x + (y - x) * weight;
            int? LerpInt(int? x, int? y) => (x, y) switch
            {
                (null, null) => null,
                (not null, null) => (int)(x.Value * (1 - weight)),
                (null, not null) => (int)(y.Value * weight),
                _ => (int)Lerp(x!.Value, y!.Value),
            };

            return new PromptProfile(
                Name: newName ?? $"{a.Name}+{b.Name}",
                Description: $"Blend of {a.Name} ({1 - weight:P0}) and {b.Name} ({weight:P0})",
                SystemPrompt: weight < 0.5 ? a.SystemPrompt : b.SystemPrompt,
                Temperature: Lerp(a.Temperature, b.Temperature),
                MaxTokens: LerpInt(a.MaxTokens, b.MaxTokens),
                TopP: Lerp(a.TopP, b.TopP),
                FrequencyPenalty: Lerp(a.FrequencyPenalty, b.FrequencyPenalty),
                PresencePenalty: Lerp(a.PresencePenalty, b.PresencePenalty),
                StopSequence: weight < 0.5 ? a.StopSequence : b.StopSequence,
                Metadata: MergeMetadata(a.Metadata, b.Metadata)
            );
        }

        /// <summary>Export all profiles to JSON.</summary>
        public string ExportJson()
        {
            var data = new Dictionary<string, object>
            {
                ["activeProfile"] = _activeProfileName ?? "",
                ["profiles"] = _profiles
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Import profiles from JSON, optionally overwriting existing ones.</summary>
        public int ImportJson(string json, bool overwrite = false)
        {
            var doc = JsonDocument.Parse(json);
            int count = 0;

            if (doc.RootElement.TryGetProperty("profiles", out var profilesEl))
            {
                foreach (var prop in profilesEl.EnumerateObject())
                {
                    if (!overwrite && _profiles.ContainsKey(prop.Name))
                        continue;

                    var profile = JsonSerializer.Deserialize<PromptProfile>(prop.Value.GetRawText());
                    if (profile != null)
                    {
                        _profiles[prop.Name] = profile;
                        count++;
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("activeProfile", out var activeEl))
            {
                var active = activeEl.GetString();
                if (!string.IsNullOrEmpty(active) && _profiles.ContainsKey(active))
                    _activeProfileName = active;
            }

            return count;
        }

        /// <summary>List all profiles with a summary of their settings.</summary>
        public string ListProfiles()
        {
            var lines = new List<string> { $"Profiles ({_profiles.Count}):" };
            foreach (var kvp in _profiles.OrderBy(p => p.Key))
            {
                var active = kvp.Key.Equals(_activeProfileName, StringComparison.OrdinalIgnoreCase) ? " [ACTIVE]" : "";
                var p = kvp.Value;
                lines.Add($"  {kvp.Key}{active}: {p.Description} (temp={p.Temperature}, topP={p.TopP}, maxTok={p.MaxTokens?.ToString() ?? "∞"})");
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static Dictionary<string, string>? MergeMetadata(Dictionary<string, string>? parent, Dictionary<string, string>? child)
        {
            if (parent == null && child == null) return null;
            var merged = new Dictionary<string, string>(parent ?? new());
            if (child != null)
                foreach (var kvp in child)
                    merged[kvp.Key] = kvp.Value;
            return merged;
        }
    }

    /// <summary>Represents a single difference between two profiles.</summary>
    public record ProfileDifference(string Property, string ValueA, string ValueB);
}
