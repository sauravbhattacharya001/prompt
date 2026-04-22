namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Priority level for context allocation. Higher priority components get budget first.
    /// </summary>
    public enum AllocationPriority
    {
        /// <summary>Must be included in full; allocation fails if budget insufficient.</summary>
        Critical = 0,
        /// <summary>High priority; allocated after critical, before normal.</summary>
        High = 1,
        /// <summary>Normal priority; standard allocation.</summary>
        Normal = 2,
        /// <summary>Low priority; allocated only if budget remains.</summary>
        Low = 3,
        /// <summary>Optional; first to be trimmed or dropped entirely.</summary>
        Optional = 4
    }

    /// <summary>
    /// Overflow strategy when a component exceeds its allocated budget.
    /// </summary>
    public enum OverflowStrategy
    {
        /// <summary>Truncate content to fit the allocated budget.</summary>
        Truncate,
        /// <summary>Drop the component entirely if it doesn't fit.</summary>
        Drop,
        /// <summary>Compress by removing whitespace and filler words.</summary>
        Compress,
        /// <summary>Summarize: keep first and last portions with ellipsis.</summary>
        Summarize
    }

    /// <summary>
    /// A named component competing for context window budget.
    /// </summary>
    public class ContextComponent
    {
        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("content")]
        public string Content { get; }

        [JsonPropertyName("priority")]
        public AllocationPriority Priority { get; }

        [JsonPropertyName("overflowStrategy")]
        public OverflowStrategy OverflowStrategy { get; }

        [JsonPropertyName("minTokens")]
        public int MinTokens { get; }

        [JsonPropertyName("maxTokens")]
        public int? MaxTokens { get; }

        [JsonPropertyName("estimatedTokens")]
        public int EstimatedTokens { get; }

        public ContextComponent(string name, string content, AllocationPriority priority = AllocationPriority.Normal,
            OverflowStrategy overflow = OverflowStrategy.Truncate, int minTokens = 0, int? maxTokens = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Content = content ?? "";
            Priority = priority;
            OverflowStrategy = overflow;
            MinTokens = minTokens;
            MaxTokens = maxTokens;
            EstimatedTokens = EstimateTokens(Content);
        }

        internal static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // cl100k_base-style approximation: ~4 chars per token
            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }
    }

    /// <summary>
    /// Result of allocating budget to a single component.
    /// </summary>
    public class AllocationResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("requestedTokens")]
        public int RequestedTokens { get; set; }

        [JsonPropertyName("allocatedTokens")]
        public int AllocatedTokens { get; set; }

        [JsonPropertyName("finalContent")]
        public string FinalContent { get; set; }

        [JsonPropertyName("wasModified")]
        public bool WasModified { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } // "kept", "truncated", "compressed", "summarized", "dropped"

        [JsonPropertyName("priority")]
        public AllocationPriority Priority { get; set; }
    }

    /// <summary>
    /// A diagnostic insight or recommendation from the allocator.
    /// </summary>
    public class AllocationInsight
    {
        [JsonPropertyName("severity")]
        public string Severity { get; set; } // "info", "warning", "critical"

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Full allocation plan for an entire context window.
    /// </summary>
    public class AllocationPlan
    {
        [JsonPropertyName("totalBudget")]
        public int TotalBudget { get; set; }

        [JsonPropertyName("totalRequested")]
        public int TotalRequested { get; set; }

        [JsonPropertyName("totalAllocated")]
        public int TotalAllocated { get; set; }

        [JsonPropertyName("remainingBudget")]
        public int RemainingBudget { get; set; }

        [JsonPropertyName("utilizationPercent")]
        public double UtilizationPercent { get; set; }

        [JsonPropertyName("allocations")]
        public List<AllocationResult> Allocations { get; set; } = new List<AllocationResult>();

        [JsonPropertyName("droppedComponents")]
        public List<string> DroppedComponents { get; set; } = new List<string>();

        [JsonPropertyName("insights")]
        public List<AllocationInsight> Insights { get; set; } = new List<AllocationInsight>();

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Assemble the final prompt from allocated components in original order.
        /// </summary>
        public string AssemblePrompt(string separator = "\n\n")
        {
            return string.Join(separator, Allocations
                .Where(a => a.Action != "dropped")
                .Select(a => a.FinalContent));
        }

        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

        public string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Context Allocation Plan");
            sb.AppendLine();
            sb.AppendLine($"**Budget:** {TotalAllocated:N0} / {TotalBudget:N0} tokens ({UtilizationPercent:F1}% utilized)");
            sb.AppendLine($"**Requested:** {TotalRequested:N0} tokens | **Remaining:** {RemainingBudget:N0} tokens");
            sb.AppendLine();
            sb.AppendLine("## Allocations");
            sb.AppendLine();
            sb.AppendLine("| Component | Priority | Requested | Allocated | Action |");
            sb.AppendLine("|-----------|----------|-----------|-----------|--------|");
            foreach (var a in Allocations)
            {
                sb.AppendLine($"| {a.Name} | {a.Priority} | {a.RequestedTokens:N0} | {a.AllocatedTokens:N0} | {a.Action} |");
            }
            if (DroppedComponents.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"**Dropped:** {string.Join(", ", DroppedComponents)}");
            }
            if (Insights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Insights");
                foreach (var insight in Insights)
                {
                    var icon = insight.Severity == "critical" ? "🔴" : insight.Severity == "warning" ? "🟡" : "🔵";
                    sb.AppendLine($"- {icon} {insight.Message}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Autonomous context window budget manager that optimally distributes token budgets
    /// across prompt components using priority-based allocation with overflow handling.
    /// 
    /// Supports proactive insights: detects over-utilization, recommends component trimming,
    /// and warns about critical capacity issues before they cause failures.
    /// 
    /// Usage:
    ///   var allocator = new PromptContextAllocator(4096);
    ///   allocator.Add("system", systemPrompt, AllocationPriority.Critical);
    ///   allocator.Add("context", retrievedDocs, AllocationPriority.Normal, OverflowStrategy.Summarize);
    ///   allocator.Add("examples", fewShot, AllocationPriority.Low, OverflowStrategy.Drop);
    ///   allocator.Add("user", userMessage, AllocationPriority.High);
    ///   var plan = allocator.Allocate();
    ///   var finalPrompt = plan.AssemblePrompt();
    /// </summary>
    public class PromptContextAllocator
    {
        private readonly int _totalBudget;
        private readonly int _reserveTokens;
        private readonly List<ContextComponent> _components = new List<ContextComponent>();

        private static readonly string[] FillerWords = {
            " really", " very", " just", " quite", " rather", " simply",
            " basically", " actually", " literally", " definitely", " certainly",
            "  ", "   ", "    "
        };

        /// <summary>
        /// Create a new context allocator.
        /// </summary>
        /// <param name="totalBudget">Total context window size in tokens.</param>
        /// <param name="reserveTokens">Tokens to reserve for model response (default 256).</param>
        public PromptContextAllocator(int totalBudget, int reserveTokens = 256)
        {
            if (totalBudget <= 0) throw new ArgumentException("Budget must be positive.", nameof(totalBudget));
            _totalBudget = totalBudget;
            _reserveTokens = Math.Max(0, reserveTokens);
        }

        /// <summary>
        /// Add a component to compete for budget.
        /// </summary>
        public PromptContextAllocator Add(string name, string content,
            AllocationPriority priority = AllocationPriority.Normal,
            OverflowStrategy overflow = OverflowStrategy.Truncate,
            int minTokens = 0, int? maxTokens = null)
        {
            _components.Add(new ContextComponent(name, content, priority, overflow, minTokens, maxTokens));
            return this;
        }

        /// <summary>
        /// Allocate the budget across all added components.
        /// Priority-based: Critical first, then High, Normal, Low, Optional.
        /// Returns a plan with allocated content, actions taken, and proactive insights.
        /// </summary>
        public AllocationPlan Allocate()
        {
            var plan = new AllocationPlan
            {
                TotalBudget = _totalBudget,
                TotalRequested = _components.Sum(c => c.EstimatedTokens),
                Success = true
            };

            int availableBudget = _totalBudget - _reserveTokens;
            if (availableBudget <= 0)
            {
                plan.Success = false;
                plan.Insights.Add(new AllocationInsight { Severity = "critical", Message = "Reserve tokens exceed total budget. No space for content." });
                return plan;
            }

            int remaining = availableBudget;

            // Sort by priority (Critical=0 first), preserve insertion order within same priority
            var ordered = _components
                .Select((c, i) => (Component: c, Index: i))
                .OrderBy(x => (int)x.Component.Priority)
                .ThenBy(x => x.Index)
                .ToList();

            var results = new AllocationResult[_components.Count];

            foreach (var (comp, idx) in ordered)
            {
                var result = new AllocationResult
                {
                    Name = comp.Name,
                    RequestedTokens = comp.EstimatedTokens,
                    Priority = comp.Priority
                };

                int effectiveMax = comp.MaxTokens.HasValue
                    ? Math.Min(comp.MaxTokens.Value, comp.EstimatedTokens)
                    : comp.EstimatedTokens;
                int desired = effectiveMax;

                if (desired <= remaining)
                {
                    // Fits entirely
                    result.AllocatedTokens = desired;
                    result.FinalContent = comp.Content;
                    result.WasModified = false;
                    result.Action = "kept";
                    remaining -= desired;
                }
                else if (remaining >= comp.MinTokens && remaining > 0)
                {
                    // Partial fit: apply overflow strategy
                    int allocated = remaining;
                    result.AllocatedTokens = allocated;
                    result.WasModified = true;

                    switch (comp.OverflowStrategy)
                    {
                        case OverflowStrategy.Truncate:
                            result.FinalContent = TruncateToTokens(comp.Content, allocated);
                            result.Action = "truncated";
                            break;
                        case OverflowStrategy.Compress:
                            var compressed = CompressContent(comp.Content);
                            var compTokens = ContextComponent.EstimateTokens(compressed);
                            if (compTokens <= remaining)
                            {
                                result.FinalContent = compressed;
                                result.AllocatedTokens = compTokens;
                                result.Action = "compressed";
                            }
                            else
                            {
                                result.FinalContent = TruncateToTokens(compressed, allocated);
                                result.Action = "compressed+truncated";
                            }
                            break;
                        case OverflowStrategy.Summarize:
                            result.FinalContent = SummarizeToTokens(comp.Content, allocated);
                            result.Action = "summarized";
                            break;
                        case OverflowStrategy.Drop:
                            result.AllocatedTokens = 0;
                            result.FinalContent = "";
                            result.Action = "dropped";
                            plan.DroppedComponents.Add(comp.Name);
                            break;
                    }
                    remaining -= result.AllocatedTokens;
                }
                else
                {
                    // No room
                    if (comp.Priority == AllocationPriority.Critical)
                    {
                        plan.Success = false;
                        plan.Insights.Add(new AllocationInsight
                        {
                            Severity = "critical",
                            Message = $"Critical component '{comp.Name}' requires {comp.EstimatedTokens} tokens but only {remaining} available. Allocation failed."
                        });
                    }
                    result.AllocatedTokens = 0;
                    result.FinalContent = "";
                    result.WasModified = true;
                    result.Action = "dropped";
                    plan.DroppedComponents.Add(comp.Name);
                }

                results[idx] = result;
            }

            plan.Allocations = results.ToList();
            plan.TotalAllocated = plan.Allocations.Sum(a => a.AllocatedTokens);
            plan.RemainingBudget = availableBudget - plan.TotalAllocated;
            plan.UtilizationPercent = availableBudget > 0 ? (plan.TotalAllocated * 100.0 / availableBudget) : 0;

            // Proactive insights
            GenerateInsights(plan, availableBudget);

            return plan;
        }

        /// <summary>
        /// Quick check: can all components fit without modification?
        /// </summary>
        public bool CanFitAll()
        {
            int needed = _components.Sum(c => c.EstimatedTokens);
            return needed <= (_totalBudget - _reserveTokens);
        }

        /// <summary>
        /// Recommend a minimum budget to fit all components at full size.
        /// </summary>
        public int RecommendedBudget()
        {
            return _components.Sum(c => c.EstimatedTokens) + _reserveTokens;
        }

        private void GenerateInsights(AllocationPlan plan, int availableBudget)
        {
            // Over-utilization warning
            if (plan.UtilizationPercent > 95)
            {
                plan.Insights.Add(new AllocationInsight
                {
                    Severity = "warning",
                    Message = $"Context window is {plan.UtilizationPercent:F1}% utilized. Consider increasing budget or trimming lower-priority components."
                });
            }

            // Dropped component warnings
            if (plan.DroppedComponents.Count > 0)
            {
                plan.Insights.Add(new AllocationInsight
                {
                    Severity = "warning",
                    Message = $"{plan.DroppedComponents.Count} component(s) dropped: {string.Join(", ", plan.DroppedComponents)}. Increase budget by ~{plan.TotalRequested - plan.TotalAllocated:N0} tokens to include all."
                });
            }

            // Single component dominance
            foreach (var a in plan.Allocations.Where(a => a.Action != "dropped"))
            {
                double share = plan.TotalAllocated > 0 ? (a.AllocatedTokens * 100.0 / availableBudget) : 0;
                if (share > 60)
                {
                    plan.Insights.Add(new AllocationInsight
                    {
                        Severity = "info",
                        Message = $"Component '{a.Name}' consumes {share:F0}% of available budget. Consider summarizing or compressing it."
                    });
                }
            }

            // Low utilization
            if (plan.UtilizationPercent < 30 && plan.TotalRequested > 0)
            {
                plan.Insights.Add(new AllocationInsight
                {
                    Severity = "info",
                    Message = $"Only {plan.UtilizationPercent:F1}% of budget used. You could add more context (examples, retrieved docs) to improve output quality."
                });
            }
        }

        private static string TruncateToTokens(string text, int maxTokens)
        {
            int maxChars = maxTokens * 4;
            if (text.Length <= maxChars) return text;
            return text.Substring(0, Math.Max(0, maxChars - 3)) + "...";
        }

        private static string CompressContent(string text)
        {
            var result = text;
            foreach (var filler in FillerWords)
            {
                result = result.Replace(filler, filler.Trim().Length == 0 ? " " : "");
            }
            // Collapse multiple newlines
            while (result.Contains("\n\n\n"))
                result = result.Replace("\n\n\n", "\n\n");
            return result.Trim();
        }

        private static string SummarizeToTokens(string text, int maxTokens)
        {
            int maxChars = maxTokens * 4;
            if (text.Length <= maxChars) return text;

            // Keep first ~40% and last ~40% with ellipsis in middle
            int keepChars = Math.Max(10, maxChars - 5);
            int firstPart = keepChars * 2 / 5;
            int lastPart = keepChars - firstPart;

            var first = text.Substring(0, firstPart);
            var last = text.Substring(text.Length - lastPart);
            return first + " ... " + last;
        }
    }
}
