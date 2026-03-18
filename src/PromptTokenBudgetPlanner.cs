namespace Prompt
{
    using System.Text.Json;

    /// <summary>
    /// Defines a named section within a prompt's token budget.
    /// </summary>
    public record BudgetSection(
        string Name,
        string Description,
        int AllocatedTokens,
        string? Content = null
    )
    {
        /// <summary>Actual token usage if content is provided.</summary>
        public int ActualTokens => Content != null ? PromptGuard.EstimateTokens(Content) : 0;

        /// <summary>Usage as a percentage of allocation.</summary>
        public double UsagePercent => AllocatedTokens > 0 ? (double)ActualTokens / AllocatedTokens * 100 : 0;

        /// <summary>Remaining tokens in this section.</summary>
        public int Remaining => Math.Max(0, AllocatedTokens - ActualTokens);

        /// <summary>Whether this section exceeds its allocation.</summary>
        public bool IsOverBudget => ActualTokens > AllocatedTokens;
    }

    /// <summary>
    /// Priority level for budget sections when optimizing.
    /// </summary>
    public enum BudgetPriority
    {
        /// <summary>Cannot be reduced.</summary>
        Fixed,
        /// <summary>Important, reduce last.</summary>
        High,
        /// <summary>Normal priority.</summary>
        Normal,
        /// <summary>Can be reduced first.</summary>
        Low
    }

    /// <summary>
    /// A section with its priority for optimization.
    /// </summary>
    public record PrioritizedSection(
        BudgetSection Section,
        BudgetPriority Priority
    );

    /// <summary>
    /// Result of a budget plan analysis.
    /// </summary>
    public class BudgetPlan
    {
        /// <summary>Target model name.</summary>
        public string ModelName { get; init; } = "";

        /// <summary>Total context window of the target model.</summary>
        public int ContextWindow { get; init; }

        /// <summary>Tokens reserved for the model's response.</summary>
        public int ResponseReserve { get; init; }

        /// <summary>Tokens available for prompt content.</summary>
        public int AvailableForPrompt => ContextWindow - ResponseReserve;

        /// <summary>All budget sections.</summary>
        public List<PrioritizedSection> Sections { get; init; } = new();

        /// <summary>Total tokens allocated across all sections.</summary>
        public int TotalAllocated => Sections.Sum(s => s.Section.AllocatedTokens);

        /// <summary>Total tokens actually used (sections with content).</summary>
        public int TotalUsed => Sections.Sum(s => s.Section.ActualTokens);

        /// <summary>Unallocated tokens (available minus allocated).</summary>
        public int Unallocated => Math.Max(0, AvailableForPrompt - TotalAllocated);

        /// <summary>Whether the total allocation exceeds available space.</summary>
        public bool IsOverBudget => TotalAllocated > AvailableForPrompt;

        /// <summary>Overall budget utilization percentage.</summary>
        public double UtilizationPercent => AvailableForPrompt > 0
            ? (double)TotalAllocated / AvailableForPrompt * 100
            : 0;

        /// <summary>Sections that are over their individual budgets.</summary>
        public IReadOnlyList<PrioritizedSection> OverBudgetSections =>
            Sections.Where(s => s.Section.IsOverBudget).ToList().AsReadOnly();

        /// <summary>Optimization suggestions based on current usage.</summary>
        public IReadOnlyList<string> Suggestions => GenerateSuggestions();

        private List<string> GenerateSuggestions()
        {
            var suggestions = new List<string>();

            if (IsOverBudget)
            {
                var excess = TotalAllocated - AvailableForPrompt;
                suggestions.Add($"⚠ Over budget by {excess:N0} tokens. Reduce allocations or pick a larger model.");

                var lowPriority = Sections
                    .Where(s => s.Priority == BudgetPriority.Low)
                    .OrderByDescending(s => s.Section.AllocatedTokens)
                    .ToList();
                if (lowPriority.Any())
                    suggestions.Add($"💡 Consider reducing low-priority section '{lowPriority[0].Section.Name}' ({lowPriority[0].Section.AllocatedTokens:N0} tokens).");

                var normalSections = Sections
                    .Where(s => s.Priority == BudgetPriority.Normal)
                    .OrderByDescending(s => s.Section.AllocatedTokens)
                    .ToList();
                if (normalSections.Any())
                    suggestions.Add($"💡 Normal-priority section '{normalSections[0].Section.Name}' could be trimmed.");
            }

            foreach (var s in Sections.Where(s => s.Section.Content != null))
            {
                if (s.Section.IsOverBudget)
                {
                    var over = s.Section.ActualTokens - s.Section.AllocatedTokens;
                    suggestions.Add($"🔴 '{s.Section.Name}' exceeds budget by {over:N0} tokens ({s.Section.UsagePercent:F0}% used).");
                }
                else if (s.Section.UsagePercent < 30 && s.Section.AllocatedTokens > 500)
                {
                    suggestions.Add($"💰 '{s.Section.Name}' uses only {s.Section.UsagePercent:F0}% of its budget — consider reallocating {s.Section.Remaining:N0} tokens.");
                }
            }

            if (!IsOverBudget && Unallocated > 1000)
                suggestions.Add($"📦 {Unallocated:N0} tokens unallocated — room for more few-shot examples or context.");

            if (ResponseReserve < 500)
                suggestions.Add("⚡ Response reserve is small (<500 tokens) — output may be truncated.");

            if (UtilizationPercent > 90 && !IsOverBudget)
                suggestions.Add("🔥 Budget utilization above 90% — little room for prompt growth.");

            return suggestions;
        }

        /// <summary>Format the plan as a readable text report.</summary>
        public string ToText()
        {
            var lines = new List<string>();
            lines.Add("═══════════════════════════════════════════════════════════════");
            lines.Add("  Token Budget Planner Report");
            lines.Add("═══════════════════════════════════════════════════════════════");
            lines.Add($"  Model:            {ModelName}");
            lines.Add($"  Context window:   {ContextWindow:N0} tokens");
            lines.Add($"  Response reserve: {ResponseReserve:N0} tokens");
            lines.Add($"  Available:        {AvailableForPrompt:N0} tokens");
            lines.Add($"  Utilization:      {UtilizationPercent:F1}%");
            lines.Add("");
            lines.Add($"  {"Section",-24} {"Priority",-10} {"Allocated",-12} {"Used",-10} {"Usage%",-8} {"Status"}");
            lines.Add($"  {"───────",-24} {"────────",-10} {"─────────",-12} {"────",-10} {"──────",-8} {"──────"}");

            foreach (var ps in Sections)
            {
                var s = ps.Section;
                var used = s.Content != null ? $"{s.ActualTokens:N0}" : "—";
                var pct = s.Content != null ? $"{s.UsagePercent:F0}%" : "—";
                var status = s.IsOverBudget ? "🔴 OVER" : s.Content != null && s.UsagePercent > 80 ? "🟡 TIGHT" : "✅";
                lines.Add($"  {s.Name,-24} {ps.Priority,-10} {s.AllocatedTokens,-12:N0} {used,-10} {pct,-8} {status}");
            }

            lines.Add("");
            lines.Add($"  Total allocated: {TotalAllocated:N0} / {AvailableForPrompt:N0}");
            if (Unallocated > 0)
                lines.Add($"  Unallocated:     {Unallocated:N0}");

            if (Suggestions.Any())
            {
                lines.Add("");
                lines.Add("  Suggestions:");
                foreach (var s in Suggestions)
                    lines.Add($"    {s}");
            }

            lines.Add("═══════════════════════════════════════════════════════════════");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Serialize the plan to JSON.</summary>
        public string ToJson(bool indented = true)
        {
            var data = new
            {
                model = ModelName,
                contextWindow = ContextWindow,
                responseReserve = ResponseReserve,
                availableForPrompt = AvailableForPrompt,
                utilizationPercent = Math.Round(UtilizationPercent, 1),
                isOverBudget = IsOverBudget,
                totalAllocated = TotalAllocated,
                totalUsed = TotalUsed,
                unallocated = Unallocated,
                sections = Sections.Select(ps => new
                {
                    name = ps.Section.Name,
                    description = ps.Section.Description,
                    priority = ps.Priority.ToString(),
                    allocatedTokens = ps.Section.AllocatedTokens,
                    actualTokens = ps.Section.Content != null ? ps.Section.ActualTokens : (int?)null,
                    usagePercent = ps.Section.Content != null ? Math.Round(ps.Section.UsagePercent, 1) : (double?)null,
                    isOverBudget = ps.Section.IsOverBudget,
                    hasContent = ps.Section.Content != null
                }).ToList(),
                suggestions = Suggestions.ToList()
            };
            return JsonSerializer.Serialize(data, SerializationGuards.WriteOptions(indented));
        }
    }

    /// <summary>
    /// Plans and manages token budget allocation across prompt sections for a
    /// target model's context window. Helps users understand how to distribute
    /// tokens between system prompts, few-shot examples, user input, and response
    /// reserve to maximize prompt effectiveness without exceeding limits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var planner = new PromptTokenBudgetPlanner("gpt-4o", 128_000);
    /// planner.SetResponseReserve(4_000);
    /// planner.AddSection("System Prompt", "Core instructions", 2_000, BudgetPriority.Fixed);
    /// planner.AddSection("Few-Shot Examples", "Example Q&amp;A pairs", 8_000, BudgetPriority.Normal);
    /// planner.AddSection("User Input", "Dynamic user query", 4_000, BudgetPriority.High);
    /// planner.AddSection("Context/RAG", "Retrieved documents", 20_000, BudgetPriority.Low);
    ///
    /// // Optionally fill sections with content to see actual usage
    /// planner.SetContent("System Prompt", "You are a helpful assistant...");
    ///
    /// var plan = planner.BuildPlan();
    /// Console.WriteLine(plan.ToText());
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptTokenBudgetPlanner
    {
        /// <summary>Maximum sections allowed.</summary>
        public const int MaxSections = 20;

        private readonly string _modelName;
        private readonly int _contextWindow;
        private int _responseReserve;
        private readonly List<(string Name, string Description, int Tokens, BudgetPriority Priority, string? Content)> _sections = new();

        /// <summary>
        /// Creates a new budget planner for a target model.
        /// </summary>
        /// <param name="modelName">Display name of the target model.</param>
        /// <param name="contextWindow">Total context window in tokens.</param>
        /// <exception cref="ArgumentException">If contextWindow is not positive.</exception>
        public PromptTokenBudgetPlanner(string modelName, int contextWindow)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("Model name cannot be empty.");
            if (contextWindow <= 0)
                throw new ArgumentException("Context window must be positive.");
            _modelName = modelName;
            _contextWindow = contextWindow;
            _responseReserve = Math.Min(4_000, contextWindow / 4);
        }

        /// <summary>
        /// Set the number of tokens reserved for the model's response.
        /// </summary>
        public void SetResponseReserve(int tokens)
        {
            if (tokens < 0)
                throw new ArgumentException("Response reserve cannot be negative.");
            if (tokens >= _contextWindow)
                throw new ArgumentException("Response reserve cannot exceed context window.");
            _responseReserve = tokens;
        }

        /// <summary>
        /// Add a named section to the budget.
        /// </summary>
        public void AddSection(string name, string description, int allocatedTokens, BudgetPriority priority = BudgetPriority.Normal)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Section name cannot be empty.");
            if (allocatedTokens < 0)
                throw new ArgumentException("Allocated tokens cannot be negative.");
            if (_sections.Count >= MaxSections)
                throw new InvalidOperationException($"Cannot exceed {MaxSections} sections.");
            if (_sections.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Duplicate section: {name}");
            _sections.Add((name, description, allocatedTokens, priority, null));
        }

        /// <summary>
        /// Remove a section by name.
        /// </summary>
        public bool RemoveSection(string name) =>
            _sections.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;

        /// <summary>
        /// Set actual content for a section to measure real usage.
        /// </summary>
        public void SetContent(string sectionName, string content)
        {
            ArgumentNullException.ThrowIfNull(content);
            var idx = _sections.FindIndex(s => s.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                throw new ArgumentException($"Unknown section: {sectionName}");
            var s = _sections[idx];
            _sections[idx] = (s.Name, s.Description, s.Tokens, s.Priority, content);
        }

        /// <summary>
        /// Update the token allocation for an existing section.
        /// </summary>
        public void ResizeSection(string sectionName, int newTokens)
        {
            if (newTokens < 0)
                throw new ArgumentException("Tokens cannot be negative.");
            var idx = _sections.FindIndex(s => s.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                throw new ArgumentException($"Unknown section: {sectionName}");
            var s = _sections[idx];
            _sections[idx] = (s.Name, s.Description, newTokens, s.Priority, s.Content);
        }

        /// <summary>
        /// Build the budget plan with analysis and suggestions.
        /// </summary>
        public BudgetPlan BuildPlan()
        {
            var sections = _sections.Select(s => new PrioritizedSection(
                new BudgetSection(s.Name, s.Description, s.Tokens, s.Content),
                s.Priority
            )).ToList();

            return new BudgetPlan
            {
                ModelName = _modelName,
                ContextWindow = _contextWindow,
                ResponseReserve = _responseReserve,
                Sections = sections
            };
        }

        /// <summary>
        /// Create a planner pre-configured with common prompt sections.
        /// </summary>
        public static PromptTokenBudgetPlanner CreateStandard(string modelName, int contextWindow, int responseReserve = 4_000)
        {
            var planner = new PromptTokenBudgetPlanner(modelName, contextWindow);
            planner.SetResponseReserve(responseReserve);
            planner.AddSection("System Prompt", "Core instructions and persona", 2_000, BudgetPriority.Fixed);
            planner.AddSection("Few-Shot Examples", "Example input/output pairs", 4_000, BudgetPriority.Normal);
            planner.AddSection("User Input", "The user's query or request", 2_000, BudgetPriority.High);
            planner.AddSection("Context/RAG", "Retrieved documents or context", 10_000, BudgetPriority.Low);
            return planner;
        }

        /// <summary>
        /// Auto-distribute unallocated tokens to non-fixed sections proportionally.
        /// </summary>
        public void AutoDistribute()
        {
            var available = _contextWindow - _responseReserve;
            var fixedTotal = _sections.Where(s => s.Priority == BudgetPriority.Fixed).Sum(s => s.Tokens);
            var remaining = available - fixedTotal;
            if (remaining <= 0) return;

            var flexible = _sections
                .Select((s, i) => (s, i))
                .Where(x => x.s.Priority != BudgetPriority.Fixed)
                .ToList();

            if (!flexible.Any()) return;

            var currentFlexTotal = flexible.Sum(x => x.s.Tokens);
            foreach (var (s, i) in flexible)
            {
                var ratio = currentFlexTotal > 0
                    ? (double)s.Tokens / currentFlexTotal
                    : 1.0 / flexible.Count;
                var newTokens = (int)(remaining * ratio);
                _sections[i] = (s.Name, s.Description, newTokens, s.Priority, s.Content);
            }
        }
    }
}
