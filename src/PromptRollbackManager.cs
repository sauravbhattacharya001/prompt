namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A versioned snapshot of a prompt with metadata and optional quality score.
    /// </summary>
    public class RollbackVersion
    {
        /// <summary>Auto-incrementing version number.</summary>
        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>The prompt text at this version.</summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        /// <summary>When this version was created.</summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Optional commit-style message describing the change.</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        /// <summary>Optional quality/performance score (0-100) for this version.</summary>
        [JsonPropertyName("score")]
        public double? Score { get; set; }

        /// <summary>Arbitrary metadata tags.</summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>Character count delta from previous version (null for v1).</summary>
        [JsonPropertyName("char_delta")]
        public int? CharDelta { get; set; }

        /// <summary>Whether this version was created by a rollback.</summary>
        [JsonPropertyName("is_rollback")]
        public bool IsRollback { get; set; }

        /// <summary>If this is a rollback, the version it was rolled back to.</summary>
        [JsonPropertyName("rolled_back_from")]
        public int? RolledBackFrom { get; set; }
    }

    /// <summary>
    /// Result of a rollback operation.
    /// </summary>
    public class RollbackResult
    {
        /// <summary>Whether the rollback succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The version that was active before the rollback.</summary>
        public int PreviousVersion { get; set; }

        /// <summary>The version that is now active (the rollback target copy).</summary>
        public int NewVersion { get; set; }

        /// <summary>The version whose text was restored.</summary>
        public int RestoredFromVersion { get; set; }

        /// <summary>Human-readable summary.</summary>
        public string Summary { get; set; } = "";
    }

    /// <summary>
    /// Result of comparing two prompt versions.
    /// </summary>
    public class VersionComparison
    {
        /// <summary>Version A number.</summary>
        public int VersionA { get; set; }

        /// <summary>Version B number.</summary>
        public int VersionB { get; set; }

        /// <summary>Whether the text is identical.</summary>
        public bool TextIdentical { get; set; }

        /// <summary>Character count of version A.</summary>
        public int CharsA { get; set; }

        /// <summary>Character count of version B.</summary>
        public int CharsB { get; set; }

        /// <summary>Character difference (B - A).</summary>
        public int CharDelta { get; set; }

        /// <summary>Score of version A (null if unscored).</summary>
        public double? ScoreA { get; set; }

        /// <summary>Score of version B (null if unscored).</summary>
        public double? ScoreB { get; set; }

        /// <summary>Score difference (B - A), null if either is unscored.</summary>
        public double? ScoreDelta { get; set; }

        /// <summary>Lines added from A to B.</summary>
        public int LinesAdded { get; set; }

        /// <summary>Lines removed from A to B.</summary>
        public int LinesRemoved { get; set; }
    }

    /// <summary>
    /// Manages prompt versioning with commit, rollback, and comparison capabilities.
    /// Think of it as a lightweight Git for your prompts — every change is tracked,
    /// scored, and reversible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var mgr = new PromptRollbackManager("greeting");
    ///
    /// // Commit initial version
    /// mgr.Commit("Hello, how can I help?", "initial version");
    ///
    /// // Iterate on the prompt
    /// mgr.Commit("Hi! I'm your AI assistant. How can I help you today?", "friendlier tone");
    ///
    /// // Score versions based on evaluation results
    /// mgr.SetScore(1, 72.0);
    /// mgr.SetScore(2, 65.0);  // oops, v2 scored worse
    ///
    /// // Roll back to the better version
    /// var result = mgr.Rollback(1);
    /// // result.NewVersion == 3, text matches v1
    ///
    /// // Get the best-performing version
    /// var best = mgr.BestVersion();  // returns v1 (score 72)
    ///
    /// // Export full history as JSON
    /// string json = mgr.ExportJson();
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptRollbackManager
    {
        private readonly string _name;
        private readonly List<RollbackVersion> _versions = new();

        /// <summary>The prompt name/identifier.</summary>
        public string Name => _name;

        /// <summary>All versions in chronological order.</summary>
        public IReadOnlyList<RollbackVersion> Versions => _versions.AsReadOnly();

        /// <summary>The current (latest) version number, or 0 if empty.</summary>
        public int CurrentVersion => _versions.Count > 0 ? _versions[^1].Version : 0;

        /// <summary>The current prompt text, or empty string if no versions exist.</summary>
        public string CurrentText => _versions.Count > 0 ? _versions[^1].Text : "";

        /// <summary>
        /// Creates a new rollback manager for a named prompt.
        /// </summary>
        /// <param name="name">Identifier for the prompt being versioned.</param>
        public PromptRollbackManager(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Commits a new version of the prompt.
        /// </summary>
        /// <param name="text">The new prompt text.</param>
        /// <param name="message">Optional commit message describing the change.</param>
        /// <param name="tags">Optional metadata tags.</param>
        /// <returns>The newly created version.</returns>
        public RollbackVersion Commit(string text, string? message = null, Dictionary<string, string>? tags = null)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var version = new RollbackVersion
            {
                Version = _versions.Count + 1,
                Text = text,
                CreatedAt = DateTime.UtcNow,
                Message = message ?? "",
                Tags = tags ?? new(),
            };

            if (_versions.Count > 0)
            {
                version.CharDelta = text.Length - _versions[^1].Text.Length;
            }

            _versions.Add(version);
            return version;
        }

        /// <summary>
        /// Sets the quality score for a specific version.
        /// </summary>
        /// <param name="version">Version number to score.</param>
        /// <param name="score">Quality score (0-100).</param>
        public void SetScore(int version, double score)
        {
            var v = GetVersion(version);
            if (v == null) throw new ArgumentException($"Version {version} not found.");
            v.Score = Math.Clamp(score, 0, 100);
        }

        /// <summary>
        /// Gets a specific version by number.
        /// </summary>
        /// <param name="version">1-based version number.</param>
        /// <returns>The version, or null if not found.</returns>
        public RollbackVersion? GetVersion(int version)
        {
            return _versions.FirstOrDefault(v => v.Version == version);
        }

        /// <summary>
        /// Rolls back to a previous version by creating a new version with
        /// the old text. History is preserved — rollback is non-destructive.
        /// </summary>
        /// <param name="targetVersion">Version number to roll back to.</param>
        /// <param name="message">Optional rollback message.</param>
        /// <returns>Result describing the rollback.</returns>
        public RollbackResult Rollback(int targetVersion, string? message = null)
        {
            var target = GetVersion(targetVersion);
            if (target == null)
                throw new ArgumentException($"Version {targetVersion} not found.");

            int prevVersion = CurrentVersion;

            var newVersion = Commit(
                target.Text,
                message ?? $"Rollback to v{targetVersion}"
            );
            newVersion.IsRollback = true;
            newVersion.RolledBackFrom = targetVersion;

            return new RollbackResult
            {
                Success = true,
                PreviousVersion = prevVersion,
                NewVersion = newVersion.Version,
                RestoredFromVersion = targetVersion,
                Summary = $"Rolled back from v{prevVersion} to v{targetVersion} (now v{newVersion.Version})"
            };
        }

        /// <summary>
        /// Returns the version with the highest score, or null if none are scored.
        /// </summary>
        public RollbackVersion? BestVersion()
        {
            return _versions
                .Where(v => v.Score.HasValue)
                .OrderByDescending(v => v.Score!.Value)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the version with the lowest score, or null if none are scored.
        /// </summary>
        public RollbackVersion? WorstVersion()
        {
            return _versions
                .Where(v => v.Score.HasValue)
                .OrderBy(v => v.Score!.Value)
                .FirstOrDefault();
        }

        /// <summary>
        /// Compares two versions and returns a structured diff summary.
        /// </summary>
        public VersionComparison Compare(int versionA, int versionB)
        {
            var a = GetVersion(versionA) ?? throw new ArgumentException($"Version {versionA} not found.");
            var b = GetVersion(versionB) ?? throw new ArgumentException($"Version {versionB} not found.");

            var linesA = a.Text.Split('\n');
            var linesB = b.Text.Split('\n');
            var setA = new HashSet<string>(linesA);
            var setB = new HashSet<string>(linesB);

            return new VersionComparison
            {
                VersionA = versionA,
                VersionB = versionB,
                TextIdentical = a.Text == b.Text,
                CharsA = a.Text.Length,
                CharsB = b.Text.Length,
                CharDelta = b.Text.Length - a.Text.Length,
                ScoreA = a.Score,
                ScoreB = b.Score,
                ScoreDelta = (a.Score.HasValue && b.Score.HasValue) ? b.Score.Value - a.Score.Value : null,
                LinesAdded = linesB.Count(l => !setA.Contains(l)),
                LinesRemoved = linesA.Count(l => !setB.Contains(l)),
            };
        }

        /// <summary>
        /// Returns versions whose score dropped compared to their predecessor,
        /// useful for identifying regressions.
        /// </summary>
        public List<RollbackVersion> FindRegressions()
        {
            var regressions = new List<RollbackVersion>();
            for (int i = 1; i < _versions.Count; i++)
            {
                var prev = _versions[i - 1];
                var curr = _versions[i];
                if (prev.Score.HasValue && curr.Score.HasValue && curr.Score.Value < prev.Score.Value)
                {
                    regressions.Add(curr);
                }
            }
            return regressions;
        }

        /// <summary>
        /// Returns a score trend across all scored versions as (version, score) pairs.
        /// </summary>
        public List<(int Version, double Score)> ScoreTrend()
        {
            return _versions
                .Where(v => v.Score.HasValue)
                .Select(v => (v.Version, v.Score!.Value))
                .ToList();
        }

        /// <summary>
        /// Automatically rolls back to the best-scoring version if the current
        /// version's score is below the threshold. Returns null if no rollback needed.
        /// </summary>
        /// <param name="threshold">Minimum acceptable score.</param>
        public RollbackResult? AutoRollbackIfBelow(double threshold)
        {
            var current = _versions.LastOrDefault();
            if (current == null || !current.Score.HasValue) return null;
            if (current.Score.Value >= threshold) return null;

            var best = BestVersion();
            if (best == null || best.Version == current.Version) return null;
            if (!best.Score.HasValue || best.Score.Value <= current.Score.Value) return null;

            return Rollback(best.Version,
                $"Auto-rollback: v{current.Version} scored {current.Score.Value:F1} (below {threshold:F1}), restoring v{best.Version} ({best.Score.Value:F1})");
        }

        /// <summary>
        /// Exports the full version history as formatted JSON.
        /// </summary>
        public string ExportJson()
        {
            var data = new
            {
                name = _name,
                current_version = CurrentVersion,
                total_versions = _versions.Count,
                versions = _versions
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
        }

        /// <summary>
        /// Imports version history from JSON exported by <see cref="ExportJson"/>.
        /// Appends to existing history.
        /// </summary>
        public static PromptRollbackManager ImportJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var name = root.GetProperty("name").GetString() ?? "imported";
            var mgr = new PromptRollbackManager(name);

            if (root.TryGetProperty("versions", out var versionsEl))
            {
                foreach (var vEl in versionsEl.EnumerateArray())
                {
                    var version = new RollbackVersion
                    {
                        Version = vEl.GetProperty("version").GetInt32(),
                        Text = vEl.GetProperty("text").GetString() ?? "",
                        Message = vEl.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
                        CreatedAt = vEl.TryGetProperty("createdAt", out var ca) ? ca.GetDateTime() : DateTime.UtcNow,
                        IsRollback = vEl.TryGetProperty("isRollback", out var rb) && rb.GetBoolean(),
                    };
                    if (vEl.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number)
                        version.Score = sc.GetDouble();
                    if (vEl.TryGetProperty("rolledBackFrom", out var rbf) && rbf.ValueKind == JsonValueKind.Number)
                        version.RolledBackFrom = rbf.GetInt32();
                    if (vEl.TryGetProperty("charDelta", out var cd) && cd.ValueKind == JsonValueKind.Number)
                        version.CharDelta = cd.GetInt32();

                    mgr._versions.Add(version);
                }
            }

            return mgr;
        }

        /// <summary>
        /// Returns a human-readable summary of the version history.
        /// </summary>
        public string Summary()
        {
            if (_versions.Count == 0) return $"[{_name}] No versions.";

            var lines = new List<string>
            {
                $"Prompt: {_name}",
                $"Versions: {_versions.Count} | Current: v{CurrentVersion}",
                ""
            };

            foreach (var v in _versions)
            {
                var scoreStr = v.Score.HasValue ? $" score={v.Score.Value:F1}" : "";
                var rollbackStr = v.IsRollback ? $" ← rollback from v{v.RolledBackFrom}" : "";
                var deltaStr = v.CharDelta.HasValue ? $" ({(v.CharDelta.Value >= 0 ? "+" : "")}{v.CharDelta.Value} chars)" : "";
                var preview = v.Text.Length > 60 ? v.Text[..57] + "..." : v.Text;
                lines.Add($"  v{v.Version}: {preview}{deltaStr}{scoreStr}{rollbackStr}");
                if (!string.IsNullOrEmpty(v.Message))
                    lines.Add($"        → {v.Message}");
            }

            var best = BestVersion();
            if (best != null)
                lines.Add($"\n  Best: v{best.Version} (score {best.Score!.Value:F1})");

            var regressions = FindRegressions();
            if (regressions.Count > 0)
                lines.Add($"  ⚠ {regressions.Count} regression(s) detected");

            return string.Join("\n", lines);
        }
    }
}
