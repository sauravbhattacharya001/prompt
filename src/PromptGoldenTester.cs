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

        public PromptGoldenTester(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be empty.", nameof(name));
            _name = name;
        }

        public string Name => _name;
        public int Count => _entries.Count;

        public PromptGoldenTester WithMatchThreshold(double threshold)
        {
            if (threshold <= 0 || threshold > 1.0)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            _matchThreshold = threshold;
            return this;
        }

        public PromptGoldenTester WithDriftThreshold(double threshold)
        {
            if (threshold < 0 || threshold >= _matchThreshold)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            _driftThreshold = threshold;
            return this;
        }

        public void RecordGolden(string id, string input, string expectedOutput, params string[] tags)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id must not be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Input must not be empty.", nameof(input));
            if (string.IsNullOrWhiteSpace(expectedOutput)) throw new ArgumentException("Expected output must not be empty.", nameof(expectedOutput));
            _entries[id] = new GoldenEntry { Id = id, Input = input, GoldenOutput = expectedOutput, Tags = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>(), RecordedAt = DateTimeOffset.UtcNow, Version = 1 };
        }

        public bool RemoveGolden(string id) => _entries.Remove(id);
        public GoldenEntry? GetEntry(string id) => _entries.TryGetValue(id, out var e) ? e : null;

        public IReadOnlyList<string> ListIds(string? tag = null)
        {
            var q = _entries.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(tag)) q = q.Where(e => e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
            return q.Select(e => e.Id).OrderBy(id => id).ToList();
        }

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

        public bool ApproveActual(string id)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.LastActual == null) return false;
            entry.GoldenOutput = entry.LastActual; entry.Version++; entry.RecordedAt = DateTimeOffset.UtcNow; entry.LastActual = null; entry.LastSimilarity = null;
            return true;
        }

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

        public string ExportJson()
        {
            return JsonSerializer.Serialize(new GoldenExport { SuiteName = _name, MatchThreshold = _matchThreshold, DriftThreshold = _driftThreshold, Entries = _entries.Values.OrderBy(e => e.Id).ToList(), ExportedAt = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        }

        public int ImportJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON must not be empty.", nameof(json));
            GoldenExport? export;
            try { export = JsonSerializer.Deserialize<GoldenExport>(json); }
            catch (JsonException ex) { throw new ArgumentException($"Invalid JSON: {ex.Message}", nameof(json)); }
            if (export?.Entries == null) return 0;
            int count = 0;
            foreach (var entry in export.Entries) { if (!string.IsNullOrWhiteSpace(entry.Id)) { _entries[entry.Id] = entry; count++; } }
            return count;
        }

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
                if (r.Status != GoldenStatus.Match && r.Diffs.Count > 0) foreach (var d in r.Diffs.Take(3)) sb.AppendLine($"      {d.Type}: \"{Truncate(d.Text, 60)}\"");
            }
            return sb.ToString();
        }

        internal static double ComputeSimilarity(string a, string b)
        {
            if (a == b) return 1.0;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
            var ba = GetBigrams(a.ToLowerInvariant()); var bb = GetBigrams(b.ToLowerInvariant());
            if (ba.Count == 0 && bb.Count == 0) return 1.0;
            if (ba.Count == 0 || bb.Count == 0) return 0.0;
            int inter = 0; var copy = new Dictionary<string, int>(bb);
            foreach (var kv in ba) if (copy.TryGetValue(kv.Key, out int cb)) { int s = Math.Min(kv.Value, cb); inter += s; copy[kv.Key] = cb - s; }
            return (2.0 * inter) / (ba.Values.Sum() + bb.Values.Sum());
        }

        private static Dictionary<string, int> GetBigrams(string text)
        { var bg = new Dictionary<string, int>(); for (int i = 0; i < text.Length - 1; i++) { var s = text.Substring(i, 2); bg[s] = bg.GetValueOrDefault(s) + 1; } return bg; }

        internal static List<DiffSegment> ComputeDiffs(string golden, string actual)
        {
            var gw = golden.Split(' ', StringSplitOptions.RemoveEmptyEntries); var aw = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var diffs = new List<DiffSegment>(); var gs = new HashSet<string>(gw); var aset = new HashSet<string>(aw);
            var rem = gw.Where(w => !aset.Contains(w)).ToList(); if (rem.Count > 0) diffs.Add(new DiffSegment { Type = DiffType.Removed, Text = string.Join(" ", rem) });
            var add = aw.Where(w => !gs.Contains(w)).ToList(); if (add.Count > 0) diffs.Add(new DiffSegment { Type = DiffType.Added, Text = string.Join(" ", add) });
            return diffs;
        }

        private GoldenStatus ClassifyStatus(double sim) => sim >= _matchThreshold ? GoldenStatus.Match : sim >= _driftThreshold ? GoldenStatus.Drift : GoldenStatus.Regression;
        private static string Truncate(string t, int m) => t.Length <= m ? t : t[..(m - 3)] + "...";

        public enum GoldenStatus { Match, Drift, Regression, Error }
        public enum DiffType { Removed, Added }
        public class GoldenEntry { public string Id { get; set; } = ""; public string Input { get; set; } = ""; public string GoldenOutput { get; set; } = ""; public List<string> Tags { get; set; } = new(); public DateTimeOffset RecordedAt { get; set; } public int Version { get; set; } = 1; public string? LastActual { get; set; } public DateTimeOffset? LastComparedAt { get; set; } public double? LastSimilarity { get; set; } }
        public class CompareResult { public string Id { get; set; } = ""; public GoldenStatus Status { get; set; } public double SimilarityScore { get; set; } public string GoldenOutput { get; set; } = ""; public string ActualOutput { get; set; } = ""; public List<DiffSegment> Diffs { get; set; } = new(); public DateTimeOffset ComparedAt { get; set; } }
        public class DiffSegment { public DiffType Type { get; set; } public string Text { get; set; } = ""; }
        public class BatchReport { public string SuiteName { get; set; } = ""; public List<CompareResult> Results { get; set; } = new(); public int TotalCount { get; set; } public int MatchCount { get; set; } public int DriftCount { get; set; } public int RegressionCount { get; set; } public int ErrorCount { get; set; } public double AverageSimilarity { get; set; } public DateTimeOffset RunAt { get; set; } }
        private class GoldenExport { public string SuiteName { get; set; } = ""; public double MatchThreshold { get; set; } public double DriftThreshold { get; set; } public List<GoldenEntry> Entries { get; set; } = new(); public DateTimeOffset ExportedAt { get; set; } }
    }
}
