namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Quality label for a dataset example, used for filtering and stratified sampling.
    /// </summary>
    public enum ExampleQuality
    {
        /// <summary>Not yet rated.</summary>
        Unrated,
        /// <summary>Poor quality — may be used as negative examples.</summary>
        Poor,
        /// <summary>Acceptable quality for training.</summary>
        Acceptable,
        /// <summary>Good quality example.</summary>
        Good,
        /// <summary>Excellent — ideal example for few-shot or fine-tuning.</summary>
        Excellent
    }

    /// <summary>
    /// The output format for dataset export.
    /// </summary>
    public enum DatasetFormat
    {
        /// <summary>JSON array of objects.</summary>
        Json,
        /// <summary>JSON Lines (one JSON object per line) — standard for fine-tuning.</summary>
        Jsonl,
        /// <summary>Comma-separated values with headers.</summary>
        Csv,
        /// <summary>Chat-completion format (messages array per line).</summary>
        ChatJsonl,
        /// <summary>Alpaca instruction format.</summary>
        Alpaca
    }

    /// <summary>
    /// Split assignment for train/validation/test partitioning.
    /// </summary>
    public enum DatasetSplit
    {
        /// <summary>Training split.</summary>
        Train,
        /// <summary>Validation split.</summary>
        Validation,
        /// <summary>Test/evaluation split.</summary>
        Test
    }

    /// <summary>
    /// A single example in a dataset consisting of a prompt (input) and
    /// expected response (output), with optional metadata.
    /// </summary>
    public class DatasetExample
    {
        /// <summary>Unique identifier for this example.</summary>
        public string Id { get; }

        /// <summary>The system prompt or instruction context.</summary>
        public string SystemPrompt { get; set; } = "";

        /// <summary>The user prompt / input text.</summary>
        public string Input { get; }

        /// <summary>The expected or actual response / output text.</summary>
        public string Output { get; set; } = "";

        /// <summary>Quality rating for this example.</summary>
        public ExampleQuality Quality { get; set; } = ExampleQuality.Unrated;

        /// <summary>User-defined tags for categorization.</summary>
        public List<string> Tags { get; } = new();

        /// <summary>Arbitrary metadata key-value pairs.</summary>
        public Dictionary<string, string> Metadata { get; } = new();

        /// <summary>When this example was added (UTC).</summary>
        public DateTime CreatedAt { get; }

        /// <summary>Optional source reference (file, URL, session ID, etc.).</summary>
        public string Source { get; set; } = "";

        /// <summary>Assigned split after partitioning.</summary>
        public DatasetSplit? Split { get; internal set; }

        /// <summary>Token count estimate for input + output (set during analysis).</summary>
        public int EstimatedTokens { get; internal set; }

        /// <summary>Creates a new dataset example.</summary>
        /// <param name="id">Unique identifier.</param>
        /// <param name="input">The prompt/input text.</param>
        public DatasetExample(string id, string input)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Id required", nameof(id)) : id;
            Input = string.IsNullOrWhiteSpace(input) ? throw new ArgumentException("Input required", nameof(input)) : input;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>Creates an example with input and output.</summary>
        public DatasetExample(string id, string input, string output) : this(id, input)
        {
            Output = output ?? "";
        }
    }

    /// <summary>
    /// Configuration for dataset splitting.
    /// </summary>
    public class SplitConfig
    {
        /// <summary>Fraction for training (0.0–1.0).</summary>
        public double TrainRatio { get; set; } = 0.8;

        /// <summary>Fraction for validation (0.0–1.0).</summary>
        public double ValidationRatio { get; set; } = 0.1;

        /// <summary>Fraction for test (0.0–1.0).</summary>
        public double TestRatio { get; set; } = 0.1;

        /// <summary>Random seed for reproducible splits.</summary>
        public int? Seed { get; set; }

        /// <summary>Whether to stratify splits by quality label.</summary>
        public bool Stratify { get; set; } = false;

        /// <summary>Validates that ratios sum to ~1.0.</summary>
        public bool IsValid => Math.Abs(TrainRatio + ValidationRatio + TestRatio - 1.0) < 0.001
                               && TrainRatio >= 0 && ValidationRatio >= 0 && TestRatio >= 0;
    }

    /// <summary>
    /// Result of a deduplication pass.
    /// </summary>
    public class DeduplicationResult
    {
        /// <summary>Number of duplicates removed.</summary>
        public int Removed { get; init; }

        /// <summary>Number of examples remaining.</summary>
        public int Remaining { get; init; }

        /// <summary>Groups of duplicate IDs that were found.</summary>
        public IReadOnlyList<IReadOnlyList<string>> DuplicateGroups { get; init; } = Array.Empty<IReadOnlyList<string>>();
    }

    /// <summary>
    /// Statistics about the dataset.
    /// </summary>
    public class DatasetStats
    {
        /// <summary>Total number of examples.</summary>
        public int TotalExamples { get; init; }

        /// <summary>Count per quality level.</summary>
        public IReadOnlyDictionary<ExampleQuality, int> QualityCounts { get; init; } = new Dictionary<ExampleQuality, int>();

        /// <summary>Count per split.</summary>
        public IReadOnlyDictionary<DatasetSplit, int> SplitCounts { get; init; } = new Dictionary<DatasetSplit, int>();

        /// <summary>Count per tag.</summary>
        public IReadOnlyDictionary<string, int> TagCounts { get; init; } = new Dictionary<string, int>();

        /// <summary>Average input length in characters.</summary>
        public double AvgInputLength { get; init; }

        /// <summary>Average output length in characters.</summary>
        public double AvgOutputLength { get; init; }

        /// <summary>Total estimated tokens across all examples.</summary>
        public int TotalEstimatedTokens { get; init; }

        /// <summary>Number of examples with empty outputs.</summary>
        public int EmptyOutputCount { get; init; }

        /// <summary>Number of unique sources.</summary>
        public int UniqueSourceCount { get; init; }

        /// <summary>Min input length.</summary>
        public int MinInputLength { get; init; }

        /// <summary>Max input length.</summary>
        public int MaxInputLength { get; init; }

        /// <summary>Min output length.</summary>
        public int MinOutputLength { get; init; }

        /// <summary>Max output length.</summary>
        public int MaxOutputLength { get; init; }
    }

    /// <summary>
    /// Builds evaluation and fine-tuning datasets from prompt-response pairs.
    /// Supports quality filtering, deduplication, train/val/test splitting,
    /// sampling, and export to JSONL, CSV, chat-completion, and Alpaca formats.
    /// </summary>
    public class PromptDatasetBuilder
    {
        private readonly List<DatasetExample> _examples = new();
        private readonly Dictionary<string, DatasetExample> _index = new(StringComparer.Ordinal);

        /// <summary>Gets the current number of examples in the dataset.</summary>
        public int Count => _examples.Count;

        /// <summary>Gets all examples (read-only snapshot).</summary>
        public IReadOnlyList<DatasetExample> Examples => _examples.AsReadOnly();

        /// <summary>Adds a single example to the dataset.</summary>
        /// <returns>This builder for chaining.</returns>
        public PromptDatasetBuilder Add(DatasetExample example)
        {
            if (example == null) throw new ArgumentNullException(nameof(example));
            if (_index.ContainsKey(example.Id))
                throw new InvalidOperationException($"Duplicate example ID: {example.Id}");
            _examples.Add(example);
            _index[example.Id] = example;
            return this;
        }

        /// <summary>Adds an example by input and output strings (auto-generates ID).</summary>
        public PromptDatasetBuilder Add(string input, string output, ExampleQuality quality = ExampleQuality.Unrated)
        {
            var ex = new DatasetExample($"ex-{_examples.Count + 1:D5}", input, output) { Quality = quality };
            return Add(ex);
        }

        /// <summary>Adds an example with system prompt, input, and output.</summary>
        public PromptDatasetBuilder Add(string systemPrompt, string input, string output, ExampleQuality quality = ExampleQuality.Unrated)
        {
            var ex = new DatasetExample($"ex-{_examples.Count + 1:D5}", input, output)
            {
                SystemPrompt = systemPrompt,
                Quality = quality
            };
            return Add(ex);
        }

        /// <summary>Adds multiple examples at once.</summary>
        public PromptDatasetBuilder AddRange(IEnumerable<DatasetExample> examples)
        {
            foreach (var ex in examples) Add(ex);
            return this;
        }

        /// <summary>Gets an example by ID, or null if not found.</summary>
        public DatasetExample? Get(string id) => _index.TryGetValue(id, out var ex) ? ex : null;

        /// <summary>Removes an example by ID. Returns true if found and removed.</summary>
        public bool Remove(string id)
        {
            if (!_index.TryGetValue(id, out var ex)) return false;
            _examples.Remove(ex);
            _index.Remove(id);
            return true;
        }

        /// <summary>Removes all examples matching a predicate.</summary>
        /// <returns>Number of examples removed.</returns>
        public int RemoveWhere(Func<DatasetExample, bool> predicate)
        {
            var toRemove = _examples.Where(predicate).ToList();
            foreach (var ex in toRemove)
            {
                _examples.Remove(ex);
                _index.Remove(ex.Id);
            }
            return toRemove.Count;
        }

        /// <summary>Filters examples by minimum quality level.</summary>
        /// <returns>Number of examples removed below the threshold.</returns>
        public int FilterByQuality(ExampleQuality minQuality)
        {
            return RemoveWhere(ex => ex.Quality != ExampleQuality.Unrated && ex.Quality < minQuality);
        }

        /// <summary>Removes examples with empty outputs.</summary>
        /// <returns>Number removed.</returns>
        public int RemoveEmptyOutputs()
        {
            return RemoveWhere(ex => string.IsNullOrWhiteSpace(ex.Output));
        }

        /// <summary>Filters by tag — keeps only examples that have at least one of the specified tags.</summary>
        /// <returns>Number removed.</returns>
        public int FilterByTags(IEnumerable<string> keepTags)
        {
            var tagSet = new HashSet<string>(keepTags, StringComparer.OrdinalIgnoreCase);
            return RemoveWhere(ex => !ex.Tags.Any(t => tagSet.Contains(t)));
        }

        /// <summary>Removes examples where input length (chars) exceeds the limit.</summary>
        public int FilterByInputLength(int maxChars)
        {
            return RemoveWhere(ex => ex.Input.Length > maxChars);
        }

        /// <summary>
        /// Deduplicates examples based on normalized input text.
        /// Keeps the first occurrence (or highest quality if tied).
        /// </summary>
        public DeduplicationResult Deduplicate()
        {
            var groups = _examples
                .GroupBy(ex => NormalizeForDedup(ex.Input))
                .Where(g => g.Count() > 1)
                .ToList();

            var dupGroups = new List<IReadOnlyList<string>>();
            var toRemove = new List<DatasetExample>();

            foreach (var group in groups)
            {
                var sorted = group.OrderByDescending(e => e.Quality).ThenBy(e => e.CreatedAt).ToList();
                dupGroups.Add(sorted.Select(e => e.Id).ToList());
                toRemove.AddRange(sorted.Skip(1)); // keep first (best quality)
            }

            // Also find exact-match singletons that weren't in groups
            var exactGroups = _examples
                .GroupBy(ex => NormalizeForDedup(ex.Input))
                .Where(g => g.Count() == 1)
                .ToList();

            foreach (var ex in toRemove)
            {
                _examples.Remove(ex);
                _index.Remove(ex.Id);
            }

            return new DeduplicationResult
            {
                Removed = toRemove.Count,
                Remaining = _examples.Count,
                DuplicateGroups = dupGroups
            };
        }

        private static string NormalizeForDedup(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
        }

        /// <summary>
        /// Samples N examples randomly (with optional seed for reproducibility).
        /// </summary>
        public PromptDatasetBuilder Sample(int count, int? seed = null)
        {
            if (count <= 0) throw new ArgumentException("Count must be positive", nameof(count));
            if (count >= _examples.Count) return this;

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var shuffled = _examples.OrderBy(_ => rng.Next()).Take(count).ToList();
            var keepIds = new HashSet<string>(shuffled.Select(e => e.Id));

            var toRemove = _examples.Where(e => !keepIds.Contains(e.Id)).ToList();
            foreach (var ex in toRemove)
            {
                _examples.Remove(ex);
                _index.Remove(ex.Id);
            }
            return this;
        }

        /// <summary>
        /// Assigns each example to a train/validation/test split based on the provided config.
        /// </summary>
        public PromptDatasetBuilder Split(SplitConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid) throw new ArgumentException("Split ratios must sum to 1.0 and be non-negative");

            var rng = config.Seed.HasValue ? new Random(config.Seed.Value) : new Random();

            if (config.Stratify)
            {
                // Stratified split by quality
                foreach (var group in _examples.GroupBy(e => e.Quality))
                {
                    AssignSplits(group.OrderBy(_ => rng.Next()).ToList(), config);
                }
            }
            else
            {
                var shuffled = _examples.OrderBy(_ => rng.Next()).ToList();
                AssignSplits(shuffled, config);
            }

            return this;
        }

        private static void AssignSplits(List<DatasetExample> items, SplitConfig config)
        {
            int trainEnd = (int)Math.Round(items.Count * config.TrainRatio);
            int valEnd = trainEnd + (int)Math.Round(items.Count * config.ValidationRatio);

            for (int i = 0; i < items.Count; i++)
            {
                items[i].Split = i < trainEnd ? DatasetSplit.Train
                    : i < valEnd ? DatasetSplit.Validation
                    : DatasetSplit.Test;
            }
        }

        /// <summary>
        /// Estimates token counts for all examples using a simple whitespace+punctuation heuristic
        /// (~4 chars per token approximation).
        /// </summary>
        public PromptDatasetBuilder EstimateTokens()
        {
            foreach (var ex in _examples)
            {
                var totalChars = ex.SystemPrompt.Length + ex.Input.Length + ex.Output.Length;
                ex.EstimatedTokens = Math.Max(1, (int)Math.Ceiling(totalChars / 4.0));
            }
            return this;
        }

        /// <summary>Computes dataset statistics.</summary>
        public DatasetStats GetStats()
        {
            if (_examples.Count == 0)
            {
                return new DatasetStats
                {
                    TotalExamples = 0,
                    QualityCounts = new Dictionary<ExampleQuality, int>(),
                    SplitCounts = new Dictionary<DatasetSplit, int>(),
                    TagCounts = new Dictionary<string, int>()
                };
            }

            return new DatasetStats
            {
                TotalExamples = _examples.Count,
                QualityCounts = _examples.GroupBy(e => e.Quality).ToDictionary(g => g.Key, g => g.Count()),
                SplitCounts = _examples.Where(e => e.Split.HasValue).GroupBy(e => e.Split!.Value).ToDictionary(g => g.Key, g => g.Count()),
                TagCounts = _examples.SelectMany(e => e.Tags).GroupBy(t => t, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
                AvgInputLength = _examples.Average(e => e.Input.Length),
                AvgOutputLength = _examples.Average(e => (double)e.Output.Length),
                TotalEstimatedTokens = _examples.Sum(e => e.EstimatedTokens),
                EmptyOutputCount = _examples.Count(e => string.IsNullOrWhiteSpace(e.Output)),
                UniqueSourceCount = _examples.Where(e => !string.IsNullOrEmpty(e.Source)).Select(e => e.Source).Distinct().Count(),
                MinInputLength = _examples.Min(e => e.Input.Length),
                MaxInputLength = _examples.Max(e => e.Input.Length),
                MinOutputLength = _examples.Min(e => e.Output.Length),
                MaxOutputLength = _examples.Max(e => e.Output.Length)
            };
        }

        /// <summary>
        /// Exports the dataset (or a specific split) to the specified format.
        /// </summary>
        /// <param name="format">Output format.</param>
        /// <param name="split">Optional: export only examples in this split.</param>
        /// <returns>Formatted string ready to write to file.</returns>
        public string Export(DatasetFormat format, DatasetSplit? split = null)
        {
            var items = split.HasValue
                ? _examples.Where(e => e.Split == split.Value).ToList()
                : _examples;

            return format switch
            {
                DatasetFormat.Json => ExportJson(items),
                DatasetFormat.Jsonl => ExportJsonl(items),
                DatasetFormat.Csv => ExportCsv(items),
                DatasetFormat.ChatJsonl => ExportChatJsonl(items),
                DatasetFormat.Alpaca => ExportAlpaca(items),
                _ => throw new ArgumentException($"Unknown format: {format}")
            };
        }

        /// <summary>Generates a human-readable summary report.</summary>
        public string Report()
        {
            var stats = GetStats();
            var sb = new StringBuilder();
            sb.AppendLine("=== Dataset Report ===");
            sb.AppendLine($"Total examples: {stats.TotalExamples}");

            if (stats.TotalExamples == 0)
            {
                sb.AppendLine("(empty dataset)");
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine("Quality Distribution:");
            foreach (var kv in stats.QualityCounts.OrderBy(k => k.Key))
                sb.AppendLine($"  {kv.Key}: {kv.Value} ({100.0 * kv.Value / stats.TotalExamples:F1}%)");

            if (stats.SplitCounts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Split Distribution:");
                foreach (var kv in stats.SplitCounts.OrderBy(k => k.Key))
                    sb.AppendLine($"  {kv.Key}: {kv.Value}");
            }

            if (stats.TagCounts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Tags ({stats.TagCounts.Count} unique):");
                foreach (var kv in stats.TagCounts.OrderByDescending(k => k.Value).Take(10))
                    sb.AppendLine($"  {kv.Key}: {kv.Value}");
            }

            sb.AppendLine();
            sb.AppendLine("Length Statistics:");
            sb.AppendLine($"  Input:  avg={stats.AvgInputLength:F0} chars, min={stats.MinInputLength}, max={stats.MaxInputLength}");
            sb.AppendLine($"  Output: avg={stats.AvgOutputLength:F0} chars, min={stats.MinOutputLength}, max={stats.MaxOutputLength}");

            if (stats.TotalEstimatedTokens > 0)
                sb.AppendLine($"  Estimated tokens: {stats.TotalEstimatedTokens:N0}");

            if (stats.EmptyOutputCount > 0)
                sb.AppendLine($"  ⚠ Empty outputs: {stats.EmptyOutputCount}");

            if (stats.UniqueSourceCount > 0)
                sb.AppendLine($"  Sources: {stats.UniqueSourceCount} unique");

            return sb.ToString();
        }

        /// <summary>
        /// Imports examples from a JSONL string (one JSON object per line).
        /// Expected fields: "input" (required), "output", "system", "quality", "tags", "source", "id".
        /// </summary>
        public PromptDatasetBuilder ImportJsonl(string jsonlContent)
        {
            if (string.IsNullOrWhiteSpace(jsonlContent)) return this;

            var lines = jsonlContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int counter = _examples.Count;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                var input = root.TryGetProperty("input", out var inp) ? inp.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(input)) continue;

                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(id)) id = $"imp-{++counter:D5}";

                var output = root.TryGetProperty("output", out var outEl) ? outEl.GetString() ?? "" : "";
                var system = root.TryGetProperty("system", out var sysEl) ? sysEl.GetString() ?? "" : "";
                var source = root.TryGetProperty("source", out var srcEl) ? srcEl.GetString() ?? "" : "";

                var ex = new DatasetExample(id, input, output)
                {
                    SystemPrompt = system,
                    Source = source
                };

                if (root.TryGetProperty("quality", out var qEl) && Enum.TryParse<ExampleQuality>(qEl.GetString(), true, out var q))
                    ex.Quality = q;

                if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsEl.EnumerateArray())
                    {
                        var t = tag.GetString();
                        if (!string.IsNullOrEmpty(t)) ex.Tags.Add(t);
                    }
                }

                Add(ex);
            }

            return this;
        }

        /// <summary>
        /// Serializes the entire dataset state to JSON for persistence.
        /// </summary>
        public string Serialize()
        {
            var data = _examples.Select(e => new Dictionary<string, object?>
            {
                ["id"] = e.Id,
                ["input"] = e.Input,
                ["output"] = e.Output,
                ["system"] = e.SystemPrompt,
                ["quality"] = e.Quality.ToString(),
                ["tags"] = e.Tags,
                ["metadata"] = e.Metadata,
                ["source"] = e.Source,
                ["split"] = e.Split?.ToString(),
                ["estimatedTokens"] = e.EstimatedTokens
            }).ToList();

            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        // --- Private export methods ---

        private static string ExportJson(IEnumerable<DatasetExample> items)
        {
            var data = items.Select(e => new Dictionary<string, object>
            {
                ["id"] = e.Id,
                ["input"] = e.Input,
                ["output"] = e.Output,
                ["system_prompt"] = e.SystemPrompt,
                ["quality"] = e.Quality.ToString().ToLowerInvariant(),
                ["tags"] = e.Tags,
                ["source"] = e.Source
            }).ToList();

            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string ExportJsonl(IEnumerable<DatasetExample> items)
        {
            var sb = new StringBuilder();
            foreach (var e in items)
            {
                var obj = new Dictionary<string, object>
                {
                    ["input"] = e.Input,
                    ["output"] = e.Output
                };
                if (!string.IsNullOrEmpty(e.SystemPrompt)) obj["system"] = e.SystemPrompt;
                sb.AppendLine(JsonSerializer.Serialize(obj));
            }
            return sb.ToString().TrimEnd();
        }

        private static string ExportChatJsonl(IEnumerable<DatasetExample> items)
        {
            var sb = new StringBuilder();
            foreach (var e in items)
            {
                var messages = new List<Dictionary<string, string>>();
                if (!string.IsNullOrEmpty(e.SystemPrompt))
                    messages.Add(new Dictionary<string, string> { ["role"] = "system", ["content"] = e.SystemPrompt });
                messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = e.Input });
                if (!string.IsNullOrEmpty(e.Output))
                    messages.Add(new Dictionary<string, string> { ["role"] = "assistant", ["content"] = e.Output });

                sb.AppendLine(JsonSerializer.Serialize(new { messages }));
            }
            return sb.ToString().TrimEnd();
        }

        private static string ExportAlpaca(IEnumerable<DatasetExample> items)
        {
            var data = items.Select(e => new Dictionary<string, string>
            {
                ["instruction"] = !string.IsNullOrEmpty(e.SystemPrompt) ? e.SystemPrompt : e.Input,
                ["input"] = !string.IsNullOrEmpty(e.SystemPrompt) ? e.Input : "",
                ["output"] = e.Output
            }).ToList();

            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static string ExportCsv(IEnumerable<DatasetExample> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("id,input,output,system_prompt,quality,tags,source");
            foreach (var e in items)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(e.Id),
                    EscapeCsv(e.Input),
                    EscapeCsv(e.Output),
                    EscapeCsv(e.SystemPrompt),
                    EscapeCsv(e.Quality.ToString()),
                    EscapeCsv(string.Join(";", e.Tags)),
                    EscapeCsv(e.Source)
                ));
            }
            return sb.ToString().TrimEnd();
        }
    }
}
