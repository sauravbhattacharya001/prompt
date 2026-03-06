namespace Prompt
{
    using System.Collections.Concurrent;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// How to truncate a context block when it exceeds its token allocation.
    /// </summary>
    public enum TruncationStrategy
    {
        /// <summary>Keep the end (most recent content). Default for logs/history.</summary>
        KeepEnd,

        /// <summary>Keep the beginning (headers/summaries). Default for documents.</summary>
        KeepStart,

        /// <summary>Keep both start and end, remove the middle.</summary>
        KeepStartAndEnd,

        /// <summary>Drop the entire block if it doesn't fit.</summary>
        DropIfOverBudget,

        /// <summary>Never truncate — always include full content (may exceed budget).</summary>
        Never
    }

    /// <summary>
    /// A single block of context to include in the prompt. Each block has
    /// content, a priority (higher = more important), an optional token
    /// limit, and a truncation strategy.
    /// </summary>
    public class ContextBlock
    {
        /// <summary>Unique identifier for this block.</summary>
        public string Id { get; }

        /// <summary>The text content of this block.</summary>
        public string Content { get; }

        /// <summary>
        /// Priority from 0–100. Higher priority blocks are allocated tokens
        /// first and are truncated last. Default: 50.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Optional label shown in the assembled context (e.g., "## User Profile").
        /// If null, content is included without a header.
        /// </summary>
        public string? Label { get; }

        /// <summary>
        /// Optional maximum tokens for this block. If null, the block
        /// competes for remaining budget based on priority.
        /// </summary>
        public int? MaxTokens { get; }

        /// <summary>
        /// Minimum tokens this block needs to be useful. If the allocated
        /// budget is below this, the block is dropped entirely. Default: 0.
        /// </summary>
        public int MinTokens { get; }

        /// <summary>How to truncate if the block exceeds its allocation.</summary>
        public TruncationStrategy Truncation { get; }

        /// <summary>Optional metadata tags for filtering and grouping.</summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>Estimated token count for the full content.</summary>
        public int EstimatedTokens { get; }

        /// <summary>
        /// Creates a new context block.
        /// </summary>
        public ContextBlock(
            string id,
            string content,
            int priority = 50,
            string? label = null,
            int? maxTokens = null,
            int minTokens = 0,
            TruncationStrategy truncation = TruncationStrategy.KeepStart,
            IEnumerable<string>? tags = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Block id cannot be null or empty.", nameof(id));

            Id = id;
            Content = content ?? string.Empty;
            Priority = Math.Clamp(priority, 0, 100);
            Label = label;
            MaxTokens = maxTokens.HasValue ? Math.Max(1, maxTokens.Value) : null;
            MinTokens = Math.Max(0, minTokens);
            Truncation = truncation;
            Tags = tags?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
            EstimatedTokens = PromptGuard.EstimateTokens(content);
        }
    }

    /// <summary>
    /// Result of building a context — the assembled text plus metadata
    /// about what was included, truncated, or dropped.
    /// </summary>
    public class ContextBuildResult
    {
        /// <summary>The assembled context text, ready to insert into a prompt.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>Total estimated tokens in the assembled context.</summary>
        public int TotalTokens { get; init; }

        /// <summary>Token budget that was available.</summary>
        public int Budget { get; init; }

        /// <summary>Number of blocks included (possibly truncated).</summary>
        public int IncludedCount { get; init; }

        /// <summary>Number of blocks dropped entirely.</summary>
        public int DroppedCount { get; init; }

        /// <summary>Number of blocks that were truncated.</summary>
        public int TruncatedCount { get; init; }

        /// <summary>Per-block details.</summary>
        public IReadOnlyList<BlockResult> Blocks { get; init; } = Array.Empty<BlockResult>();

        /// <summary>Blocks that were dropped, with reasons.</summary>
        public IReadOnlyList<DroppedBlock> Dropped { get; init; } = Array.Empty<DroppedBlock>();

        /// <summary>Percentage of budget used (0–100).</summary>
        public double UsagePercent => Budget > 0 ? Math.Min(100.0, TotalTokens * 100.0 / Budget) : 0;

        /// <summary>Remaining tokens in budget.</summary>
        public int RemainingTokens => Math.Max(0, Budget - TotalTokens);
    }

    /// <summary>Details about how a single block was included.</summary>
    public class BlockResult
    {
        /// <summary>Block identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Whether the block was truncated.</summary>
        public bool WasTruncated { get; init; }

        /// <summary>Original token count before truncation.</summary>
        public int OriginalTokens { get; init; }

        /// <summary>Final token count after truncation (including label).</summary>
        public int FinalTokens { get; init; }

        /// <summary>Priority used for ordering.</summary>
        public int Priority { get; init; }
    }

    /// <summary>Details about why a block was dropped.</summary>
    public class DroppedBlock
    {
        /// <summary>Block identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Why the block was dropped.</summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>Tokens the block would have needed.</summary>
        public int RequestedTokens { get; init; }
    }

    /// <summary>
    /// Assembles complex prompt contexts from multiple prioritized data
    /// sources with automatic token budgeting and intelligent truncation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PromptContextBuilder solves the common problem of constructing
    /// prompts from many sources (user profile, conversation history,
    /// retrieved documents, system instructions) while staying within
    /// a model's context window.
    /// </para>
    /// <para>
    /// Blocks are allocated tokens based on priority: higher-priority
    /// blocks get their full content first, lower-priority blocks
    /// share remaining space. If the budget is tight, low-priority
    /// blocks are truncated or dropped according to their strategy.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var builder = new PromptContextBuilder(maxTokens: 4000);
    ///
    /// builder.AddBlock("system", "You are a helpful assistant.",
    ///     priority: 100, label: "## System Instructions",
    ///     truncation: TruncationStrategy.Never);
    ///
    /// builder.AddBlock("profile", userProfile,
    ///     priority: 80, label: "## User Profile",
    ///     maxTokens: 500);
    ///
    /// builder.AddBlock("history", chatHistory,
    ///     priority: 60, label: "## Recent Conversation",
    ///     truncation: TruncationStrategy.KeepEnd);
    ///
    /// builder.AddBlock("docs", retrievedDocs,
    ///     priority: 40, label: "## Relevant Documents",
    ///     truncation: TruncationStrategy.KeepStart,
    ///     minTokens: 100);
    ///
    /// var result = builder.Build();
    /// Console.WriteLine($"Context: {result.TotalTokens}/{result.Budget} tokens");
    /// Console.WriteLine($"Included: {result.IncludedCount}, Dropped: {result.DroppedCount}");
    /// string prompt = $"{result.Text}\n\n## User Question\n{userQuestion}";
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptContextBuilder
    {
        private readonly List<ContextBlock> _blocks = new();
        private readonly HashSet<string> _blockIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _maxTokens;
        private string _separator = "\n\n";
        private string _overflowMarker = "\n[... truncated ...]\n";

        /// <summary>
        /// Creates a new context builder with the given token budget.
        /// </summary>
        /// <param name="maxTokens">
        /// Maximum tokens for the assembled context. Must be at least 1.
        /// </param>
        public PromptContextBuilder(int maxTokens)
        {
            if (maxTokens < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTokens),
                    "Token budget must be at least 1.");
            _maxTokens = maxTokens;
        }

        /// <summary>Gets the total token budget.</summary>
        public int MaxTokens => _maxTokens;

        /// <summary>Gets the number of blocks added.</summary>
        public int BlockCount => _blocks.Count;

        /// <summary>
        /// Set the separator between blocks in the assembled output.
        /// Default: "\n\n".
        /// </summary>
        public PromptContextBuilder WithSeparator(string separator)
        {
            _separator = separator ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Set the marker inserted when content is truncated.
        /// Default: "\n[... truncated ...]\n".
        /// </summary>
        public PromptContextBuilder WithOverflowMarker(string marker)
        {
            _overflowMarker = marker ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Add a context block.
        /// </summary>
        /// <returns>This builder (for chaining).</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when a block with the same id already exists.
        /// </exception>
        public PromptContextBuilder AddBlock(
            string id,
            string content,
            int priority = 50,
            string? label = null,
            int? maxTokens = null,
            int minTokens = 0,
            TruncationStrategy truncation = TruncationStrategy.KeepStart,
            IEnumerable<string>? tags = null)
        {
            if (!_blockIds.Add(id?.Trim() ?? throw new ArgumentException("id is required")))
                throw new ArgumentException($"Block with id '{id}' already exists.", nameof(id));

            _blocks.Add(new ContextBlock(id, content, priority, label,
                maxTokens, minTokens, truncation, tags));
            return this;
        }

        /// <summary>
        /// Add a pre-built <see cref="ContextBlock"/>.
        /// </summary>
        public PromptContextBuilder AddBlock(ContextBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            if (!_blockIds.Add(block.Id))
                throw new ArgumentException($"Block with id '{block.Id}' already exists.");
            _blocks.Add(block);
            return this;
        }

        /// <summary>
        /// Remove a block by id.
        /// </summary>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveBlock(string id)
        {
            var block = _blocks.FirstOrDefault(b =>
                string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));
            if (block == null) return false;
            _blocks.Remove(block);
            _blockIds.Remove(id);
            return true;
        }

        /// <summary>
        /// Get all blocks, optionally filtered by tag.
        /// </summary>
        public IReadOnlyList<ContextBlock> GetBlocks(string? tag = null)
        {
            if (tag == null) return _blocks.AsReadOnly();
            return _blocks.Where(b => b.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                         .ToList().AsReadOnly();
        }

        /// <summary>
        /// Preview how many tokens the current blocks would use without
        /// actually building the context.
        /// </summary>
        public int EstimateTotalTokens()
        {
            int total = 0;
            int sepTokens = PromptGuard.EstimateTokens(_separator);
            foreach (var block in _blocks)
            {
                if (total > 0) total += sepTokens;
                if (block.Label != null)
                    total += PromptGuard.EstimateTokens(block.Label + "\n");
                total += block.EstimatedTokens;
            }
            return total;
        }

        /// <summary>
        /// Build the context: allocate tokens by priority, truncate as
        /// needed, and assemble the final text.
        /// </summary>
        /// <returns>A <see cref="ContextBuildResult"/> with text and metadata.</returns>
        public ContextBuildResult Build()
        {
            if (_blocks.Count == 0)
            {
                return new ContextBuildResult
                {
                    Text = string.Empty,
                    Budget = _maxTokens,
                    TotalTokens = 0,
                    IncludedCount = 0,
                    DroppedCount = 0,
                    TruncatedCount = 0,
                };
            }

            int sepTokens = PromptGuard.EstimateTokens(_separator);
            int markerTokens = PromptGuard.EstimateTokens(_overflowMarker);

            // Sort by priority descending (highest first for allocation)
            var ordered = _blocks.OrderByDescending(b => b.Priority)
                                 .ThenBy(b => b.Id)
                                 .ToList();

            // Phase 1: Calculate how much each block needs (content + label)
            var needs = new Dictionary<string, int>();
            foreach (var block in ordered)
            {
                int need = block.EstimatedTokens;
                if (block.Label != null)
                    need += PromptGuard.EstimateTokens(block.Label + "\n");
                if (block.MaxTokens.HasValue)
                    need = Math.Min(need, block.MaxTokens.Value);
                needs[block.Id] = need;
            }

            // Phase 2: Allocate tokens greedily by priority
            int remaining = _maxTokens;
            // Reserve separator tokens
            int totalSepTokens = Math.Max(0, ordered.Count - 1) * sepTokens;
            remaining -= totalSepTokens;

            var allocations = new Dictionary<string, int>();
            var dropped = new List<DroppedBlock>();

            // First pass: allocate Never-truncate blocks (they must fit)
            foreach (var block in ordered.Where(b => b.Truncation == TruncationStrategy.Never))
            {
                int need = needs[block.Id];
                allocations[block.Id] = need;
                remaining -= need;
            }

            // Second pass: allocate remaining blocks by priority
            foreach (var block in ordered.Where(b => b.Truncation != TruncationStrategy.Never))
            {
                int need = needs[block.Id];

                if (remaining <= 0)
                {
                    dropped.Add(new DroppedBlock
                    {
                        Id = block.Id,
                        Reason = "No budget remaining",
                        RequestedTokens = need,
                    });
                    continue;
                }

                int allocated = Math.Min(need, remaining);

                // Check MinTokens threshold
                if (allocated < block.MinTokens)
                {
                    if (block.Truncation == TruncationStrategy.DropIfOverBudget ||
                        allocated < block.MinTokens)
                    {
                        dropped.Add(new DroppedBlock
                        {
                            Id = block.Id,
                            Reason = $"Below minimum ({allocated} < {block.MinTokens} tokens)",
                            RequestedTokens = need,
                        });
                        continue;
                    }
                }

                // DropIfOverBudget: only include if full content fits
                if (block.Truncation == TruncationStrategy.DropIfOverBudget && allocated < need)
                {
                    dropped.Add(new DroppedBlock
                    {
                        Id = block.Id,
                        Reason = $"Would require truncation ({need} > {allocated} available)",
                        RequestedTokens = need,
                    });
                    continue;
                }

                allocations[block.Id] = allocated;
                remaining -= allocated;
            }

            // Phase 3: Assemble the output
            var sb = new StringBuilder();
            var blockResults = new List<BlockResult>();
            int totalTokens = 0;
            int truncatedCount = 0;
            bool first = true;

            // Assemble in original insertion order (not priority order)
            foreach (var block in _blocks)
            {
                if (!allocations.ContainsKey(block.Id))
                    continue;

                if (!first) sb.Append(_separator);
                first = false;

                int allocation = allocations[block.Id];
                string content = block.Content;
                int labelTokens = 0;

                // Add label
                if (block.Label != null)
                {
                    sb.Append(block.Label).Append('\n');
                    labelTokens = PromptGuard.EstimateTokens(block.Label + "\n");
                }

                int contentBudget = allocation - labelTokens;
                bool wasTruncated = false;

                if (block.EstimatedTokens > contentBudget &&
                    block.Truncation != TruncationStrategy.Never)
                {
                    content = TruncateContent(content, contentBudget, block.Truncation, markerTokens);
                    wasTruncated = true;
                    truncatedCount++;
                }

                sb.Append(content);

                int finalTokens = labelTokens + PromptGuard.EstimateTokens(content);
                totalTokens += finalTokens;

                blockResults.Add(new BlockResult
                {
                    Id = block.Id,
                    WasTruncated = wasTruncated,
                    OriginalTokens = block.EstimatedTokens + labelTokens,
                    FinalTokens = finalTokens,
                    Priority = block.Priority,
                });
            }

            // Add separator tokens
            int usedSeps = Math.Max(0, blockResults.Count - 1);
            totalTokens += usedSeps * sepTokens;

            return new ContextBuildResult
            {
                Text = sb.ToString(),
                Budget = _maxTokens,
                TotalTokens = totalTokens,
                IncludedCount = blockResults.Count,
                DroppedCount = dropped.Count,
                TruncatedCount = truncatedCount,
                Blocks = blockResults.AsReadOnly(),
                Dropped = dropped.AsReadOnly(),
            };
        }

        /// <summary>
        /// Build and return just the assembled text (convenience method).
        /// </summary>
        public string BuildText() => Build().Text;

        /// <summary>
        /// Generate a human-readable summary of how blocks would be allocated.
        /// </summary>
        public string GetAllocationSummary()
        {
            var result = Build();
            var sb = new StringBuilder();
            sb.AppendLine($"=== Context Allocation Summary ===");
            sb.AppendLine($"Budget: {result.Budget} tokens");
            sb.AppendLine($"Used:   {result.TotalTokens} tokens ({result.UsagePercent:F1}%)");
            sb.AppendLine($"Remaining: {result.RemainingTokens} tokens");
            sb.AppendLine();
            sb.AppendLine($"Included ({result.IncludedCount}):");
            foreach (var block in result.Blocks)
            {
                string truncNote = block.WasTruncated ? " [TRUNCATED]" : "";
                sb.AppendLine($"  [{block.Priority:D3}] {block.Id}: " +
                    $"{block.FinalTokens}/{block.OriginalTokens} tokens{truncNote}");
            }
            if (result.Dropped.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Dropped ({result.DroppedCount}):");
                foreach (var drop in result.Dropped)
                {
                    sb.AppendLine($"  {drop.Id}: {drop.Reason} ({drop.RequestedTokens} tokens)");
                }
            }
            return sb.ToString();
        }

        // ── Truncation ─────────────────────────────────────────────────

        private string TruncateContent(string content, int targetTokens,
            TruncationStrategy strategy, int markerTokens)
        {
            if (targetTokens <= 0) return string.Empty;
            if (PromptGuard.EstimateTokens(content) <= targetTokens)
                return content;

            // Estimate chars from tokens (rough: 4 chars/token)
            int targetChars = Math.Max(1, (targetTokens - markerTokens) * 4);

            switch (strategy)
            {
                case TruncationStrategy.KeepStart:
                    return TruncateKeepStart(content, targetChars);

                case TruncationStrategy.KeepEnd:
                    return TruncateKeepEnd(content, targetChars);

                case TruncationStrategy.KeepStartAndEnd:
                    return TruncateKeepStartAndEnd(content, targetChars);

                case TruncationStrategy.DropIfOverBudget:
                    return string.Empty;

                default:
                    return content;
            }
        }

        private string TruncateKeepStart(string content, int charBudget)
        {
            if (charBudget >= content.Length) return content;
            // Find a clean break point (line or word boundary)
            int cutPoint = FindBreakPoint(content, charBudget, forward: false);
            return content[..cutPoint].TrimEnd() + _overflowMarker;
        }

        private string TruncateKeepEnd(string content, int charBudget)
        {
            if (charBudget >= content.Length) return content;
            int startPoint = content.Length - charBudget;
            startPoint = FindBreakPoint(content, startPoint, forward: true);
            return _overflowMarker + content[startPoint..].TrimStart();
        }

        private string TruncateKeepStartAndEnd(string content, int charBudget)
        {
            if (charBudget >= content.Length) return content;
            int halfBudget = charBudget / 2;
            int startEnd = FindBreakPoint(content, halfBudget, forward: false);
            int endStart = content.Length - halfBudget;
            endStart = FindBreakPoint(content, endStart, forward: true);
            if (endStart <= startEnd) return content; // overlap means it fits
            return content[..startEnd].TrimEnd() + _overflowMarker +
                   content[endStart..].TrimStart();
        }

        /// <summary>
        /// Find a clean break point near the target position.
        /// Prefers line breaks, then word breaks.
        /// </summary>
        private static int FindBreakPoint(string text, int target, bool forward)
        {
            if (target <= 0) return 0;
            if (target >= text.Length) return text.Length;

            // Look for a newline within 20% of target
            int searchRange = Math.Max(20, target / 5);

            if (forward)
            {
                // Search forward from target for a newline
                for (int i = target; i < Math.Min(text.Length, target + searchRange); i++)
                {
                    if (text[i] == '\n') return i + 1;
                }
                // Fall back to word boundary
                for (int i = target; i < Math.Min(text.Length, target + searchRange); i++)
                {
                    if (char.IsWhiteSpace(text[i])) return i + 1;
                }
            }
            else
            {
                // Search backward from target for a newline
                for (int i = Math.Min(target, text.Length - 1);
                     i > Math.Max(0, target - searchRange); i--)
                {
                    if (text[i] == '\n') return i + 1;
                }
                // Fall back to word boundary
                for (int i = Math.Min(target, text.Length - 1);
                     i > Math.Max(0, target - searchRange); i--)
                {
                    if (char.IsWhiteSpace(text[i])) return i;
                }
            }

            return target; // no clean break found
        }
    }
}
