namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a deprecation notice for a prompt template.
    /// </summary>
    public class DeprecationNotice
    {
        /// <summary>Gets the prompt identifier (name or key).</summary>
        public string PromptId { get; internal set; } = "";

        /// <summary>Gets the reason for deprecation.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Gets the replacement prompt identifier, if any.</summary>
        public string? ReplacementId { get; internal set; }

        /// <summary>Gets the date when the prompt was deprecated.</summary>
        public DateTimeOffset DeprecatedOn { get; internal set; }

        /// <summary>Gets the sunset date after which the prompt should no longer be used.</summary>
        public DateTimeOffset? SunsetDate { get; internal set; }

        /// <summary>Gets the severity level (Warning, Error, Info).</summary>
        public DeprecationSeverity Severity { get; internal set; } = DeprecationSeverity.Warning;

        /// <summary>Gets migration notes for moving to the replacement.</summary>
        public string MigrationNotes { get; internal set; } = "";

        /// <summary>Returns true if the sunset date has passed.</summary>
        public bool IsSunset => SunsetDate.HasValue && DateTimeOffset.UtcNow >= SunsetDate.Value;

        /// <summary>Returns days until sunset, or null if no sunset date set.</summary>
        public int? DaysUntilSunset => SunsetDate.HasValue
            ? (int)Math.Max(0, (SunsetDate.Value - DateTimeOffset.UtcNow).TotalDays)
            : null;
    }

    /// <summary>
    /// Severity levels for deprecation notices.
    /// </summary>
    public enum DeprecationSeverity
    {
        /// <summary>Informational — prompt still works but a better alternative exists.</summary>
        Info,
        /// <summary>Warning — prompt will be removed; migrate soon.</summary>
        Warning,
        /// <summary>Error — prompt is past sunset and should not be used.</summary>
        Error
    }

    /// <summary>
    /// Result of a deprecation audit across a collection of prompts.
    /// </summary>
    public class DeprecationAuditResult
    {
        /// <summary>Gets all active deprecation notices.</summary>
        public List<DeprecationNotice> Notices { get; internal set; } = new();

        /// <summary>Gets prompts that are past their sunset date.</summary>
        public List<DeprecationNotice> Sunsetted => Notices.Where(n => n.IsSunset).ToList();

        /// <summary>Gets prompts approaching sunset (within 30 days).</summary>
        public List<DeprecationNotice> Approaching => Notices
            .Where(n => n.DaysUntilSunset is > 0 and <= 30)
            .ToList();

        /// <summary>Gets prompts with no replacement defined.</summary>
        public List<DeprecationNotice> NoReplacement => Notices
            .Where(n => string.IsNullOrEmpty(n.ReplacementId))
            .ToList();

        /// <summary>Gets a summary string for reporting.</summary>
        public string Summary =>
            $"Deprecation Audit: {Notices.Count} deprecated prompt(s) — " +
            $"{Sunsetted.Count} sunsetted, {Approaching.Count} approaching sunset, " +
            $"{NoReplacement.Count} without replacement";
    }

    /// <summary>
    /// Manages prompt deprecation lifecycle — marking prompts as deprecated,
    /// tracking replacements, sunset dates, and running deprecation audits.
    /// </summary>
    public class PromptDeprecationManager
    {
        private readonly Dictionary<string, DeprecationNotice> _notices = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Mark a prompt as deprecated.
        /// </summary>
        /// <param name="promptId">The prompt identifier to deprecate.</param>
        /// <param name="reason">Why the prompt is being deprecated.</param>
        /// <param name="replacementId">Optional replacement prompt identifier.</param>
        /// <param name="sunsetDate">Optional date after which the prompt should not be used.</param>
        /// <param name="severity">Deprecation severity level.</param>
        /// <param name="migrationNotes">Optional notes on how to migrate.</param>
        /// <returns>The created deprecation notice.</returns>
        public DeprecationNotice Deprecate(
            string promptId,
            string reason,
            string? replacementId = null,
            DateTimeOffset? sunsetDate = null,
            DeprecationSeverity severity = DeprecationSeverity.Warning,
            string migrationNotes = "")
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("Prompt ID cannot be empty.", nameof(promptId));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Deprecation reason cannot be empty.", nameof(reason));

            var notice = new DeprecationNotice
            {
                PromptId = promptId,
                Reason = reason,
                ReplacementId = replacementId,
                DeprecatedOn = DateTimeOffset.UtcNow,
                SunsetDate = sunsetDate,
                Severity = severity,
                MigrationNotes = migrationNotes
            };

            _notices[promptId] = notice;
            return notice;
        }

        /// <summary>
        /// Check if a prompt is deprecated.
        /// </summary>
        public bool IsDeprecated(string promptId)
            => _notices.ContainsKey(promptId);

        /// <summary>
        /// Get the deprecation notice for a prompt, or null if not deprecated.
        /// </summary>
        public DeprecationNotice? GetNotice(string promptId)
            => _notices.TryGetValue(promptId, out var notice) ? notice : null;

        /// <summary>
        /// Remove a deprecation notice (un-deprecate a prompt).
        /// </summary>
        public bool Revoke(string promptId)
            => _notices.Remove(promptId);

        /// <summary>
        /// Run an audit across all tracked deprecations.
        /// </summary>
        public DeprecationAuditResult Audit()
            => new() { Notices = _notices.Values.ToList() };

        /// <summary>
        /// Check a list of prompt IDs and return any that are deprecated.
        /// Useful for scanning a codebase or config for deprecated prompt usage.
        /// </summary>
        /// <param name="promptIds">Prompt IDs to check.</param>
        /// <returns>Deprecation notices for any deprecated prompts found.</returns>
        public List<DeprecationNotice> Scan(IEnumerable<string> promptIds)
            => promptIds
                .Where(id => _notices.ContainsKey(id))
                .Select(id => _notices[id])
                .ToList();

        /// <summary>
        /// Get all deprecation notices, optionally filtered by severity.
        /// </summary>
        public List<DeprecationNotice> GetAll(DeprecationSeverity? severity = null)
            => severity.HasValue
                ? _notices.Values.Where(n => n.Severity == severity.Value).ToList()
                : _notices.Values.ToList();

        /// <summary>
        /// Export all deprecation notices as a formatted report string.
        /// </summary>
        public string ExportReport()
        {
            if (_notices.Count == 0)
                return "No deprecated prompts.";

            var lines = new List<string> { "# Prompt Deprecation Report", "" };

            foreach (var notice in _notices.Values.OrderBy(n => n.SunsetDate ?? DateTimeOffset.MaxValue))
            {
                var status = notice.IsSunset ? "🔴 SUNSET" :
                    notice.DaysUntilSunset <= 30 ? $"🟡 {notice.DaysUntilSunset}d remaining" :
                    "🟢 Active";

                lines.Add($"## {notice.PromptId} [{status}]");
                lines.Add($"- **Reason:** {notice.Reason}");
                lines.Add($"- **Severity:** {notice.Severity}");
                lines.Add($"- **Deprecated:** {notice.DeprecatedOn:yyyy-MM-dd}");

                if (notice.SunsetDate.HasValue)
                    lines.Add($"- **Sunset:** {notice.SunsetDate.Value:yyyy-MM-dd}");

                if (!string.IsNullOrEmpty(notice.ReplacementId))
                    lines.Add($"- **Replacement:** {notice.ReplacementId}");

                if (!string.IsNullOrEmpty(notice.MigrationNotes))
                    lines.Add($"- **Migration:** {notice.MigrationNotes}");

                lines.Add("");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
