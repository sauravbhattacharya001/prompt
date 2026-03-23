namespace Prompt
{
    using System.Text.Json;

    /// <summary>
    /// A single recorded prompt execution with token counts and cost.
    /// </summary>
    public record UsageEntry(
        string Id,
        DateTime Timestamp,
        string ModelId,
        string Provider,
        int InputTokens,
        int OutputTokens,
        decimal Cost,
        string? Tag,
        string? PromptName
    );

    /// <summary>
    /// Aggregated usage statistics for a grouping key (model, provider, tag, etc.).
    /// </summary>
    public record UsageSummary(
        string Key,
        int TotalCalls,
        long TotalInputTokens,
        long TotalOutputTokens,
        decimal TotalCost,
        decimal AverageCostPerCall,
        int AverageInputTokens,
        int AverageOutputTokens,
        DateTime FirstUsed,
        DateTime LastUsed
    );

    /// <summary>
    /// Daily usage breakdown for time-series analysis.
    /// </summary>
    public record DailyUsage(
        DateOnly Date,
        int Calls,
        long InputTokens,
        long OutputTokens,
        decimal Cost
    );

    /// <summary>
    /// Budget alert when spending exceeds a threshold.
    /// </summary>
    public record BudgetAlert(
        string Name,
        decimal Threshold,
        decimal CurrentSpend,
        decimal PercentUsed,
        bool Exceeded
    );

    /// <summary>
    /// Tracks cumulative prompt usage across executions and provides
    /// spending dashboards, budget alerts, and usage analytics.
    /// Complements <see cref="PromptCostEstimator"/> (pre-execution estimates)
    /// with post-execution tracking of actual costs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var dashboard = new PromptUsageDashboard();
    /// dashboard.SetBudget("monthly", 50.00m);
    ///
    /// // Record actual usage after each API call
    /// dashboard.Record("gpt-4o", "OpenAI", 1500, 800, 0.0118m, tag: "summarize");
    /// dashboard.Record("claude-3-5-haiku", "Anthropic", 2000, 500, 0.0036m, tag: "classify");
    ///
    /// // Get summaries
    /// Console.WriteLine(dashboard.SummaryByModel().ToText());
    /// Console.WriteLine(dashboard.DailyBreakdown().ToText());
    ///
    /// // Check budget
    /// var alert = dashboard.CheckBudget("monthly");
    /// if (alert.Exceeded) Console.WriteLine("⚠ Budget exceeded!");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptUsageDashboard
    {
        /// <summary>Maximum entries retained in memory.</summary>
        public const int MaxEntries = 100_000;

        private readonly List<UsageEntry> _entries = new();
        private readonly Dictionary<string, decimal> _budgets = new(StringComparer.OrdinalIgnoreCase);
        private int _nextId = 1;

        /// <summary>Total number of recorded entries.</summary>
        public int EntryCount => _entries.Count;

        /// <summary>Total cost across all recorded entries.</summary>
        public decimal TotalCost => _entries.Sum(e => e.Cost);

        /// <summary>Total input tokens across all entries.</summary>
        public long TotalInputTokens => _entries.Sum(e => (long)e.InputTokens);

        /// <summary>Total output tokens across all entries.</summary>
        public long TotalOutputTokens => _entries.Sum(e => (long)e.OutputTokens);

        /// <summary>
        /// Record a prompt execution.
        /// </summary>
        /// <param name="modelId">Model identifier (e.g. "gpt-4o").</param>
        /// <param name="provider">Provider name (e.g. "OpenAI").</param>
        /// <param name="inputTokens">Actual input token count.</param>
        /// <param name="outputTokens">Actual output token count.</param>
        /// <param name="cost">Actual cost in USD.</param>
        /// <param name="tag">Optional tag for categorizing usage (e.g. "summarize", "classify").</param>
        /// <param name="promptName">Optional prompt template name.</param>
        /// <param name="timestamp">Optional timestamp; defaults to UtcNow.</param>
        /// <returns>The ID of the recorded entry.</returns>
        public string Record(
            string modelId,
            string provider,
            int inputTokens,
            int outputTokens,
            decimal cost,
            string? tag = null,
            string? promptName = null,
            DateTime? timestamp = null)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("ModelId cannot be empty.");
            if (string.IsNullOrWhiteSpace(provider))
                throw new ArgumentException("Provider cannot be empty.");
            if (inputTokens < 0)
                throw new ArgumentException("Input tokens cannot be negative.");
            if (outputTokens < 0)
                throw new ArgumentException("Output tokens cannot be negative.");
            if (cost < 0)
                throw new ArgumentException("Cost cannot be negative.");

            if (_entries.Count >= MaxEntries)
                _entries.RemoveAt(0); // FIFO eviction

            var id = $"usage-{_nextId++}";
            _entries.Add(new UsageEntry(
                Id: id,
                Timestamp: timestamp ?? DateTime.UtcNow,
                ModelId: modelId,
                Provider: provider,
                InputTokens: inputTokens,
                OutputTokens: outputTokens,
                Cost: cost,
                Tag: tag,
                PromptName: promptName
            ));

            return id;
        }

        /// <summary>
        /// Record usage by auto-calculating cost from a <see cref="ModelPricing"/>.
        /// </summary>
        public string Record(
            ModelPricing pricing,
            int inputTokens,
            int outputTokens,
            string? tag = null,
            string? promptName = null,
            DateTime? timestamp = null)
        {
            ArgumentNullException.ThrowIfNull(pricing);
            var cost = pricing.TotalCost(inputTokens, outputTokens);
            return Record(pricing.ModelId, pricing.Provider, inputTokens, outputTokens, cost, tag, promptName, timestamp);
        }

        /// <summary>
        /// Set a named budget threshold in USD.
        /// </summary>
        public void SetBudget(string name, decimal amount)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Budget name cannot be empty.");
            if (amount <= 0)
                throw new ArgumentException("Budget amount must be positive.");
            _budgets[name] = amount;
        }

        /// <summary>
        /// Remove a named budget.
        /// </summary>
        public bool RemoveBudget(string name) => _budgets.Remove(name);

        /// <summary>
        /// Check spending against a named budget. Uses all entries by default,
        /// or entries from a specific time range.
        /// </summary>
        public BudgetAlert CheckBudget(string name, DateTime? since = null)
        {
            if (!_budgets.TryGetValue(name, out var threshold))
                throw new ArgumentException($"Unknown budget: {name}");

            var entries = since.HasValue
                ? _entries.Where(e => e.Timestamp >= since.Value)
                : _entries;

            var spent = entries.Sum(e => e.Cost);
            var percent = threshold > 0 ? spent / threshold * 100 : 0;

            return new BudgetAlert(
                Name: name,
                Threshold: threshold,
                CurrentSpend: spent,
                PercentUsed: percent,
                Exceeded: spent >= threshold
            );
        }

        /// <summary>
        /// Check all budgets and return alerts for those exceeding a percentage threshold.
        /// </summary>
        public List<BudgetAlert> CheckAllBudgets(decimal alertAtPercent = 80m, DateTime? since = null)
        {
            return _budgets.Keys
                .Select(name => CheckBudget(name, since))
                .Where(a => a.PercentUsed >= alertAtPercent)
                .OrderByDescending(a => a.PercentUsed)
                .ToList();
        }

        /// <summary>
        /// Get usage summary grouped by model.
        /// </summary>
        public List<UsageSummary> SummaryByModel(DateTime? since = null, DateTime? until = null)
        {
            return Summarize(e => e.ModelId, since, until);
        }

        /// <summary>
        /// Get usage summary grouped by provider.
        /// </summary>
        public List<UsageSummary> SummaryByProvider(DateTime? since = null, DateTime? until = null)
        {
            return Summarize(e => e.Provider, since, until);
        }

        /// <summary>
        /// Get usage summary grouped by tag.
        /// </summary>
        public List<UsageSummary> SummaryByTag(DateTime? since = null, DateTime? until = null)
        {
            return Summarize(e => e.Tag ?? "(untagged)", since, until);
        }

        /// <summary>
        /// Get usage summary grouped by prompt name.
        /// </summary>
        public List<UsageSummary> SummaryByPromptName(DateTime? since = null, DateTime? until = null)
        {
            return Summarize(e => e.PromptName ?? "(unnamed)", since, until);
        }

        /// <summary>
        /// Get daily usage breakdown for time-series visualization.
        /// </summary>
        public List<DailyUsage> DailyBreakdown(DateTime? since = null, DateTime? until = null)
        {
            var filtered = FilterEntries(since, until);
            return filtered
                .GroupBy(e => DateOnly.FromDateTime(e.Timestamp))
                .OrderBy(g => g.Key)
                .Select(g => new DailyUsage(
                    Date: g.Key,
                    Calls: g.Count(),
                    InputTokens: g.Sum(e => (long)e.InputTokens),
                    OutputTokens: g.Sum(e => (long)e.OutputTokens),
                    Cost: g.Sum(e => e.Cost)
                ))
                .ToList();
        }

        /// <summary>
        /// Get the top N most expensive entries.
        /// </summary>
        public List<UsageEntry> TopCostliestCalls(int count = 10) =>
            _entries.OrderByDescending(e => e.Cost).Take(count).ToList();

        /// <summary>
        /// Get the top N models by total spending.
        /// </summary>
        public List<UsageSummary> TopModelsBySpend(int count = 5) =>
            SummaryByModel().Take(count).ToList();

        /// <summary>
        /// Clear all recorded entries.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _nextId = 1;
        }

        /// <summary>
        /// Export all entries as JSON.
        /// </summary>
        public string ExportJson(bool indented = true)
        {
            var data = new
            {
                exportedAt = DateTime.UtcNow.ToString("o"),
                totalEntries = _entries.Count,
                totalCost = TotalCost,
                entries = _entries.Select(e => new
                {
                    id = e.Id,
                    timestamp = e.Timestamp.ToString("o"),
                    modelId = e.ModelId,
                    provider = e.Provider,
                    inputTokens = e.InputTokens,
                    outputTokens = e.OutputTokens,
                    cost = e.Cost,
                    tag = e.Tag,
                    promptName = e.PromptName
                }).ToList()
            };

            return JsonSerializer.Serialize(data, SerializationGuards.WriteOptions(indented));
        }

        /// <summary>
        /// Import entries from a JSON string previously exported via <see cref="ExportJson"/>.
        /// </summary>
        public int ImportJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("entries", out var entriesElem))
                throw new ArgumentException("Invalid export format: missing 'entries' array.");

            int imported = 0;
            foreach (var entry in entriesElem.EnumerateArray())
            {
                var modelId = entry.GetProperty("modelId").GetString() ?? "";
                var provider = entry.GetProperty("provider").GetString() ?? "";
                var inputTokens = entry.GetProperty("inputTokens").GetInt32();
                var outputTokens = entry.GetProperty("outputTokens").GetInt32();
                var cost = entry.GetProperty("cost").GetDecimal();
                var tag = entry.TryGetProperty("tag", out var tagElem) ? tagElem.GetString() : null;
                var promptName = entry.TryGetProperty("promptName", out var pnElem) ? pnElem.GetString() : null;
                var timestamp = entry.TryGetProperty("timestamp", out var tsElem)
                    ? DateTime.Parse(tsElem.GetString()!)
                    : DateTime.UtcNow;

                Record(modelId, provider, inputTokens, outputTokens, cost, tag, promptName, timestamp);
                imported++;
            }

            return imported;
        }

        /// <summary>
        /// Format a full dashboard as human-readable text.
        /// </summary>
        public string ToText(DateTime? since = null, DateTime? until = null)
        {
            var lines = new List<string>();
            lines.Add("═══════════════════════════════════════════════════════════════");
            lines.Add("  Prompt Usage Dashboard");
            lines.Add("═══════════════════════════════════════════════════════════════");
            lines.Add($"  Total calls:         {EntryCount:N0}");
            lines.Add($"  Total input tokens:  {TotalInputTokens:N0}");
            lines.Add($"  Total output tokens: {TotalOutputTokens:N0}");
            lines.Add($"  Total cost:          ${TotalCost:F4}");
            lines.Add("");

            // By provider
            var byProvider = SummaryByProvider(since, until);
            if (byProvider.Count > 0)
            {
                lines.Add("  ── By Provider ──────────────────────────────────────────────");
                lines.Add($"  {"Provider",-16} {"Calls",8} {"Input Tok",12} {"Output Tok",12} {"Cost",12}");
                lines.Add($"  {"────────",-16} {"─────",8} {"─────────",12} {"──────────",12} {"────",12}");
                foreach (var s in byProvider)
                {
                    lines.Add($"  {s.Key,-16} {s.TotalCalls,8:N0} {s.TotalInputTokens,12:N0} {s.TotalOutputTokens,12:N0} ${s.TotalCost,11:F4}");
                }
                lines.Add("");
            }

            // By model (top 10)
            var byModel = SummaryByModel(since, until).Take(10).ToList();
            if (byModel.Count > 0)
            {
                lines.Add("  ── By Model (Top 10) ────────────────────────────────────────");
                lines.Add($"  {"Model",-24} {"Calls",8} {"Avg $/call",12} {"Total $",12}");
                lines.Add($"  {"─────",-24} {"─────",8} {"──────────",12} {"───────",12}");
                foreach (var s in byModel)
                {
                    lines.Add($"  {s.Key,-24} {s.TotalCalls,8:N0} ${s.AverageCostPerCall,11:F6} ${s.TotalCost,11:F4}");
                }
                lines.Add("");
            }

            // Budget alerts
            var alerts = CheckAllBudgets(0m);
            if (alerts.Count > 0)
            {
                lines.Add("  ── Budgets ──────────────────────────────────────────────────");
                foreach (var a in alerts)
                {
                    var icon = a.Exceeded ? "🔴" : a.PercentUsed >= 80 ? "🟡" : "🟢";
                    lines.Add($"  {icon} {a.Name}: ${a.CurrentSpend:F4} / ${a.Threshold:F2} ({a.PercentUsed:F1}%)");
                }
                lines.Add("");
            }

            lines.Add("═══════════════════════════════════════════════════════════════");
            return string.Join(Environment.NewLine, lines);
        }

        private List<UsageEntry> FilterEntries(DateTime? since, DateTime? until)
        {
            IEnumerable<UsageEntry> result = _entries;
            if (since.HasValue) result = result.Where(e => e.Timestamp >= since.Value);
            if (until.HasValue) result = result.Where(e => e.Timestamp <= until.Value);
            return result.ToList();
        }

        private List<UsageSummary> Summarize(Func<UsageEntry, string> keySelector, DateTime? since, DateTime? until)
        {
            var filtered = FilterEntries(since, until);
            return filtered
                .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var count = g.Count();
                    var totalIn = g.Sum(e => (long)e.InputTokens);
                    var totalOut = g.Sum(e => (long)e.OutputTokens);
                    var totalCost = g.Sum(e => e.Cost);

                    return new UsageSummary(
                        Key: g.Key,
                        TotalCalls: count,
                        TotalInputTokens: totalIn,
                        TotalOutputTokens: totalOut,
                        TotalCost: totalCost,
                        AverageCostPerCall: count > 0 ? totalCost / count : 0,
                        AverageInputTokens: count > 0 ? (int)(totalIn / count) : 0,
                        AverageOutputTokens: count > 0 ? (int)(totalOut / count) : 0,
                        FirstUsed: g.Min(e => e.Timestamp),
                        LastUsed: g.Max(e => e.Timestamp)
                    );
                })
                .OrderByDescending(s => s.TotalCost)
                .ToList();
        }
    }
}
