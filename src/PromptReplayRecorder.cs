namespace Prompt
{
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A recorded prompt interaction (request + response + metadata).
    /// </summary>
    public class RecordedInteraction
    {
        /// <summary>Unique identifier for this recording.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>The prompt text sent to the model.</summary>
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        /// <summary>The system prompt, if any.</summary>
        [JsonPropertyName("systemPrompt")]
        public string? SystemPrompt { get; set; }

        /// <summary>The model response text.</summary>
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";

        /// <summary>Model identifier (e.g. "gpt-4", "claude-3-opus").</summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        /// <summary>Response latency in milliseconds.</summary>
        [JsonPropertyName("latencyMs")]
        public long LatencyMs { get; set; }

        /// <summary>Input token count.</summary>
        [JsonPropertyName("inputTokens")]
        public int InputTokens { get; set; }

        /// <summary>Output token count.</summary>
        [JsonPropertyName("outputTokens")]
        public int OutputTokens { get; set; }

        /// <summary>Estimated cost in USD.</summary>
        [JsonPropertyName("estimatedCostUsd")]
        public double EstimatedCostUsd { get; set; }

        /// <summary>UTC timestamp when the interaction was recorded.</summary>
        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Optional tags for filtering/grouping.</summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>Optional key-value metadata.</summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>Temperature setting used.</summary>
        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        /// <summary>Whether this interaction resulted in an error.</summary>
        [JsonPropertyName("isError")]
        public bool IsError { get; set; }

        /// <summary>Error message if IsError is true.</summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Content-based fingerprint for matching during replay.
        /// Computed from prompt + systemPrompt + model.
        /// </summary>
        [JsonPropertyName("fingerprint")]
        public string Fingerprint { get; set; } = "";

        /// <summary>Compute the content fingerprint for this interaction.</summary>
        internal void ComputeFingerprint()
        {
            var input = $"{Prompt}|{SystemPrompt ?? ""}|{Model}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            Fingerprint = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }
    }

    /// <summary>
    /// A cassette containing a sequence of recorded interactions.
    /// Named after VCR cassettes — record once, replay many times.
    /// </summary>
    public class Cassette
    {
        /// <summary>Cassette name/identifier.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>When this cassette was created.</summary>
        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Human-readable description.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        /// <summary>The recorded interactions in order.</summary>
        [JsonPropertyName("interactions")]
        public List<RecordedInteraction> Interactions { get; set; } = new();

        /// <summary>Cassette-level tags.</summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>Schema version for forward compatibility.</summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Result of a replay operation.
    /// </summary>
    public class ReplayResult
    {
        /// <summary>The matched recorded interaction (null if miss).</summary>
        public RecordedInteraction? Recording { get; set; }

        /// <summary>Whether a matching recording was found.</summary>
        public bool IsHit => Recording != null;

        /// <summary>The fingerprint used for matching.</summary>
        public string Fingerprint { get; set; } = "";

        /// <summary>Match strategy that found the recording.</summary>
        public string MatchStrategy { get; set; } = "";
    }

    /// <summary>
    /// Replay matching strategy.
    /// </summary>
    public enum ReplayMatchStrategy
    {
        /// <summary>Exact match on prompt + systemPrompt + model (fingerprint).</summary>
        Exact,

        /// <summary>Match on prompt + systemPrompt only (ignore model).</summary>
        PromptOnly,

        /// <summary>Sequential — replay in recording order regardless of content.</summary>
        Sequential
    }

    /// <summary>
    /// Statistics for replay operations.
    /// </summary>
    public class ReplayStats
    {
        /// <summary>Total replay attempts.</summary>
        public int TotalAttempts { get; internal set; }

        /// <summary>Successful matches.</summary>
        public int Hits { get; internal set; }

        /// <summary>Failed matches.</summary>
        public int Misses { get; internal set; }

        /// <summary>Hit rate as percentage.</summary>
        public double HitRate => TotalAttempts == 0 ? 0 : Math.Round((double)Hits / TotalAttempts * 100, 2);

        /// <summary>Total tokens replayed (input + output).</summary>
        public long TotalTokensReplayed { get; internal set; }

        /// <summary>Total estimated cost saved by replaying.</summary>
        public double EstimatedCostSaved { get; internal set; }

        /// <summary>Total latency saved (sum of original latencies).</summary>
        public long LatencySavedMs { get; internal set; }
    }

    /// <summary>
    /// VCR-style prompt interaction recorder and replayer.
    ///
    /// <para>Records prompt interactions (input/output/model/latency/tokens) into
    /// named cassettes, then replays them for debugging, regression testing,
    /// or cost estimation — without hitting the LLM API.</para>
    ///
    /// <para>Inspired by Ruby's VCR gem and Python's vcrpy for HTTP cassettes,
    /// but designed specifically for LLM prompt interactions.</para>
    ///
    /// <example>
    /// <code>
    /// // Record
    /// var recorder = new PromptReplayRecorder();
    /// recorder.StartRecording("my-test-session");
    /// recorder.Record(new RecordedInteraction {
    ///     Prompt = "What is 2+2?",
    ///     Response = "4",
    ///     Model = "gpt-4",
    ///     LatencyMs = 320,
    ///     InputTokens = 12,
    ///     OutputTokens = 1
    /// });
    /// recorder.StopRecording();
    ///
    /// // Replay
    /// recorder.LoadCassette("my-test-session");
    /// var result = recorder.Replay("What is 2+2?", model: "gpt-4");
    /// // result.Recording.Response == "4", no API call needed
    ///
    /// // Export/Import
    /// string json = recorder.ExportCassette("my-test-session");
    /// recorder.ImportCassette(json);
    /// </code>
    /// </example>
    /// </summary>
    public class PromptReplayRecorder
    {
        private readonly Dictionary<string, Cassette> _cassettes = new();
        private Cassette? _activeCassette;
        private string? _activeCassetteName;
        private bool _isRecording;
        private int _sequentialIndex;
        private ReplayMatchStrategy _defaultStrategy;
        private readonly ReplayStats _stats = new();
        private readonly object _lock = new();

        /// <summary>
        /// Creates a new PromptReplayRecorder with the specified default match strategy.
        /// </summary>
        /// <param name="defaultStrategy">Default matching strategy for replay operations.</param>
        public PromptReplayRecorder(ReplayMatchStrategy defaultStrategy = ReplayMatchStrategy.Exact)
        {
            _defaultStrategy = defaultStrategy;
        }

        // ── Recording ─────────────────────────────────────────────

        /// <summary>
        /// Start recording interactions into a named cassette.
        /// Creates a new cassette or appends to an existing one.
        /// </summary>
        /// <param name="cassetteName">Name for the cassette.</param>
        /// <param name="description">Optional description.</param>
        /// <param name="append">If true, append to existing cassette; if false, overwrite.</param>
        /// <exception cref="ArgumentException">If cassetteName is null or empty.</exception>
        /// <exception cref="InvalidOperationException">If already recording.</exception>
        public void StartRecording(string cassetteName, string? description = null, bool append = false)
        {
            if (string.IsNullOrWhiteSpace(cassetteName))
                throw new ArgumentException("Cassette name cannot be null or empty.", nameof(cassetteName));

            lock (_lock)
            {
                if (_isRecording)
                    throw new InvalidOperationException(
                        $"Already recording to cassette '{_activeCassetteName}'. Call StopRecording() first.");

                if (!append || !_cassettes.ContainsKey(cassetteName))
                {
                    _cassettes[cassetteName] = new Cassette
                    {
                        Name = cassetteName,
                        Description = description ?? ""
                    };
                }
                else if (description != null)
                {
                    _cassettes[cassetteName].Description = description;
                }

                _activeCassette = _cassettes[cassetteName];
                _activeCassetteName = cassetteName;
                _isRecording = true;
            }
        }

        /// <summary>
        /// Record a prompt interaction into the active cassette.
        /// </summary>
        /// <param name="interaction">The interaction to record.</param>
        /// <exception cref="InvalidOperationException">If not currently recording.</exception>
        /// <exception cref="ArgumentNullException">If interaction is null.</exception>
        public void Record(RecordedInteraction interaction)
        {
            if (interaction == null) throw new ArgumentNullException(nameof(interaction));

            lock (_lock)
            {
                if (!_isRecording || _activeCassette == null)
                    throw new InvalidOperationException("Not recording. Call StartRecording() first.");

                interaction.ComputeFingerprint();
                _activeCassette.Interactions.Add(interaction);
            }
        }

        /// <summary>
        /// Stop recording and finalize the cassette.
        /// </summary>
        /// <returns>The completed cassette.</returns>
        /// <exception cref="InvalidOperationException">If not currently recording.</exception>
        public Cassette StopRecording()
        {
            lock (_lock)
            {
                if (!_isRecording || _activeCassette == null)
                    throw new InvalidOperationException("Not recording.");

                var cassette = _activeCassette;
                _activeCassette = null;
                _activeCassetteName = null;
                _isRecording = false;
                return cassette;
            }
        }

        /// <summary>Whether the recorder is currently in recording mode.</summary>
        public bool IsRecording
        {
            get { lock (_lock) { return _isRecording; } }
        }

        /// <summary>Name of the cassette currently being recorded to.</summary>
        public string? ActiveCassetteName
        {
            get { lock (_lock) { return _activeCassetteName; } }
        }

        // ── Replay ────────────────────────────────────────────────

        /// <summary>
        /// Load a cassette for replay and reset the sequential index.
        /// </summary>
        /// <param name="cassetteName">Name of the cassette to load.</param>
        /// <exception cref="KeyNotFoundException">If cassette doesn't exist.</exception>
        public void LoadCassette(string cassetteName)
        {
            lock (_lock)
            {
                if (!_cassettes.ContainsKey(cassetteName))
                    throw new KeyNotFoundException($"Cassette '{cassetteName}' not found.");

                _activeCassette = _cassettes[cassetteName];
                _activeCassetteName = cassetteName;
                _sequentialIndex = 0;
            }
        }

        /// <summary>
        /// Replay a prompt interaction from the loaded cassette.
        /// </summary>
        /// <param name="prompt">The prompt to match.</param>
        /// <param name="systemPrompt">Optional system prompt to match.</param>
        /// <param name="model">Optional model to match.</param>
        /// <param name="strategy">Override the default match strategy.</param>
        /// <returns>ReplayResult with the matched recording or a miss.</returns>
        /// <exception cref="InvalidOperationException">If no cassette is loaded.</exception>
        public ReplayResult Replay(string prompt, string? systemPrompt = null,
                                    string? model = null, ReplayMatchStrategy? strategy = null)
        {
            lock (_lock)
            {
                if (_activeCassette == null)
                    throw new InvalidOperationException("No cassette loaded. Call LoadCassette() first.");

                var strat = strategy ?? _defaultStrategy;
                _stats.TotalAttempts++;

                RecordedInteraction? match = null;
                string fingerprint = "";
                string strategyName = strat.ToString();

                switch (strat)
                {
                    case ReplayMatchStrategy.Exact:
                        fingerprint = ComputeFingerprint(prompt, systemPrompt, model ?? "");
                        match = _activeCassette.Interactions
                            .FirstOrDefault(i => i.Fingerprint == fingerprint);
                        break;

                    case ReplayMatchStrategy.PromptOnly:
                        fingerprint = ComputeFingerprint(prompt, systemPrompt, "");
                        // Match on prompt + systemPrompt, ignore model
                        match = _activeCassette.Interactions
                            .FirstOrDefault(i =>
                                i.Prompt == prompt &&
                                (i.SystemPrompt ?? "") == (systemPrompt ?? ""));
                        break;

                    case ReplayMatchStrategy.Sequential:
                        if (_sequentialIndex < _activeCassette.Interactions.Count)
                        {
                            match = _activeCassette.Interactions[_sequentialIndex];
                            _sequentialIndex++;
                        }
                        fingerprint = $"seq:{_sequentialIndex - 1}";
                        strategyName = "Sequential";
                        break;
                }

                if (match != null)
                {
                    _stats.Hits++;
                    _stats.TotalTokensReplayed += match.InputTokens + match.OutputTokens;
                    _stats.EstimatedCostSaved += match.EstimatedCostUsd;
                    _stats.LatencySavedMs += match.LatencyMs;
                }
                else
                {
                    _stats.Misses++;
                }

                return new ReplayResult
                {
                    Recording = match,
                    Fingerprint = fingerprint,
                    MatchStrategy = strategyName
                };
            }
        }

        /// <summary>
        /// Reset the sequential replay index to the beginning.
        /// </summary>
        public void ResetSequentialIndex()
        {
            lock (_lock) { _sequentialIndex = 0; }
        }

        // ── Cassette Management ───────────────────────────────────

        /// <summary>Get the names of all stored cassettes.</summary>
        public List<string> ListCassettes()
        {
            lock (_lock) { return _cassettes.Keys.OrderBy(k => k).ToList(); }
        }

        /// <summary>Get a cassette by name.</summary>
        /// <param name="cassetteName">The cassette name.</param>
        /// <returns>The cassette, or null if not found.</returns>
        public Cassette? GetCassette(string cassetteName)
        {
            lock (_lock)
            {
                return _cassettes.TryGetValue(cassetteName, out var c) ? c : null;
            }
        }

        /// <summary>Remove a cassette.</summary>
        /// <param name="cassetteName">The cassette name.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveCassette(string cassetteName)
        {
            lock (_lock)
            {
                if (_activeCassetteName == cassetteName)
                {
                    _activeCassette = null;
                    _activeCassetteName = null;
                    _isRecording = false;
                }
                return _cassettes.Remove(cassetteName);
            }
        }

        /// <summary>Remove all cassettes and reset state.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cassettes.Clear();
                _activeCassette = null;
                _activeCassetteName = null;
                _isRecording = false;
                _sequentialIndex = 0;
            }
        }

        /// <summary>Total number of stored cassettes.</summary>
        public int CassetteCount
        {
            get { lock (_lock) { return _cassettes.Count; } }
        }

        // ── Tags ──────────────────────────────────────────────────

        /// <summary>
        /// Add a tag to a cassette.
        /// </summary>
        public void TagCassette(string cassetteName, string tag)
        {
            lock (_lock)
            {
                if (!_cassettes.TryGetValue(cassetteName, out var c))
                    throw new KeyNotFoundException($"Cassette '{cassetteName}' not found.");
                if (!c.Tags.Contains(tag))
                    c.Tags.Add(tag);
            }
        }

        /// <summary>
        /// Find cassettes by tag.
        /// </summary>
        public List<string> FindByTag(string tag)
        {
            lock (_lock)
            {
                return _cassettes
                    .Where(kv => kv.Value.Tags.Contains(tag))
                    .Select(kv => kv.Key)
                    .OrderBy(k => k)
                    .ToList();
            }
        }

        // ── Export / Import ────────────────────────────────────────

        /// <summary>
        /// Export a cassette as a JSON string.
        /// </summary>
        /// <param name="cassetteName">The cassette to export.</param>
        /// <returns>JSON string representation.</returns>
        /// <exception cref="KeyNotFoundException">If cassette doesn't exist.</exception>
        public string ExportCassette(string cassetteName)
        {
            lock (_lock)
            {
                if (!_cassettes.TryGetValue(cassetteName, out var cassette))
                    throw new KeyNotFoundException($"Cassette '{cassetteName}' not found.");

                return JsonSerializer.Serialize(cassette, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
        }

        /// <summary>
        /// Import a cassette from a JSON string.
        /// </summary>
        /// <param name="json">JSON string of a Cassette.</param>
        /// <param name="overwrite">If true, overwrite existing cassette with same name.</param>
        /// <returns>The imported cassette name.</returns>
        /// <exception cref="ArgumentException">If JSON is invalid or cassette name is empty.</exception>
        /// <exception cref="InvalidOperationException">If cassette exists and overwrite is false.</exception>
        public string ImportCassette(string json, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or empty.", nameof(json));

            SerializationGuards.ThrowIfPayloadTooLarge(json);

            Cassette cassette;
            try
            {
                cassette = JsonSerializer.Deserialize<Cassette>(json)
                    ?? throw new ArgumentException("Deserialized cassette was null.");
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid cassette JSON: {ex.Message}", nameof(json), ex);
            }

            if (string.IsNullOrWhiteSpace(cassette.Name))
                throw new ArgumentException("Cassette name cannot be empty.");

            lock (_lock)
            {
                if (_cassettes.ContainsKey(cassette.Name) && !overwrite)
                    throw new InvalidOperationException(
                        $"Cassette '{cassette.Name}' already exists. Use overwrite: true to replace.");

                // Recompute fingerprints
                foreach (var interaction in cassette.Interactions)
                    interaction.ComputeFingerprint();

                _cassettes[cassette.Name] = cassette;
                return cassette.Name;
            }
        }

        /// <summary>
        /// Export all cassettes as a JSON string.
        /// </summary>
        public string ExportAll()
        {
            lock (_lock)
            {
                var all = _cassettes.Values.ToList();
                return JsonSerializer.Serialize(all, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
        }

        // ── Analysis ──────────────────────────────────────────────

        /// <summary>
        /// Get replay statistics.
        /// </summary>
        public ReplayStats GetStats()
        {
            lock (_lock)
            {
                return new ReplayStats
                {
                    TotalAttempts = _stats.TotalAttempts,
                    Hits = _stats.Hits,
                    Misses = _stats.Misses,
                    TotalTokensReplayed = _stats.TotalTokensReplayed,
                    EstimatedCostSaved = _stats.EstimatedCostSaved,
                    LatencySavedMs = _stats.LatencySavedMs
                };
            }
        }

        /// <summary>
        /// Get a summary of a cassette's contents.
        /// </summary>
        /// <param name="cassetteName">The cassette to summarize.</param>
        /// <returns>Summary dictionary with interaction count, models, total tokens, etc.</returns>
        public Dictionary<string, object> GetCassetteSummary(string cassetteName)
        {
            lock (_lock)
            {
                if (!_cassettes.TryGetValue(cassetteName, out var c))
                    throw new KeyNotFoundException($"Cassette '{cassetteName}' not found.");

                var models = c.Interactions.Select(i => i.Model).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();
                var totalInputTokens = c.Interactions.Sum(i => i.InputTokens);
                var totalOutputTokens = c.Interactions.Sum(i => i.OutputTokens);
                var totalCost = c.Interactions.Sum(i => i.EstimatedCostUsd);
                var totalLatency = c.Interactions.Sum(i => i.LatencyMs);
                var errorCount = c.Interactions.Count(i => i.IsError);
                var allTags = c.Interactions.SelectMany(i => i.Tags).Distinct().OrderBy(t => t).ToList();

                return new Dictionary<string, object>
                {
                    ["name"] = c.Name,
                    ["description"] = c.Description,
                    ["interactionCount"] = c.Interactions.Count,
                    ["models"] = models,
                    ["totalInputTokens"] = totalInputTokens,
                    ["totalOutputTokens"] = totalOutputTokens,
                    ["totalTokens"] = totalInputTokens + totalOutputTokens,
                    ["totalEstimatedCostUsd"] = Math.Round(totalCost, 6),
                    ["totalLatencyMs"] = totalLatency,
                    ["avgLatencyMs"] = c.Interactions.Count > 0
                        ? Math.Round((double)totalLatency / c.Interactions.Count, 1) : 0.0,
                    ["errorCount"] = errorCount,
                    ["tags"] = allTags,
                    ["createdAt"] = c.CreatedAt.ToString("o")
                };
            }
        }

        /// <summary>
        /// Compare two cassettes and return the differences.
        /// Useful for regression testing — did the model outputs change?
        /// </summary>
        /// <param name="cassetteA">First cassette name.</param>
        /// <param name="cassetteB">Second cassette name.</param>
        /// <returns>List of comparison entries showing matches, mismatches, and missing items.</returns>
        public List<Dictionary<string, object>> CompareCassettes(string cassetteA, string cassetteB)
        {
            lock (_lock)
            {
                if (!_cassettes.TryGetValue(cassetteA, out var a))
                    throw new KeyNotFoundException($"Cassette '{cassetteA}' not found.");
                if (!_cassettes.TryGetValue(cassetteB, out var b))
                    throw new KeyNotFoundException($"Cassette '{cassetteB}' not found.");

                var results = new List<Dictionary<string, object>>();
                var bByFingerprint = b.Interactions
                    .GroupBy(i => i.Fingerprint)
                    .ToDictionary(g => g.Key, g => g.First());

                var matchedFingerprints = new HashSet<string>();

                foreach (var ia in a.Interactions)
                {
                    if (bByFingerprint.TryGetValue(ia.Fingerprint, out var ib))
                    {
                        matchedFingerprints.Add(ia.Fingerprint);
                        var match = ia.Response == ib.Response;
                        var entry = new Dictionary<string, object>
                        {
                            ["status"] = match ? "match" : "mismatch",
                            ["fingerprint"] = ia.Fingerprint,
                            ["prompt"] = ia.Prompt.Length > 80
                                ? ia.Prompt[..80] + "..." : ia.Prompt,
                            ["responseA"] = ia.Response.Length > 100
                                ? ia.Response[..100] + "..." : ia.Response,
                            ["responseB"] = ib.Response.Length > 100
                                ? ib.Response[..100] + "..." : ib.Response,
                            ["latencyDiffMs"] = ib.LatencyMs - ia.LatencyMs
                        };
                        results.Add(entry);
                    }
                    else
                    {
                        results.Add(new Dictionary<string, object>
                        {
                            ["status"] = "only_in_a",
                            ["fingerprint"] = ia.Fingerprint,
                            ["prompt"] = ia.Prompt.Length > 80
                                ? ia.Prompt[..80] + "..." : ia.Prompt,
                        });
                    }
                }

                foreach (var ib in b.Interactions)
                {
                    if (!matchedFingerprints.Contains(ib.Fingerprint))
                    {
                        results.Add(new Dictionary<string, object>
                        {
                            ["status"] = "only_in_b",
                            ["fingerprint"] = ib.Fingerprint,
                            ["prompt"] = ib.Prompt.Length > 80
                                ? ib.Prompt[..80] + "..." : ib.Prompt,
                        });
                    }
                }

                return results;
            }
        }

        // ── Internal helpers ──────────────────────────────────────

        private static string ComputeFingerprint(string prompt, string? systemPrompt, string model)
        {
            var input = $"{prompt}|{systemPrompt ?? ""}|{model}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }
    }
}
