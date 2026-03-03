namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;

    /// <summary>
    /// Status of a single batch item after processing.
    /// </summary>
    public enum BatchItemStatus
    {
        /// <summary>Item has not been processed yet.</summary>
        Pending,
        /// <summary>Item completed successfully.</summary>
        Succeeded,
        /// <summary>Item failed after all retry attempts.</summary>
        Failed,
        /// <summary>Item was skipped (e.g., due to batch cancellation).</summary>
        Skipped
    }

    /// <summary>
    /// A single item in a batch, containing the input prompt and variables,
    /// plus the result after processing.
    /// </summary>
    public class BatchItem
    {
        /// <summary>Gets the unique identifier for this item within the batch.</summary>
        public string Id { get; }

        /// <summary>Gets the prompt template to render.</summary>
        public PromptTemplate Template { get; }

        /// <summary>Gets the variables to pass to the template.</summary>
        public IReadOnlyDictionary<string, string> Variables { get; }

        /// <summary>Gets or sets the rendered prompt text after processing.</summary>
        public string? RenderedPrompt { get; internal set; }

        /// <summary>Gets or sets the current status of this item.</summary>
        public BatchItemStatus Status { get; internal set; } = BatchItemStatus.Pending;

        /// <summary>Gets or sets the error message if the item failed.</summary>
        public string? ErrorMessage { get; internal set; }

        /// <summary>Gets or sets the number of attempts made to process this item.</summary>
        public int Attempts { get; internal set; }

        /// <summary>Gets or sets the processing duration in milliseconds.</summary>
        public long DurationMs { get; internal set; }

        /// <summary>Gets or sets user-defined tags for categorization and filtering.</summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Creates a new batch item.
        /// </summary>
        /// <param name="id">Unique identifier for this item.</param>
        /// <param name="template">The prompt template to render.</param>
        /// <param name="variables">Variables for template rendering.</param>
        /// <param name="tags">Optional tags for categorization.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is null or empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="template"/> is null.
        /// </exception>
        public BatchItem(string id, PromptTemplate template,
                         Dictionary<string, string>? variables = null,
                         string[]? tags = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "Item ID cannot be null or empty.", nameof(id));
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            Id = id;
            Template = template;
            Variables = variables != null
                ? new Dictionary<string, string>(variables)
                : new Dictionary<string, string>();
            Tags = tags != null
                ? Array.AsReadOnly(tags.ToArray())
                : Array.AsReadOnly(Array.Empty<string>());
        }
    }

    /// <summary>
    /// Configuration for retry behavior when processing batch items.
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>Maximum number of retry attempts (0 = no retries).</summary>
        public int MaxRetries { get; set; }

        /// <summary>Base delay between retries in milliseconds.</summary>
        public int BaseDelayMs { get; set; } = 100;

        /// <summary>Whether to use exponential backoff (delay doubles each retry).</summary>
        public bool ExponentialBackoff { get; set; } = true;

        /// <summary>Maximum delay cap in milliseconds (prevents runaway backoff).</summary>
        public int MaxDelayMs { get; set; } = 5000;

        /// <summary>
        /// Whether to add random jitter to retry delays to prevent thundering herd.
        /// </summary>
        public bool Jitter { get; set; } = true;

        /// <summary>
        /// Returns the delay in milliseconds for a given attempt number (0-based).
        /// </summary>
        /// <param name="attempt">The attempt number (0-based).</param>
        /// <returns>Delay in milliseconds.</returns>
        public int GetDelay(int attempt)
        {
            if (attempt <= 0 || BaseDelayMs <= 0) return 0;

            int delay = BaseDelayMs;
            if (ExponentialBackoff)
            {
                // 2^attempt * base, capped at MaxDelayMs
                for (int i = 0; i < attempt && delay < MaxDelayMs; i++)
                    delay = Math.Min(delay * 2, MaxDelayMs);
            }

            delay = Math.Min(delay, MaxDelayMs);

            if (Jitter)
            {
                // Add ±25% jitter using the thread-safe shared instance
                // to avoid identical seeds when called in quick succession.
                int jitterRange = Math.Max(1, delay / 4);
                delay += Random.Shared.Next(-jitterRange, jitterRange + 1);
                delay = Math.Max(1, delay);
            }

            return delay;
        }

        /// <summary>
        /// Creates a policy with no retries.
        /// </summary>
        public static RetryPolicy None => new RetryPolicy { MaxRetries = 0 };

        /// <summary>
        /// Creates a default policy (3 retries, exponential backoff, jitter).
        /// </summary>
        public static RetryPolicy Default => new RetryPolicy
        {
            MaxRetries = 3,
            BaseDelayMs = 100,
            ExponentialBackoff = true,
            MaxDelayMs = 5000,
            Jitter = true
        };
    }

    /// <summary>
    /// Progress information reported during batch processing.
    /// </summary>
    public class BatchProgress
    {
        /// <summary>Total number of items in the batch.</summary>
        public int Total { get; set; }

        /// <summary>Number of items completed (succeeded + failed).</summary>
        public int Completed { get; set; }

        /// <summary>Number of items that succeeded.</summary>
        public int Succeeded { get; set; }

        /// <summary>Number of items that failed.</summary>
        public int Failed { get; set; }

        /// <summary>Number of items still pending.</summary>
        public int Pending => Total - Completed;

        /// <summary>Completion percentage (0–100).</summary>
        public double PercentComplete => Total == 0 ? 100.0
            : Math.Round(100.0 * Completed / Total, 1);

        /// <summary>The ID of the most recently processed item.</summary>
        public string? LastProcessedId { get; set; }
    }

    /// <summary>
    /// Aggregated result of a batch processing run.
    /// </summary>
    public class BatchResult
    {
        /// <summary>Gets all processed items.</summary>
        public IReadOnlyList<BatchItem> Items { get; }

        /// <summary>Gets the total processing duration in milliseconds.</summary>
        public long TotalDurationMs { get; }

        /// <summary>Gets the number of items that succeeded.</summary>
        public int SucceededCount { get; }

        /// <summary>Gets the number of items that failed.</summary>
        public int FailedCount { get; }

        /// <summary>Gets the number of items that were skipped.</summary>
        public int SkippedCount { get; }

        /// <summary>Gets whether all items succeeded.</summary>
        public bool AllSucceeded => FailedCount == 0 && SkippedCount == 0;

        /// <summary>Gets the success rate as a percentage (0–100).</summary>
        public double SuccessRate => Items.Count == 0 ? 100.0
            : Math.Round(100.0 * SucceededCount / Items.Count, 1);

        /// <summary>Gets the total number of retry attempts across all items.</summary>
        public int TotalRetries { get; }

        /// <summary>Gets the average processing duration per item in ms.</summary>
        public double AverageDurationMs => Items.Count == 0 ? 0
            : Math.Round((double)Items.Sum(i => i.DurationMs) / Items.Count, 1);

        /// <summary>Gets only the items that failed.</summary>
        public IReadOnlyList<BatchItem> FailedItems { get; }

        /// <summary>Gets only the items that succeeded.</summary>
        public IReadOnlyList<BatchItem> SucceededItems { get; }

        internal BatchResult(IReadOnlyList<BatchItem> items, long totalDurationMs)
        {
            Items = items;
            TotalDurationMs = totalDurationMs;
            SucceededCount = items.Count(i => i.Status == BatchItemStatus.Succeeded);
            FailedCount = items.Count(i => i.Status == BatchItemStatus.Failed);
            SkippedCount = items.Count(i => i.Status == BatchItemStatus.Skipped);
            TotalRetries = items.Sum(i => Math.Max(0, i.Attempts - 1));
            FailedItems = items.Where(i => i.Status == BatchItemStatus.Failed).ToList().AsReadOnly();
            SucceededItems = items.Where(i => i.Status == BatchItemStatus.Succeeded).ToList().AsReadOnly();
        }

        /// <summary>
        /// Returns a human-readable summary of the batch result.
        /// </summary>
        public string ToSummary()
        {
            var lines = new List<string>
            {
                "═══════════════════════════════════════════",
                "  BATCH PROCESSING RESULT",
                "═══════════════════════════════════════════",
                $"  Total items:      {Items.Count}",
                $"  Succeeded:        {SucceededCount}",
                $"  Failed:           {FailedCount}",
                $"  Skipped:          {SkippedCount}",
                $"  Success rate:     {SuccessRate}%",
                $"  Total duration:   {TotalDurationMs} ms",
                $"  Avg duration:     {AverageDurationMs} ms/item",
                $"  Total retries:    {TotalRetries}"
            };

            if (FailedItems.Count > 0)
            {
                lines.Add("───────────────────────────────────────────");
                lines.Add("  FAILED ITEMS");
                lines.Add("───────────────────────────────────────────");
                foreach (var item in FailedItems)
                {
                    lines.Add($"  [{item.Id}] {item.ErrorMessage} " +
                              $"(attempts: {item.Attempts})");
                }
            }

            lines.Add("═══════════════════════════════════════════");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Serializes the batch result to JSON.
        /// </summary>
        /// <param name="indented">Whether to indent the output.</param>
        /// <returns>JSON string representation.</returns>
        public string ToJson(bool indented = true)
        {
            var dto = new
            {
                totalItems = Items.Count,
                succeeded = SucceededCount,
                failed = FailedCount,
                skipped = SkippedCount,
                successRate = SuccessRate,
                totalDurationMs = TotalDurationMs,
                averageDurationMs = AverageDurationMs,
                totalRetries = TotalRetries,
                items = Items.Select(i => new
                {
                    id = i.Id,
                    status = i.Status.ToString().ToLowerInvariant(),
                    renderedPrompt = i.RenderedPrompt,
                    errorMessage = i.ErrorMessage,
                    attempts = i.Attempts,
                    durationMs = i.DurationMs,
                    tags = i.Tags.Count > 0 ? i.Tags : null
                })
            };
            return JsonSerializer.Serialize(dto,
                SerializationGuards.WriteOptions(indented));
        }

        /// <summary>
        /// Groups results by tag and returns counts per tag.
        /// </summary>
        public Dictionary<string, BatchProgress> GroupByTag()
        {
            var tagGroups = new Dictionary<string, List<BatchItem>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in Items)
            {
                foreach (var tag in item.Tags)
                {
                    if (!tagGroups.ContainsKey(tag))
                        tagGroups[tag] = new List<BatchItem>();
                    tagGroups[tag].Add(item);
                }
            }

            var result = new Dictionary<string, BatchProgress>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var (tag, items) in tagGroups)
            {
                result[tag] = new BatchProgress
                {
                    Total = items.Count,
                    Completed = items.Count(i => i.Status != BatchItemStatus.Pending),
                    Succeeded = items.Count(i => i.Status == BatchItemStatus.Succeeded),
                    Failed = items.Count(i => i.Status == BatchItemStatus.Failed)
                };
            }
            return result;
        }
    }

    /// <summary>
    /// Processes batches of prompt template renders with concurrency control,
    /// retry logic, progress reporting, and result aggregation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The batch processor handles the common pattern of rendering many prompts
    /// from templates, with configurable error handling and progress tracking.
    /// It supports user-defined processing functions for integration with LLM
    /// APIs or other downstream systems.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var processor = new PromptBatchProcessor();
    /// processor.RetryPolicy = RetryPolicy.Default;
    ///
    /// var template = new PromptTemplate("Translate to {{lang}}: {{text}}");
    /// processor.AddItem("t1", template, new Dictionary&lt;string, string&gt;
    /// {
    ///     ["lang"] = "French", ["text"] = "Hello"
    /// });
    /// processor.AddItem("t2", template, new Dictionary&lt;string, string&gt;
    /// {
    ///     ["lang"] = "Spanish", ["text"] = "Goodbye"
    /// });
    ///
    /// var result = processor.ProcessAll();
    /// Console.WriteLine(result.ToSummary());
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptBatchProcessor
    {
        private readonly List<BatchItem> _items = new();
        private readonly object _lock = new();
        private Action<BatchProgress>? _progressCallback;
        private Func<string, string?>? _processFunc;
        private Func<BatchItem, bool>? _filterFunc;
        private bool _stopOnFirstFailure;

        /// <summary>Maximum number of items allowed in a single batch.</summary>
        public const int MaxBatchSize = 10_000;

        /// <summary>
        /// Gets or sets the retry policy for failed items.
        /// Defaults to <see cref="RetryPolicy.None"/>.
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.None;

        /// <summary>
        /// Gets the current number of items in the batch.
        /// </summary>
        public int Count
        {
            get { lock (_lock) return _items.Count; }
        }

        /// <summary>
        /// Adds a single item to the batch.
        /// </summary>
        /// <param name="id">Unique identifier for this item.</param>
        /// <param name="template">The prompt template to render.</param>
        /// <param name="variables">Variables for template rendering.</param>
        /// <param name="tags">Optional tags for categorization.</param>
        /// <returns>This processor for fluent chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the batch size would exceed <see cref="MaxBatchSize"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when an item with the same <paramref name="id"/> already exists.
        /// </exception>
        public PromptBatchProcessor AddItem(string id, PromptTemplate template,
                                             Dictionary<string, string>? variables = null,
                                             string[]? tags = null)
        {
            lock (_lock)
            {
                if (_items.Count >= MaxBatchSize)
                    throw new InvalidOperationException(
                        $"Batch size cannot exceed {MaxBatchSize} items.");
                if (_items.Any(i => i.Id == id))
                    throw new ArgumentException(
                        $"An item with ID '{id}' already exists in the batch.", nameof(id));
                _items.Add(new BatchItem(id, template, variables, tags));
            }
            return this;
        }

        /// <summary>
        /// Adds multiple items from a list of (id, template, variables) tuples.
        /// </summary>
        /// <param name="items">Items to add.</param>
        /// <returns>This processor for fluent chaining.</returns>
        public PromptBatchProcessor AddItems(
            IEnumerable<(string id, PromptTemplate template,
                         Dictionary<string, string>? variables)> items)
        {
            foreach (var (id, template, variables) in items)
                AddItem(id, template, variables);
            return this;
        }

        /// <summary>
        /// Generates batch items from a template and a list of variable sets.
        /// Items are auto-numbered with the given prefix.
        /// </summary>
        /// <param name="template">The template to use for all items.</param>
        /// <param name="variableSets">Variable dictionaries, one per item.</param>
        /// <param name="idPrefix">Prefix for auto-generated IDs.</param>
        /// <param name="tags">Tags to apply to all generated items.</param>
        /// <returns>This processor for fluent chaining.</returns>
        public PromptBatchProcessor AddFromTemplate(
            PromptTemplate template,
            IEnumerable<Dictionary<string, string>> variableSets,
            string idPrefix = "item",
            string[]? tags = null)
        {
            int index = 0;
            foreach (var vars in variableSets)
            {
                AddItem($"{idPrefix}-{index}", template, vars, tags);
                index++;
            }
            return this;
        }

        /// <summary>
        /// Sets a callback to be invoked after each item is processed.
        /// </summary>
        /// <param name="callback">Progress callback.</param>
        /// <returns>This processor for fluent chaining.</returns>
        public PromptBatchProcessor OnProgress(Action<BatchProgress> callback)
        {
            _progressCallback = callback;
            return this;
        }

        /// <summary>
        /// Sets a processing function that transforms rendered prompts.
        /// This is where you'd call an LLM API, database, or other service.
        /// If null, items are rendered from templates only.
        /// </summary>
        /// <param name="processFunc">
        /// Function that takes a rendered prompt and returns a processed result.
        /// Throw an exception to trigger retry logic.
        /// Return null to indicate a soft failure (no retry).
        /// </param>
        /// <returns>This processor for fluent chaining.</returns>
        public PromptBatchProcessor WithProcessor(Func<string, string?> processFunc)
        {
            _processFunc = processFunc;
            return this;
        }

        /// <summary>
        /// Sets a filter function. Only items where the filter returns true
        /// will be processed; others are marked as Skipped.
        /// </summary>
        /// <param name="filter">Filter predicate.</param>
        /// <returns>This processor for fluent chaining.</returns>
        public PromptBatchProcessor WithFilter(Func<BatchItem, bool> filter)
        {
            _filterFunc = filter;
            return this;
        }

        /// <summary>
        /// If set, the batch will stop processing on the first failure
        /// and mark remaining items as Skipped.
        /// </summary>
        /// <param name="stop">Whether to stop on first failure.</param>
        /// <returns>This processor for fluent chaining.</returns>
        public PromptBatchProcessor StopOnFirstFailure(bool stop = true)
        {
            _stopOnFirstFailure = stop;
            return this;
        }

        /// <summary>
        /// Clears all items from the batch.
        /// </summary>
        public void Clear()
        {
            lock (_lock) _items.Clear();
        }

        /// <summary>
        /// Processes all items in the batch sequentially.
        /// </summary>
        /// <returns>Aggregated batch result with all items and metrics.</returns>
        public BatchResult ProcessAll()
        {
            List<BatchItem> snapshot;
            lock (_lock) snapshot = new List<BatchItem>(_items);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool shouldStop = false;
            int completed = 0;
            int succeeded = 0;
            int failed = 0;

            foreach (var item in snapshot)
            {
                if (shouldStop)
                {
                    item.Status = BatchItemStatus.Skipped;
                    continue;
                }

                // Skip items that already completed — makes ProcessAll() idempotent
                if (item.Status == BatchItemStatus.Succeeded)
                {
                    completed++;
                    succeeded++;
                    ReportProgress(snapshot.Count, completed, succeeded, failed, item.Id);
                    continue;
                }
                if (item.Status == BatchItemStatus.Skipped)
                {
                    completed++;
                    ReportProgress(snapshot.Count, completed, succeeded, failed, item.Id);
                    continue;
                }

                if (_filterFunc != null && !_filterFunc(item))
                {
                    item.Status = BatchItemStatus.Skipped;
                    completed++;
                    ReportProgress(snapshot.Count, completed, succeeded, failed, item.Id);
                    continue;
                }

                ProcessSingleItem(item);
                completed++;

                if (item.Status == BatchItemStatus.Succeeded)
                    succeeded++;
                else if (item.Status == BatchItemStatus.Failed)
                {
                    failed++;
                    if (_stopOnFirstFailure)
                        shouldStop = true;
                }

                ReportProgress(snapshot.Count, completed, succeeded, failed, item.Id);
            }

            sw.Stop();
            return new BatchResult(snapshot.AsReadOnly(), sw.ElapsedMilliseconds);
        }

        /// <summary>
        /// Processes a single item by ID and returns the item.
        /// </summary>
        /// <param name="id">The item ID to process.</param>
        /// <returns>The processed item, or null if not found.</returns>
        public BatchItem? ProcessSingle(string id)
        {
            BatchItem? item;
            lock (_lock) item = _items.FirstOrDefault(i => i.Id == id);
            if (item == null) return null;

            ProcessSingleItem(item);
            return item;
        }

        /// <summary>
        /// Gets an item by ID without processing it.
        /// </summary>
        /// <param name="id">The item ID.</param>
        /// <returns>The batch item, or null if not found.</returns>
        public BatchItem? GetItem(string id)
        {
            lock (_lock) return _items.FirstOrDefault(i => i.Id == id);
        }

        /// <summary>
        /// Gets all items matching a specific tag.
        /// </summary>
        /// <param name="tag">The tag to filter by.</param>
        /// <returns>List of matching items.</returns>
        public IReadOnlyList<BatchItem> GetItemsByTag(string tag)
        {
            lock (_lock)
                return _items.Where(i => i.Tags.Contains(tag,
                    StringComparer.OrdinalIgnoreCase)).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets current progress without modifying state.
        /// </summary>
        public BatchProgress GetProgress()
        {
            lock (_lock)
            {
                return new BatchProgress
                {
                    Total = _items.Count,
                    Completed = _items.Count(i =>
                        i.Status != BatchItemStatus.Pending),
                    Succeeded = _items.Count(i =>
                        i.Status == BatchItemStatus.Succeeded),
                    Failed = _items.Count(i =>
                        i.Status == BatchItemStatus.Failed)
                };
            }
        }

        /// <summary>
        /// Resets all failed items back to Pending so they can be retried
        /// in a subsequent <see cref="ProcessAll"/> call.
        /// </summary>
        /// <returns>Number of items reset.</returns>
        public int RetryFailed()
        {
            int count = 0;
            lock (_lock)
            {
                foreach (var item in _items)
                {
                    if (item.Status == BatchItemStatus.Failed)
                    {
                        item.Status = BatchItemStatus.Pending;
                        item.ErrorMessage = null;
                        item.Attempts = 0;
                        item.DurationMs = 0;
                        item.RenderedPrompt = null;
                        count++;
                    }
                }
            }
            return count;
        }

        // ── Private helpers ─────────────────────────────────────────

        private void ProcessSingleItem(BatchItem item)
        {
            var itemSw = System.Diagnostics.Stopwatch.StartNew();
            int maxAttempts = 1 + Math.Max(0, RetryPolicy.MaxRetries);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                item.Attempts = attempt + 1;

                if (attempt > 0)
                {
                    int delay = RetryPolicy.GetDelay(attempt);
                    if (delay > 0)
                        Thread.Sleep(delay);
                }

                try
                {
                    // Render the template
                    string rendered = item.Template.Render(
                        new Dictionary<string, string>(item.Variables));
                    item.RenderedPrompt = rendered;

                    // Apply processor function if set
                    if (_processFunc != null)
                    {
                        string? result = _processFunc(rendered);
                        if (result == null)
                        {
                            item.Status = BatchItemStatus.Failed;
                            item.ErrorMessage = "Processor returned null (soft failure).";
                            // No retry for soft failures
                            break;
                        }
                        item.RenderedPrompt = result;
                    }

                    item.Status = BatchItemStatus.Succeeded;
                    item.ErrorMessage = null;
                    break; // success, stop retrying
                }
                catch (Exception ex)
                {
                    item.ErrorMessage = ex.Message;
                    item.Status = BatchItemStatus.Failed;

                    if (attempt + 1 >= maxAttempts)
                        break; // exhausted retries
                    // else: loop continues to retry
                }
            }

            itemSw.Stop();
            item.DurationMs = itemSw.ElapsedMilliseconds;
        }

        private void ReportProgress(int total, int completed, int succeeded,
                                     int failed, string? lastId)
        {
            _progressCallback?.Invoke(new BatchProgress
            {
                Total = total,
                Completed = completed,
                Succeeded = succeeded,
                Failed = failed,
                LastProcessedId = lastId
            });
        }
    }
}
