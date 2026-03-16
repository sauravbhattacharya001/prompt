namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Lifecycle stages for prompt promotion.
    /// </summary>
    public enum PromptStage
    {
        /// <summary>Initial authoring stage.</summary>
        Draft,
        /// <summary>Under review / testing.</summary>
        Staging,
        /// <summary>Active in production.</summary>
        Production,
        /// <summary>Retired from use.</summary>
        Deprecated
    }

    /// <summary>
    /// Records a single promotion or rollback event.
    /// </summary>
    public class PromotionEvent
    {
        /// <summary>Stage before the transition.</summary>
        public PromptStage FromStage { get; set; }

        /// <summary>Stage after the transition.</summary>
        public PromptStage ToStage { get; set; }

        /// <summary>Who approved or performed the transition.</summary>
        public string Actor { get; set; } = "";

        /// <summary>Optional reason or notes.</summary>
        public string? Reason { get; set; }

        /// <summary>When the transition occurred.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>Whether this was a rollback.</summary>
        public bool IsRollback { get; set; }

        /// <summary>Snapshot of the prompt text at the time of promotion.</summary>
        public string? PromptSnapshot { get; set; }
    }

    /// <summary>
    /// A prompt registered in the promotion pipeline with its current stage,
    /// history, and metadata.
    /// </summary>
    public class ManagedPrompt
    {
        /// <summary>Unique identifier.</summary>
        public string Id { get; set; } = "";

        /// <summary>The prompt text content.</summary>
        public string Content { get; set; } = "";

        /// <summary>Current lifecycle stage.</summary>
        public PromptStage Stage { get; set; } = PromptStage.Draft;

        /// <summary>Version counter, incremented on each promotion.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Who created this prompt.</summary>
        public string Author { get; set; } = "";

        /// <summary>Optional tags for organization.</summary>
        public HashSet<string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>When this prompt was registered.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>When this prompt was last modified.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Promotion/rollback history.</summary>
        public List<PromotionEvent> History { get; set; } = new();

        /// <summary>Required approvers for promotion to production.</summary>
        public HashSet<string> RequiredApprovers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Approvers who have signed off on the current version.</summary>
        public HashSet<string> CurrentApprovals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Result of a promotion or rollback attempt.
    /// </summary>
    public class PromotionResult
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Descriptive message.</summary>
        public string Message { get; set; } = "";

        /// <summary>The new stage after the operation, if successful.</summary>
        public PromptStage? NewStage { get; set; }

        /// <summary>Reasons the operation was blocked, if any.</summary>
        public List<string> BlockReasons { get; set; } = new();
    }

    /// <summary>
    /// Summary report of all managed prompts.
    /// </summary>
    public class PromotionReport
    {
        /// <summary>Total prompts managed.</summary>
        public int TotalPrompts { get; set; }

        /// <summary>Count per stage.</summary>
        public Dictionary<PromptStage, int> ByStage { get; set; } = new();

        /// <summary>Recent promotion events across all prompts.</summary>
        public List<PromotionEvent> RecentEvents { get; set; } = new();

        /// <summary>Prompts awaiting approvals.</summary>
        public List<string> PendingApproval { get; set; } = new();

        /// <summary>Total promotion events recorded.</summary>
        public int TotalEvents { get; set; }
    }

    /// <summary>
    /// Manages the lifecycle of prompts through draft → staging → production → deprecated
    /// stages with approval gates, rollback, and full promotion history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Usage:</b>
    /// </para>
    /// <code>
    /// var mgr = new PromptPromotionManager();
    /// mgr.Register("greeting", "Hello {{name}}", "alice");
    /// mgr.AddApprover("greeting", "bob");
    /// mgr.Promote("greeting", "alice"); // draft → staging
    /// mgr.Approve("greeting", "bob");
    /// mgr.Promote("greeting", "alice"); // staging → production (bob approved)
    /// mgr.Rollback("greeting", "alice", "Bug found"); // back to staging
    /// </code>
    /// </remarks>
    public class PromptPromotionManager
    {
        private readonly Dictionary<string, ManagedPrompt> _prompts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the number of managed prompts.</summary>
        public int Count => _prompts.Count;

        /// <summary>
        /// Registers a new prompt in Draft stage.
        /// </summary>
        /// <param name="id">Unique prompt identifier. Must be non-empty, alphanumeric with hyphens/underscores.</param>
        /// <param name="content">The prompt text.</param>
        /// <param name="author">Who is creating this prompt.</param>
        /// <param name="tags">Optional tags.</param>
        /// <returns>The created <see cref="ManagedPrompt"/>.</returns>
        /// <exception cref="ArgumentException">If id, content, or author is invalid, or id already exists.</exception>
        public ManagedPrompt Register(string id, string content, string author, params string[] tags)
        {
            ValidateId(id);
            ValidateNonEmpty(content, nameof(content));
            ValidateNonEmpty(author, nameof(author));

            if (_prompts.ContainsKey(id))
                throw new ArgumentException($"Prompt '{id}' is already registered.", nameof(id));

            var now = DateTimeOffset.UtcNow;
            var prompt = new ManagedPrompt
            {
                Id = id.Trim(),
                Content = content,
                Author = author.Trim(),
                Stage = PromptStage.Draft,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
                Tags = new HashSet<string>(tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase)
            };

            _prompts[prompt.Id] = prompt;
            return prompt;
        }

        /// <summary>
        /// Gets a managed prompt by id.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <returns>The prompt, or null if not found.</returns>
        public ManagedPrompt? Get(string id) =>
            !string.IsNullOrWhiteSpace(id) && _prompts.TryGetValue(id.Trim(), out var p) ? p : null;

        /// <summary>
        /// Updates the content of a prompt. Only allowed in Draft or Staging.
        /// Clears any current approvals.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <param name="newContent">Updated prompt text.</param>
        /// <param name="actor">Who is making the update.</param>
        /// <returns>True if updated, false if not found or in wrong stage.</returns>
        /// <exception cref="ArgumentException">If newContent is empty.</exception>
        public bool UpdateContent(string id, string newContent, string actor)
        {
            ValidateNonEmpty(newContent, nameof(newContent));
            ValidateNonEmpty(actor, nameof(actor));

            var prompt = Get(id);
            if (prompt == null) return false;
            if (prompt.Stage == PromptStage.Production || prompt.Stage == PromptStage.Deprecated)
                return false;

            prompt.Content = newContent;
            prompt.UpdatedAt = DateTimeOffset.UtcNow;
            prompt.CurrentApprovals.Clear();
            return true;
        }

        /// <summary>
        /// Adds a required approver for promotion to production.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <param name="approver">The approver name.</param>
        /// <returns>True if added, false if prompt not found.</returns>
        public bool AddApprover(string id, string approver)
        {
            ValidateNonEmpty(approver, nameof(approver));
            var prompt = Get(id);
            if (prompt == null) return false;
            prompt.RequiredApprovers.Add(approver.Trim());
            return true;
        }

        /// <summary>
        /// Removes a required approver.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <param name="approver">The approver name.</param>
        /// <returns>True if removed.</returns>
        public bool RemoveApprover(string id, string approver)
        {
            var prompt = Get(id);
            if (prompt == null) return false;
            return prompt.RequiredApprovers.Remove(approver?.Trim() ?? "");
        }

        /// <summary>
        /// Records an approval from the given approver.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <param name="approver">The approver name.</param>
        /// <returns>True if the approval was recorded (approver is in required list and hasn't approved yet).</returns>
        public bool Approve(string id, string approver)
        {
            ValidateNonEmpty(approver, nameof(approver));
            var prompt = Get(id);
            if (prompt == null) return false;
            if (!prompt.RequiredApprovers.Contains(approver.Trim())) return false;
            return prompt.CurrentApprovals.Add(approver.Trim());
        }

        /// <summary>
        /// Checks whether all required approvals are met.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <returns>True if no required approvers, or all have approved.</returns>
        public bool IsFullyApproved(string id)
        {
            var prompt = Get(id);
            if (prompt == null) return false;
            if (prompt.RequiredApprovers.Count == 0) return true;
            return prompt.RequiredApprovers.All(a => prompt.CurrentApprovals.Contains(a));
        }

        /// <summary>
        /// Promotes a prompt to the next stage.
        /// Draft → Staging (always allowed),
        /// Staging → Production (requires all approvals),
        /// Production → Deprecated (always allowed).
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <param name="actor">Who is performing the promotion.</param>
        /// <param name="reason">Optional reason.</param>
        /// <returns>A <see cref="PromotionResult"/> describing the outcome.</returns>
        public PromotionResult Promote(string id, string actor, string? reason = null)
        {
            ValidateNonEmpty(actor, nameof(actor));
            var prompt = Get(id);
            if (prompt == null)
                return new PromotionResult { Success = false, Message = $"Prompt '{id}' not found." };

            if (prompt.Stage == PromptStage.Deprecated)
                return new PromotionResult { Success = false, Message = "Cannot promote a deprecated prompt." };

            var nextStage = prompt.Stage switch
            {
                PromptStage.Draft => PromptStage.Staging,
                PromptStage.Staging => PromptStage.Production,
                PromptStage.Production => PromptStage.Deprecated,
                _ => throw new InvalidOperationException("Unknown stage.")
            };

            // Check approval gate for staging → production
            if (prompt.Stage == PromptStage.Staging && !IsFullyApproved(id))
            {
                var missing = prompt.RequiredApprovers
                    .Where(a => !prompt.CurrentApprovals.Contains(a))
                    .ToList();

                return new PromotionResult
                {
                    Success = false,
                    Message = "Missing required approvals.",
                    BlockReasons = missing.Select(a => $"Awaiting approval from: {a}").ToList()
                };
            }

            var evt = new PromotionEvent
            {
                FromStage = prompt.Stage,
                ToStage = nextStage,
                Actor = actor.Trim(),
                Reason = reason,
                Timestamp = DateTimeOffset.UtcNow,
                IsRollback = false,
                PromptSnapshot = prompt.Content
            };

            prompt.Stage = nextStage;
            prompt.Version++;
            prompt.UpdatedAt = DateTimeOffset.UtcNow;
            prompt.History.Add(evt);

            if (nextStage == PromptStage.Production)
                prompt.CurrentApprovals.Clear();

            return new PromotionResult
            {
                Success = true,
                Message = $"Promoted '{id}' from {evt.FromStage} to {nextStage}.",
                NewStage = nextStage
            };
        }

        /// <summary>
        /// Rolls back a prompt one stage (Production → Staging, Staging → Draft).
        /// Cannot roll back from Draft or Deprecated.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <param name="actor">Who is performing the rollback.</param>
        /// <param name="reason">Optional reason for the rollback.</param>
        /// <returns>A <see cref="PromotionResult"/> describing the outcome.</returns>
        public PromotionResult Rollback(string id, string actor, string? reason = null)
        {
            ValidateNonEmpty(actor, nameof(actor));
            var prompt = Get(id);
            if (prompt == null)
                return new PromotionResult { Success = false, Message = $"Prompt '{id}' not found." };

            if (prompt.Stage == PromptStage.Draft)
                return new PromotionResult { Success = false, Message = "Cannot roll back from Draft." };

            if (prompt.Stage == PromptStage.Deprecated)
                return new PromotionResult { Success = false, Message = "Cannot roll back from Deprecated." };

            var prevStage = prompt.Stage switch
            {
                PromptStage.Staging => PromptStage.Draft,
                PromptStage.Production => PromptStage.Staging,
                _ => throw new InvalidOperationException("Unknown stage.")
            };

            var evt = new PromotionEvent
            {
                FromStage = prompt.Stage,
                ToStage = prevStage,
                Actor = actor.Trim(),
                Reason = reason,
                Timestamp = DateTimeOffset.UtcNow,
                IsRollback = true,
                PromptSnapshot = prompt.Content
            };

            prompt.Stage = prevStage;
            prompt.Version++;
            prompt.UpdatedAt = DateTimeOffset.UtcNow;
            prompt.History.Add(evt);
            prompt.CurrentApprovals.Clear();

            return new PromotionResult
            {
                Success = true,
                Message = $"Rolled back '{id}' from {evt.FromStage} to {prevStage}.",
                NewStage = prevStage
            };
        }

        /// <summary>
        /// Restores content from a previous promotion snapshot.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <param name="historyIndex">Zero-based index into the prompt's history.</param>
        /// <param name="actor">Who is performing the restore.</param>
        /// <returns>True if restored, false if not found or index out of range.</returns>
        public bool RestoreFromHistory(string id, int historyIndex, string actor)
        {
            ValidateNonEmpty(actor, nameof(actor));
            var prompt = Get(id);
            if (prompt == null) return false;
            if (historyIndex < 0 || historyIndex >= prompt.History.Count) return false;
            var snapshot = prompt.History[historyIndex].PromptSnapshot;
            if (string.IsNullOrEmpty(snapshot)) return false;

            prompt.Content = snapshot;
            prompt.UpdatedAt = DateTimeOffset.UtcNow;
            prompt.CurrentApprovals.Clear();
            return true;
        }

        /// <summary>
        /// Lists all prompts, optionally filtered by stage and/or tag.
        /// </summary>
        /// <param name="stage">Optional stage filter.</param>
        /// <param name="tag">Optional tag filter.</param>
        /// <returns>Matching prompt ids sorted alphabetically.</returns>
        public IReadOnlyList<string> List(PromptStage? stage = null, string? tag = null)
        {
            var q = _prompts.Values.AsEnumerable();
            if (stage.HasValue)
                q = q.Where(p => p.Stage == stage.Value);
            if (!string.IsNullOrWhiteSpace(tag))
                q = q.Where(p => p.Tags.Contains(tag!.Trim()));
            return q.Select(p => p.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Removes a prompt entirely. Only allowed in Draft or Deprecated stage.
        /// </summary>
        /// <param name="id">The prompt identifier.</param>
        /// <returns>True if removed.</returns>
        public bool Remove(string id)
        {
            var prompt = Get(id);
            if (prompt == null) return false;
            if (prompt.Stage == PromptStage.Staging || prompt.Stage == PromptStage.Production)
                return false;
            return _prompts.Remove(prompt.Id);
        }

        /// <summary>
        /// Generates a summary report of all managed prompts.
        /// </summary>
        /// <param name="recentCount">Number of recent events to include.</param>
        /// <returns>A <see cref="PromotionReport"/>.</returns>
        public PromotionReport GenerateReport(int recentCount = 10)
        {
            var allEvents = _prompts.Values
                .SelectMany(p => p.History)
                .OrderByDescending(e => e.Timestamp)
                .ToList();

            var pending = _prompts.Values
                .Where(p => p.Stage == PromptStage.Staging && p.RequiredApprovers.Count > 0 && !IsFullyApproved(p.Id))
                .Select(p => p.Id)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var byStage = new Dictionary<PromptStage, int>();
            foreach (PromptStage s in Enum.GetValues(typeof(PromptStage)))
                byStage[s] = _prompts.Values.Count(p => p.Stage == s);

            return new PromotionReport
            {
                TotalPrompts = _prompts.Count,
                ByStage = byStage,
                RecentEvents = allEvents.Take(recentCount).ToList(),
                PendingApproval = pending,
                TotalEvents = allEvents.Count
            };
        }

        /// <summary>
        /// Exports all managed prompts to JSON.
        /// </summary>
        /// <returns>JSON string.</returns>
        public string ExportJson()
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            return JsonSerializer.Serialize(_prompts.Values.OrderBy(p => p.Id).ToList(), opts);
        }

        /// <summary>
        /// Generates a text summary of the promotion pipeline.
        /// </summary>
        /// <returns>Multi-line text report.</returns>
        public string ExportText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Prompt Promotion Pipeline ===");
            sb.AppendLine();

            foreach (PromptStage stage in new[] { PromptStage.Draft, PromptStage.Staging, PromptStage.Production, PromptStage.Deprecated })
            {
                var prompts = _prompts.Values.Where(p => p.Stage == stage).OrderBy(p => p.Id).ToList();
                sb.AppendLine($"[{stage}] ({prompts.Count})");
                foreach (var p in prompts)
                {
                    sb.AppendLine($"  {p.Id} v{p.Version} by {p.Author}");
                    if (stage == PromptStage.Staging && p.RequiredApprovers.Count > 0)
                    {
                        var approved = p.CurrentApprovals.Count;
                        var total = p.RequiredApprovers.Count;
                        sb.AppendLine($"    Approvals: {approved}/{total}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id cannot be null or empty.", nameof(id));
            if (!System.Text.RegularExpressions.Regex.IsMatch(id.Trim(), @"^[a-zA-Z0-9_-]+$"))
                throw new ArgumentException("Id must contain only letters, digits, hyphens, and underscores.", nameof(id));
        }

        private static void ValidateNonEmpty(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{paramName} cannot be null or empty.", paramName);
        }
    }
}
