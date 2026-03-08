using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Prompt;

/// <summary>
/// Severity level for an audit log entry.
/// </summary>
public enum AuditSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Immutable record of a single prompt execution for compliance and debugging.
/// </summary>
public sealed class AuditEntry
{
    public string Id { get; }
    public DateTimeOffset Timestamp { get; }
    public string PromptId { get; }
    public string? UserId { get; }
    public string? Model { get; }
    public string? Provider { get; }
    public AuditSeverity Severity { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public int InputTokens { get; }
    public int OutputTokens { get; }
    public double? CostUsd { get; }
    public TimeSpan Duration { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>SHA-256 hash chain link — hash of previous entry + this entry's content.</summary>
    public string Hash { get; internal set; } = "";

    /// <summary>Hash of the previous entry (empty string for genesis entry).</summary>
    public string PreviousHash { get; internal set; } = "";

    public AuditEntry(
        string promptId,
        bool success,
        string? userId = null,
        string? model = null,
        string? provider = null,
        AuditSeverity severity = AuditSeverity.Info,
        string? errorMessage = null,
        int inputTokens = 0,
        int outputTokens = 0,
        double? costUsd = null,
        TimeSpan? duration = null,
        IDictionary<string, string>? metadata = null)
    {
        Id = Guid.NewGuid().ToString("N");
        Timestamp = DateTimeOffset.UtcNow;
        PromptId = promptId ?? throw new ArgumentNullException(nameof(promptId));
        UserId = userId;
        Model = model;
        Provider = provider;
        Severity = severity;
        Success = success;
        ErrorMessage = errorMessage;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
        Duration = duration ?? TimeSpan.Zero;
        Metadata = metadata != null
            ? new Dictionary<string, string>(metadata)
            : new Dictionary<string, string>();
    }

    internal string ComputeContentHash()
    {
        var content = $"{Id}|{Timestamp:O}|{PromptId}|{UserId}|{Model}|{Success}|{InputTokens}|{OutputTokens}|{CostUsd}|{Duration.TotalMilliseconds}|{PreviousHash}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Configuration for <see cref="PromptAuditLog"/>.
/// </summary>
public sealed class AuditLogConfig
{
    /// <summary>Maximum entries to retain. Oldest entries beyond this are purged. Default 100,000.</summary>
    public int MaxEntries { get; init; } = 100_000;

    /// <summary>Retention period. Entries older than this are eligible for purge. Default 90 days.</summary>
    public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(90);

    /// <summary>Enable hash chain for tamper detection. Default true.</summary>
    public bool EnableHashChain { get; init; } = true;

    /// <summary>Auto-purge expired entries when new entries are appended. Default true.</summary>
    public bool AutoPurge { get; init; } = true;
}

/// <summary>
/// Time-bounded summary of audit log entries.
/// </summary>
public sealed class AuditSummary
{
    public int TotalEntries { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public double TotalCostUsd { get; init; }
    public double AverageDurationMs { get; init; }
    public double SuccessRate { get; init; }
    public IReadOnlyDictionary<string, int> ByModel { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ByUser { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ByPrompt { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<AuditSeverity, int> BySeverity { get; init; } = new Dictionary<AuditSeverity, int>();
    public DateTimeOffset? EarliestEntry { get; init; }
    public DateTimeOffset? LatestEntry { get; init; }
}

/// <summary>
/// Result of a hash chain integrity verification.
/// </summary>
public sealed class IntegrityCheckResult
{
    public bool IsValid { get; init; }
    public int EntriesChecked { get; init; }
    public int CorruptEntries { get; init; }
    public List<string> CorruptEntryIds { get; init; } = new();
    public string? FirstCorruptId { get; init; }
}

/// <summary>
/// Immutable, hash-chained audit log for prompt executions.
///
/// Records who ran which prompt, when, with what results, at what cost.
/// Supports querying, filtering, JSON export, retention policies, and
/// tamper detection via SHA-256 hash chains.
///
/// Different from <see cref="PromptAnalytics"/> (aggregate statistics)
/// and <see cref="PromptHistory"/> (prompt text evolution) — this is a
/// per-execution immutable log for compliance and debugging.
/// </summary>
public sealed class PromptAuditLog
{
    private readonly List<AuditEntry> _entries = new();
    private readonly AuditLogConfig _config;
    private readonly object _lock = new();
    private string _lastHash = "";

    public PromptAuditLog(AuditLogConfig? config = null)
    {
        _config = config ?? new AuditLogConfig();
    }

    /// <summary>Total entries currently in the log.</summary>
    public int Count
    {
        get { lock (_lock) { return _entries.Count; } }
    }

    /// <summary>
    /// Append a new entry to the audit log.
    /// Computes hash chain link and applies retention policy if configured.
    /// </summary>
    public AuditEntry Append(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            // Hash chain
            if (_config.EnableHashChain)
            {
                entry.PreviousHash = _lastHash;
                entry.Hash = entry.ComputeContentHash();
                _lastHash = entry.Hash;
            }

            _entries.Add(entry);

            // Auto-purge if enabled
            if (_config.AutoPurge)
            {
                PurgeExpired();
                while (_entries.Count > _config.MaxEntries)
                {
                    _entries.RemoveAt(0);
                }
            }
        }

        return entry;
    }

    /// <summary>
    /// Query entries with optional filters.
    /// </summary>
    public List<AuditEntry> Query(
        string? promptId = null,
        string? userId = null,
        string? model = null,
        bool? success = null,
        AuditSeverity? minSeverity = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int? limit = null,
        bool descending = true)
    {
        lock (_lock)
        {
            IEnumerable<AuditEntry> query = _entries;

            if (promptId != null)
                query = query.Where(e => e.PromptId == promptId);
            if (userId != null)
                query = query.Where(e => e.UserId == userId);
            if (model != null)
                query = query.Where(e => e.Model == model);
            if (success != null)
                query = query.Where(e => e.Success == success.Value);
            if (minSeverity != null)
                query = query.Where(e => e.Severity >= minSeverity.Value);
            if (from != null)
                query = query.Where(e => e.Timestamp >= from.Value);
            if (to != null)
                query = query.Where(e => e.Timestamp <= to.Value);

            if (descending)
                query = query.OrderByDescending(e => e.Timestamp);
            else
                query = query.OrderBy(e => e.Timestamp);

            if (limit != null && limit.Value > 0)
                query = query.Take(limit.Value);

            return query.ToList();
        }
    }

    /// <summary>
    /// Get an entry by its unique ID.
    /// </summary>
    public AuditEntry? GetById(string id)
    {
        lock (_lock)
        {
            return _entries.FirstOrDefault(e => e.Id == id);
        }
    }

    /// <summary>
    /// Generate a summary of entries within the specified time range.
    /// </summary>
    public AuditSummary Summarize(DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        lock (_lock)
        {
            var entries = _entries.AsEnumerable();
            if (from != null) entries = entries.Where(e => e.Timestamp >= from.Value);
            if (to != null) entries = entries.Where(e => e.Timestamp <= to.Value);

            var list = entries.ToList();
            if (list.Count == 0)
            {
                return new AuditSummary { TotalEntries = 0 };
            }

            var byModel = new Dictionary<string, int>();
            var byUser = new Dictionary<string, int>();
            var byPrompt = new Dictionary<string, int>();
            var bySeverity = new Dictionary<AuditSeverity, int>();
            int successes = 0, failures = 0;
            int totalIn = 0, totalOut = 0;
            double totalCost = 0;
            double totalDurationMs = 0;

            foreach (var e in list)
            {
                if (e.Success) successes++; else failures++;
                totalIn += e.InputTokens;
                totalOut += e.OutputTokens;
                totalCost += e.CostUsd ?? 0;
                totalDurationMs += e.Duration.TotalMilliseconds;

                var modelKey = e.Model ?? "(none)";
                byModel[modelKey] = byModel.GetValueOrDefault(modelKey) + 1;

                var userKey = e.UserId ?? "(anonymous)";
                byUser[userKey] = byUser.GetValueOrDefault(userKey) + 1;

                byPrompt[e.PromptId] = byPrompt.GetValueOrDefault(e.PromptId) + 1;
                bySeverity[e.Severity] = bySeverity.GetValueOrDefault(e.Severity) + 1;
            }

            return new AuditSummary
            {
                TotalEntries = list.Count,
                SuccessCount = successes,
                FailureCount = failures,
                TotalInputTokens = totalIn,
                TotalOutputTokens = totalOut,
                TotalCostUsd = Math.Round(totalCost, 6),
                AverageDurationMs = Math.Round(totalDurationMs / list.Count, 2),
                SuccessRate = Math.Round((double)successes / list.Count * 100, 2),
                ByModel = byModel,
                ByUser = byUser,
                ByPrompt = byPrompt,
                BySeverity = bySeverity,
                EarliestEntry = list.Min(e => e.Timestamp),
                LatestEntry = list.Max(e => e.Timestamp)
            };
        }
    }

    /// <summary>
    /// Verify the integrity of the hash chain.
    /// Returns which entries (if any) have been tampered with.
    /// </summary>
    public IntegrityCheckResult VerifyIntegrity()
    {
        lock (_lock)
        {
            if (!_config.EnableHashChain)
            {
                return new IntegrityCheckResult
                {
                    IsValid = true,
                    EntriesChecked = 0
                };
            }

            var corruptIds = new List<string>();
            var prevHash = "";

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                // Check previous hash link
                if (entry.PreviousHash != prevHash)
                {
                    corruptIds.Add(entry.Id);
                    prevHash = entry.Hash;
                    continue;
                }

                // Recompute and verify content hash
                var expectedHash = entry.ComputeContentHash();
                if (entry.Hash != expectedHash)
                {
                    corruptIds.Add(entry.Id);
                }

                prevHash = entry.Hash;
            }

            return new IntegrityCheckResult
            {
                IsValid = corruptIds.Count == 0,
                EntriesChecked = _entries.Count,
                CorruptEntries = corruptIds.Count,
                CorruptEntryIds = corruptIds,
                FirstCorruptId = corruptIds.FirstOrDefault()
            };
        }
    }

    /// <summary>
    /// Remove entries older than the retention period.
    /// </summary>
    public int PurgeExpired()
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow - _config.RetentionPeriod;
            var before = _entries.Count;
            _entries.RemoveAll(e => e.Timestamp < cutoff);
            return before - _entries.Count;
        }
    }

    /// <summary>
    /// Export all entries as a JSON string.
    /// </summary>
    public string ExportJson(bool indented = false)
    {
        lock (_lock)
        {
            var records = _entries.Select(e => new Dictionary<string, object?>
            {
                ["id"] = e.Id,
                ["timestamp"] = e.Timestamp.ToString("O"),
                ["promptId"] = e.PromptId,
                ["userId"] = e.UserId,
                ["model"] = e.Model,
                ["provider"] = e.Provider,
                ["severity"] = e.Severity.ToString(),
                ["success"] = e.Success,
                ["errorMessage"] = e.ErrorMessage,
                ["inputTokens"] = e.InputTokens,
                ["outputTokens"] = e.OutputTokens,
                ["costUsd"] = e.CostUsd,
                ["durationMs"] = e.Duration.TotalMilliseconds,
                ["hash"] = e.Hash,
                ["previousHash"] = e.PreviousHash,
                ["metadata"] = e.Metadata.Count > 0 ? e.Metadata : null
            }).ToList();

            return JsonSerializer.Serialize(records, new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    /// <summary>
    /// Export entries as a CSV string.
    /// </summary>
    public string ExportCsv()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Timestamp,PromptId,UserId,Model,Provider,Severity,Success,ErrorMessage,InputTokens,OutputTokens,CostUsd,DurationMs,Hash");

            foreach (var e in _entries)
            {
                sb.Append(e.Id).Append(',');
                sb.Append(e.Timestamp.ToString("O")).Append(',');
                sb.Append(CsvEscape(e.PromptId)).Append(',');
                sb.Append(CsvEscape(e.UserId ?? "")).Append(',');
                sb.Append(CsvEscape(e.Model ?? "")).Append(',');
                sb.Append(CsvEscape(e.Provider ?? "")).Append(',');
                sb.Append(e.Severity).Append(',');
                sb.Append(e.Success).Append(',');
                sb.Append(CsvEscape(e.ErrorMessage ?? "")).Append(',');
                sb.Append(e.InputTokens).Append(',');
                sb.Append(e.OutputTokens).Append(',');
                sb.Append(e.CostUsd?.ToString() ?? "").Append(',');
                sb.Append(e.Duration.TotalMilliseconds).Append(',');
                sb.AppendLine(e.Hash);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Clear all entries and reset the hash chain.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _lastHash = "";
        }
    }

    /// <summary>
    /// Get the most recent N entries.
    /// </summary>
    public List<AuditEntry> Recent(int count = 10)
    {
        lock (_lock)
        {
            return _entries
                .Skip(Math.Max(0, _entries.Count - count))
                .Reverse()
                .ToList();
        }
    }

    /// <summary>
    /// Get all distinct prompt IDs that have been logged.
    /// </summary>
    public List<string> GetLoggedPromptIds()
    {
        lock (_lock)
        {
            return _entries.Select(e => e.PromptId).Distinct().ToList();
        }
    }

    /// <summary>
    /// Get all distinct user IDs that have been logged.
    /// </summary>
    public List<string> GetLoggedUserIds()
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.UserId != null)
                .Select(e => e.UserId!)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>
    /// Get the error rate for a specific prompt over recent entries.
    /// </summary>
    public double GetErrorRate(string promptId, int recentCount = 100)
    {
        lock (_lock)
        {
            var relevant = _entries
                .Where(e => e.PromptId == promptId)
                .TakeLast(recentCount)
                .ToList();

            if (relevant.Count == 0) return 0;
            return Math.Round((double)relevant.Count(e => !e.Success) / relevant.Count * 100, 2);
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
