namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Snapshot / golden-file testing for prompt outputs.  Captures expected
    /// ("golden") responses for a set of test inputs and compares subsequent
    /// runs against them, surfacing regressions, improvements, and drift.
    /// </summary>
    public class PromptGoldenTester
    {
        private readonly string _name;
        private readonly Dictionary<string, GoldenEntry> _entries = new();
        private double _matchThreshold = 0.95;
        private double _driftThreshold = 0.70;

        /// <summary>
        /// Initializes a new golden tester suite with the specified name.
        /// </summary>
        /// <param name="name">Unique name identifying this golden test suite.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
        public PromptGoldenTester(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be empty.", nameof(name));
            _name = name;
        }

        /// <summary>Gets the name of this golden test suite.</summary>
        public string Name => _name;

        /// <summary>Gets the number of golden entries registered in this suite.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Sets the similarity threshold at or above which an output is considered a match.
        /// </summary>
        /// <param name="threshold">A value in (0, 1.0].</param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is out of range.</exception>
        public PromptGoldenTester WithMatchThreshold(double threshold)
        {
            if (threshold <= 0 || threshold > 1.0)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            _matchThreshold = threshold;
            return this;
        }

        /// <summary>
        /// Sets the similarity threshold below which an output is classified as a regression.
        /// Values between drift and match thresholds are classified as drift.
        /// </summary>
        /// <param name="threshold">A value in [0, matchThreshold).</param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is out of range.</exception>
        public PromptGoldenTester WithDriftThreshold(double threshold)
        {
            if (threshold < 0 || threshold >= _matchThreshold)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            _driftThreshold = threshold;
            return this;
        }

        /// <summary>
        /// Records a golden (expected) output for a given test input.
        /// </summary>
        /// <param name="id">Unique identifier for this golden entry.</param>
        /// <param name="input">The prompt input text.</param>
        /// <param name="expectedOutput">The expected (golden) output to compare against.</param>
        /// <param name="tags">Optional tags for filtering entries in batch runs.</param>
        /// <exception cref="ArgumentException">Thrown when required string parameters are null or whitespace.</exception>
        public void RecordGolden(string id, string input, string expectedOutput, params string[] tags)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id must not be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Input must not be empty.", nameof(input));
            if (string.IsNullOrWhiteSpace(expectedOutput)) throw new ArgumentException("Expected output must not be empty.", nameof(expectedOutput));
            _entries[id] = new GoldenEntry { Id = id, Input = input, GoldenOutput = expectedOutput, Tags = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>(), RecordedAt = DateTimeOffset.UtcNow, Version = 1 };
        }

        /// <summary>Removes a golden entry by its identifier.</summary>
        /// <param name="id">The entry identifier to remove.</param>
        /// <returns><c>true</c> if the entry was found and removed; otherwise <c>false</c>.</returns>
        public bool RemoveGolden(string id) => _entries.Remove(id);

        /// <summary>Retrieves a golden entry by its identifier, or <c>null</c> if not found.</summary>
        /// <param name="id">The entry identifier.</param>
        /// <returns>The <see cref="GoldenEntry"/> if found; otherwise <c>null</c>.</returns>
        public GoldenEntry? GetEntry(string id) => _entries.TryGetValue(id, out var e) ? e : null;

        /// <summary>
        /// Lists all entry identifiers, optionally filtered by tag.
        /// </summary>
        /// <param name="tag">If specified, only entries containing this tag are returned.</param>
        /// <returns>A sorted list of matching entry identifiers.</returns>
        public IReadOnlyList<string> ListIds(string? tag = null)
        {
            var q = _entries.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(tag)) q = q.Where(e => e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
            return q.Select(e => e.Id).OrderBy(id => id).ToList();
        }

        /// <summary>
        /// Compares an actual output against the stored golden output for the given entry,
        /// computing a similarity score and classifying the result as match, drift, or regression.
        /// </summary>
        /// <param name="id">The golden entry identifier to compare against.</param>
        /// <param name="actualOutput">The actual output produced by the current prompt run.</param>
        /// <returns>A <see cref="CompareResult"/> with similarity score, status, and diffs.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when no entry exists for <paramref name="id"/>.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actualOutput"/> is null.</exception>
        public CompareResult Compare(string id, string actualOutput)
        {
            if (!_entries.TryGetValue(id, out var entry)) throw new KeyNotFoundException($"No golden entry found for '{id}'.");
            if (actualOutput == null) throw new ArgumentNullException(nameof(actualOutput));
            double similarity = ComputeSimilarity(entry.GoldenOutput, actualOutput);
            entry.LastActual = actualOutput;
            entry.LastComparedAt = DateTimeOffset.UtcNow;
            entry.LastSimilarity = similarity;
            return new CompareResult { Id = id, Status = ClassifyStatus(similarity), SimilarityScore = similarity, GoldenOutput = entry.GoldenOutput, ActualOutput = actualOutput, Diffs = ComputeDiffs(entry.GoldenOutput, actualOutput), ComparedAt = entry.LastComparedAt.Value };
        }

        /// <summary>
        /// Promotes the last compared actual output to become the new golden output,
        /// incrementing the entry version.
        /// </summary>
        /// <param name="id">The golden entry identifier to approve.</param>
        /// <returns><c>true</c> if the entry was updated; <c>false</c> if the entry was not found or had no last actual output.</returns>
        public bool ApproveActual(string id)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.LastActual == null) return false;
            entry.GoldenOutput = entry.LastActual; entry.Version++; entry.RecordedAt = DateTimeOffset.UtcNow; entry.LastActual = null; entry.LastSimilarity = null;
            return true;
        }

        /// <summary>
        /// Runs a batch comparison across all entries (optionally filtered by tag),
        /// invoking <paramref name="runFunc"/> for each input and comparing against golden outputs.
        /// </summary>
        /// <param name="runFunc">A function that takes a prompt input and returns the actual output.</param>
        /// <param name="tag">If specified, only entries with this tag are included.</param>
        /// <returns>A <see cref="BatchReport"/> summarizing match/drift/regression/error counts and average similarity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="runFunc"/> is null.</exception>
        public BatchReport RunBatch(Func<string, string> runFunc, string? tag = null)
        {
            if (runFunc == null) throw new ArgumentNullException(nameof(runFunc));
            var ids = ListIds(tag); var results = new List<CompareResult>();
            foreach (var id in ids)
            {
                var entry = _entries[id];
                try { results.Add(Compare(id, runFunc(entry.Input))); }
                catch (Exception ex) { results.Add(new CompareResult { Id = id, Status = GoldenStatus.Error, SimilarityScore = 0, GoldenOutput = entry.GoldenOutput, ActualOutput = $"[ERROR] {ex.Message}", Diffs = new List<DiffSegment>(), ComparedAt = DateTimeOffset.UtcNow }); }
            }
            return new BatchReport { SuiteName = _name, Results = results, TotalCount = results.Count, MatchCount = results.Count(r => r.Status == GoldenStatus.Match), DriftCount = results.Count(r => r.Status == GoldenStatus.Drift), RegressionCount = results.Count(r => r.Status == GoldenStatus.Regression), ErrorCount = results.Count(r => r.Status == GoldenStatus.Error), AverageSimilarity = results.Count > 0 ? results.Average(r => r.SimilarityScore) : 0, RunAt = DateTimeOffset.UtcNow };
        }

        /// <summary>
        /// Serializes all golden entries and suite configuration to a JSON string for persistence or sharing.
        /// </summary>
        /// <returns>A formatted JSON string representing the entire golden test suite.</returns>
        public string ExportJson()
        {
            return JsonSerializer.Serialize(new GoldenExport { SuiteName = _name, MatchThreshold = _matchThreshold, DriftThreshold = _driftThreshold, Entries = _entries.Values.OrderBy(e => e.Id).ToList(), ExportedAt = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        }

        /// <summary>
        /// Imports golden entries from a JSON string previously produced by <see cref="ExportJson"/>.
        /// Existing entries with the same identifier are overwritten.
        /// </summary>
        /// <param name="json">The JSON string to import.</param>
        /// <returns>The number of entries imported.</returns>
        /// <exception cref="ArgumentException">Thrown when JSON is empty or malformed.</exception>
        public int ImportJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json, nameof(json));
            GoldenExport? export;
            try { export = JsonSerializer.Deserialize<GoldenExport>(json, SerializationGuards.ReadCamelCase); }
            catch (JsonException ex) { throw new ArgumentException($"Invalid JSON: {ex.Message}", nameof(json)); }
            if (export?.Entries == null) return 0;
            int count = 0;
            foreach (var entry in export.Entries) { if (!string.IsNullOrWhiteSpace(entry.Id)) { _entries[entry.Id] = entry; count++; } }
            return count;
        }

        /// <summary>
        /// Formats a <see cref="BatchReport"/> into a human-readable text summary with per-entry status icons and diffs.
        /// </summary>
        /// <param name="report">The batch report to format.</param>
        /// <returns>A multi-line formatted string suitable for console or log output.</returns>
        public static string FormatReport(BatchReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Golden Test Report: {report.SuiteName} ===");
            sb.AppendLine($"Run at: {report.RunAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total: {report.TotalCount}  Match: {report.MatchCount}  Drift: {report.DriftCount}  Regression: {report.RegressionCount}  Error: {report.ErrorCount}");
            sb.AppendLine($"Average similarity: {report.AverageSimilarity:P1}");
            sb.AppendLine();
            foreach (var r in report.Results)
            {
                string icon = r.Status switch { GoldenStatus.Match => "✓", GoldenStatus.Drift => "~", GoldenStatus.Regression => "✗", GoldenStatus.Error => "!", _ => "?" };
                sb.AppendLine($"  {icon} [{r.Id}] {r.Status} (similarity: {r.SimilarityScore:P1})");
                if (r.Status != GoldenStatus.Match && r.Diffs.Count > 0) foreach (var d in r.Diffs.Take(3)) sb.AppendLine($"      {d.Type}: \"{StringHelpers.Truncate(d.Text, 60)}\"");
            }
            return sb.ToString();
        }

        internal static double ComputeSimilarity(string a, string b)
        {
            if (a == b) return 1.0;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
            var ba = TextAnalysisHelpers.GetNgrams(a.ToLowerInvariant(), 2); var bb = TextAnalysisHelpers.GetNgrams(b.ToLowerInvariant(), 2);
            if (ba.Count == 0 && bb.Count == 0) return 1.0;
            if (ba.Count == 0 || bb.Count == 0) return 0.0;
            int inter = 0; var copy = new Dictionary<string, int>(bb);
            foreach (var kv in ba) if (copy.TryGetValue(kv.Key, out int cb)) { int s = Math.Min(kv.Value, cb); inter += s; copy[kv.Key] = cb - s; }
            return (2.0 * inter) / (ba.Values.Sum() + bb.Values.Sum());
        }

        // GetBigrams consolidated into TextAnalysisHelpers.GetNgrams(text, 2)

        internal static List<DiffSegment> ComputeDiffs(string golden, string actual)
        {
            var gw = golden.Split(' ', StringSplitOptions.RemoveEmptyEntries); var aw = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var diffs = new List<DiffSegment>(); var gs = new HashSet<string>(gw); var aset = new HashSet<string>(aw);
            var rem = gw.Where(w => !aset.Contains(w)).ToList(); if (rem.Count > 0) diffs.Add(new DiffSegment { Type = DiffType.Removed, Text = string.Join(" ", rem) });
            var add = aw.Where(w => !gs.Contains(w)).ToList(); if (add.Count > 0) diffs.Add(new DiffSegment { Type = DiffType.Added, Text = string.Join(" ", add) });
            return diffs;
        }

        private GoldenStatus ClassifyStatus(double sim) => sim >= _matchThreshold ? GoldenStatus.Match : sim >= _driftThreshold ? GoldenStatus.Drift : GoldenStatus.Regression;

        /// <summary>
        /// Classification of a golden comparison outcome.
        /// </summary>
        public enum GoldenStatus
        {
            /// <summary>Output similarity is at or above the match threshold.</summary>
            Match,
            /// <summary>Similarity sits between the drift and match thresholds (acceptable but worth review).</summary>
            Drift,
            /// <summary>Similarity has fallen below the drift threshold (regression).</summary>
            Regression,
            /// <summary>The run function threw an exception; no similarity was computed.</summary>
            Error,
        }

        /// <summary>
        /// Type of word-level change reported in a <see cref="DiffSegment"/>.
        /// </summary>
        public enum DiffType
        {
            /// <summary>Words present in the golden output but missing from the actual output.</summary>
            Removed,
            /// <summary>Words present in the actual output but missing from the golden output.</summary>
            Added,
        }

        /// <summary>
        /// A single golden test entry: an input plus the expected (golden) output
        /// and bookkeeping for the most recent comparison.
        /// </summary>
        public class GoldenEntry
        {
            /// <summary>Unique identifier for this entry within the suite.</summary>
            public string Id { get; set; } = "";
            /// <summary>Prompt input text that produced <see cref="GoldenOutput"/>.</summary>
            public string Input { get; set; } = "";
            /// <summary>Expected (golden) output to compare future runs against.</summary>
            public string GoldenOutput { get; set; } = "";
            /// <summary>Free-form tags used to filter entries in batch runs.</summary>
            public List<string> Tags { get; set; } = new();
            /// <summary>UTC timestamp at which this entry's current golden output was recorded.</summary>
            public DateTimeOffset RecordedAt { get; set; }
            /// <summary>Monotonic version, incremented each time the golden output is approved/updated.</summary>
            public int Version { get; set; } = 1;
            /// <summary>Most recent actual output observed by <see cref="Compare"/>, or <c>null</c> if none.</summary>
            public string? LastActual { get; set; }
            /// <summary>UTC timestamp of the most recent comparison, or <c>null</c> if never compared.</summary>
            public DateTimeOffset? LastComparedAt { get; set; }
            /// <summary>Similarity score (0.0-1.0) of the most recent comparison, or <c>null</c> if never compared.</summary>
            public double? LastSimilarity { get; set; }
        }

        /// <summary>
        /// Result of comparing an actual output against a stored golden output.
        /// </summary>
        public class CompareResult
        {
            /// <summary>Identifier of the <see cref="GoldenEntry"/> that was compared.</summary>
            public string Id { get; set; } = "";
            /// <summary>Classification of the comparison outcome.</summary>
            public GoldenStatus Status { get; set; }
            /// <summary>Bigram-overlap similarity score in the range [0.0, 1.0].</summary>
            public double SimilarityScore { get; set; }
            /// <summary>The expected (golden) output that was used as the baseline.</summary>
            public string GoldenOutput { get; set; } = "";
            /// <summary>The actual output produced by the run being compared.</summary>
            public string ActualOutput { get; set; } = "";
            /// <summary>Word-level diff segments highlighting added and removed words.</summary>
            public List<DiffSegment> Diffs { get; set; } = new();
            /// <summary>UTC timestamp at which the comparison was performed.</summary>
            public DateTimeOffset ComparedAt { get; set; }
        }

        /// <summary>
        /// A single word-level difference between golden and actual outputs.
        /// </summary>
        public class DiffSegment
        {
            /// <summary>Whether the words in <see cref="Text"/> were added or removed.</summary>
            public DiffType Type { get; set; }
            /// <summary>Space-joined words involved in this diff segment.</summary>
            public string Text { get; set; } = "";
        }

        /// <summary>
        /// Aggregate report produced by <see cref="RunBatch"/>, summarising the
        /// outcome across every entry executed in a single batch.
        /// </summary>
        public class BatchReport
        {
            /// <summary>Name of the golden test suite this report was produced from.</summary>
            public string SuiteName { get; set; } = "";
            /// <summary>Per-entry comparison results in execution order.</summary>
            public List<CompareResult> Results { get; set; } = new();
            /// <summary>Total number of entries executed in this batch.</summary>
            public int TotalCount { get; set; }
            /// <summary>Count of entries whose <see cref="CompareResult.Status"/> is <see cref="GoldenStatus.Match"/>.</summary>
            public int MatchCount { get; set; }
            /// <summary>Count of entries classified as <see cref="GoldenStatus.Drift"/>.</summary>
            public int DriftCount { get; set; }
            /// <summary>Count of entries classified as <see cref="GoldenStatus.Regression"/>.</summary>
            public int RegressionCount { get; set; }
            /// <summary>Count of entries whose run function threw an exception.</summary>
            public int ErrorCount { get; set; }
            /// <summary>Arithmetic mean of <see cref="CompareResult.SimilarityScore"/> across all entries (0.0 when the batch is empty).</summary>
            public double AverageSimilarity { get; set; }
            /// <summary>UTC timestamp at which the batch run completed.</summary>
            public DateTimeOffset RunAt { get; set; }
        }

        /// <summary>
        /// Internal DTO used by <see cref="ExportJson"/> / <see cref="ImportJson"/>
        /// to round-trip an entire suite to and from JSON.
        /// </summary>
        private class GoldenExport
        {
            public string SuiteName { get; set; } = "";
            public double MatchThreshold { get; set; }
            public double DriftThreshold { get; set; }
            public List<GoldenEntry> Entries { get; set; } = new();
            public DateTimeOffset ExportedAt { get; set; }
        }
    }
}