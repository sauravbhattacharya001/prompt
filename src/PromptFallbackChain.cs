namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Configuration for a single model tier in a <see cref="PromptFallbackChain"/>.
    /// </summary>
    /// <remarks>
    /// Each tier represents an Azure OpenAI deployment that the chain will
    /// try in priority order.  Override the environment variables to point
    /// at a different deployment (endpoint, key, model) per tier.
    /// </remarks>
    public class FallbackTier
    {
        /// <summary>Human-readable name for this tier (e.g. "gpt-4-turbo").</summary>
        public string Name { get; set; } = "";

        /// <summary>Azure OpenAI endpoint URI.  When <c>null</c>, the chain
        /// uses the ambient <c>AZURE_OPENAI_API_URI</c> variable.</summary>
        public string? EndpointUri { get; set; }

        /// <summary>Azure OpenAI API key.  When <c>null</c>, the chain
        /// uses the ambient <c>AZURE_OPENAI_API_KEY</c> variable.</summary>
        public string? ApiKey { get; set; }

        /// <summary>Deployed model name.  When <c>null</c>, the chain
        /// uses the ambient <c>AZURE_OPENAI_API_MODEL</c> variable.</summary>
        public string? Model { get; set; }

        /// <summary>Per-tier timeout.  When <c>null</c>, no timeout is applied
        /// beyond the caller's <see cref="CancellationToken"/>.</summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>Maximum retries for this tier (default 1 — fail fast to
        /// reach the next tier quickly).</summary>
        public int MaxRetries { get; set; } = 1;

        /// <summary>Optional <see cref="PromptOptions"/> for this tier.
        /// When <c>null</c>, the chain-level options are used.</summary>
        public PromptOptions? Options { get; set; }

        /// <summary>Priority order (lower = tried first).  Ties are broken
        /// by insertion order.</summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Result of a <see cref="PromptFallbackChain"/> execution, including
    /// which tier responded and telemetry for every attempted tier.
    /// </summary>
    public class FallbackResult
    {
        /// <summary>The final response text, or <c>null</c> if all tiers failed.</summary>
        public string? Response { get; set; }

        /// <summary>Whether any tier produced a successful response.</summary>
        public bool Success { get; set; }

        /// <summary>Name of the tier that provided the response.</summary>
        public string? RespondingTier { get; set; }

        /// <summary>0-based index of the tier in the priority-ordered list.</summary>
        public int TierIndex { get; set; } = -1;

        /// <summary>Total number of fallback transitions (0 means first tier succeeded).</summary>
        public int FallbackCount { get; set; }

        /// <summary>Total wall-clock time across all tier attempts.</summary>
        public TimeSpan TotalLatency { get; set; }

        /// <summary>Per-tier attempt telemetry, in execution order.</summary>
        public List<TierAttempt> Attempts { get; set; } = new();
    }

    /// <summary>
    /// Telemetry for a single tier attempt within a fallback execution.
    /// </summary>
    public class TierAttempt
    {
        /// <summary>Name of the tier that was attempted.</summary>
        public string TierName { get; set; } = "";

        /// <summary>Whether this attempt succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Wall-clock duration of this attempt.</summary>
        public TimeSpan Latency { get; set; }

        /// <summary>Error message if the attempt failed.</summary>
        public string? Error { get; set; }

        /// <summary>The response text if successful.</summary>
        public string? Response { get; set; }
    }

    /// <summary>
    /// Optional quality gate: given a response, returns <c>true</c> if the
    /// response meets quality requirements, <c>false</c> to reject it and
    /// try the next tier.
    /// </summary>
    /// <param name="response">The model's response text.</param>
    /// <param name="tierName">The tier that produced the response.</param>
    /// <returns><c>true</c> to accept, <c>false</c> to reject and continue.</returns>
    public delegate bool QualityGate(string response, string tierName);

    /// <summary>
    /// Executes a prompt against a priority-ordered list of model tiers,
    /// automatically falling back to the next tier on failure, timeout,
    /// or quality-gate rejection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Production LLM applications need resilience: primary models may be
    /// rate-limited, overloaded, or returning low-quality results.
    /// <c>PromptFallbackChain</c> wraps this pattern into a single call.
    /// </para>
    /// <para><b>Usage example:</b></para>
    /// <code>
    /// var chain = new PromptFallbackChain()
    ///     .AddTier(new FallbackTier
    ///     {
    ///         Name = "gpt-4-turbo",
    ///         Model = "gpt-4-turbo",
    ///         Timeout = TimeSpan.FromSeconds(30),
    ///         Priority = 0,
    ///     })
    ///     .AddTier(new FallbackTier
    ///     {
    ///         Name = "gpt-35-turbo",
    ///         Model = "gpt-35-turbo",
    ///         Timeout = TimeSpan.FromSeconds(10),
    ///         Priority = 1,
    ///         Options = PromptOptions.ForDataExtraction(),
    ///     });
    ///
    /// var result = await chain.ExecuteAsync("Summarize this document...");
    /// Console.WriteLine($"Responded by: {result.RespondingTier}");
    /// Console.WriteLine($"Fallbacks: {result.FallbackCount}");
    /// Console.WriteLine(result.Response);
    /// </code>
    /// </remarks>
    public class PromptFallbackChain
    {
        private readonly List<FallbackTier> _tiers = new();
        private QualityGate? _qualityGate;
        private PromptOptions? _defaultOptions;
        private int _maxTotalAttempts = 10;

        /// <summary>
        /// Creates an empty fallback chain.  Add tiers with <see cref="AddTier"/>.
        /// </summary>
        public PromptFallbackChain() { }

        /// <summary>
        /// Adds a tier to the chain.  Tiers are tried in ascending
        /// <see cref="FallbackTier.Priority"/> order; ties are broken
        /// by insertion order.
        /// </summary>
        /// <returns>This chain instance for fluent configuration.</returns>
        public PromptFallbackChain AddTier(FallbackTier tier)
        {
            if (tier == null) throw new ArgumentNullException(nameof(tier));
            if (string.IsNullOrWhiteSpace(tier.Name))
                throw new ArgumentException("Tier must have a non-empty name.", nameof(tier));
            if (_tiers.Any(t => t.Name == tier.Name))
                throw new ArgumentException($"A tier named '{tier.Name}' already exists.", nameof(tier));
            _tiers.Add(tier);
            return this;
        }

        /// <summary>
        /// Removes a tier by name.
        /// </summary>
        /// <returns><c>true</c> if the tier was found and removed.</returns>
        public bool RemoveTier(string name)
        {
            return _tiers.RemoveAll(t => t.Name == name) > 0;
        }

        /// <summary>
        /// Sets the default <see cref="PromptOptions"/> used when a tier
        /// does not specify its own.
        /// </summary>
        /// <returns>This chain instance for fluent configuration.</returns>
        public PromptFallbackChain WithOptions(PromptOptions options)
        {
            _defaultOptions = options;
            return this;
        }

        /// <summary>
        /// Sets an optional quality gate that evaluates each response before
        /// accepting it.  If the gate returns <c>false</c>, the chain moves
        /// to the next tier.
        /// </summary>
        /// <returns>This chain instance for fluent configuration.</returns>
        public PromptFallbackChain WithQualityGate(QualityGate gate)
        {
            _qualityGate = gate;
            return this;
        }

        /// <summary>
        /// Sets the maximum total attempts across all tiers (default 10).
        /// Prevents infinite loops when every tier fails.
        /// </summary>
        /// <returns>This chain instance for fluent configuration.</returns>
        public PromptFallbackChain WithMaxAttempts(int max)
        {
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max), "Must be at least 1.");
            _maxTotalAttempts = max;
            return this;
        }

        /// <summary>
        /// Returns the number of configured tiers.
        /// </summary>
        public int TierCount => _tiers.Count;

        /// <summary>
        /// Returns the configured tier names in priority order.
        /// </summary>
        public IReadOnlyList<string> TierNames =>
            _tiers.OrderBy(t => t.Priority).ThenBy(t => _tiers.IndexOf(t))
                  .Select(t => t.Name).ToList().AsReadOnly();

        /// <summary>
        /// Executes the prompt against tiers in priority order, falling
        /// back on failure, timeout, or quality-gate rejection.
        /// </summary>
        /// <param name="prompt">The user prompt to send.</param>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="cancellationToken">Cancellation token (cancels the entire chain).</param>
        /// <returns>A <see cref="FallbackResult"/> with the response and telemetry.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no tiers are configured.</exception>
        /// <exception cref="ArgumentException">Thrown when the prompt is null or empty.</exception>
        public async Task<FallbackResult> ExecuteAsync(
            string prompt,
            string? systemPrompt = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            if (_tiers.Count == 0)
                throw new InvalidOperationException("No tiers configured. Add at least one tier with AddTier().");

            var totalSw = Stopwatch.StartNew();
            var result = new FallbackResult();
            int totalAttempts = 0;

            // Sort by priority, then insertion order
            var sortedTiers = _tiers
                .Select((t, i) => (tier: t, index: i))
                .OrderBy(x => x.tier.Priority)
                .ThenBy(x => x.index)
                .Select(x => x.tier)
                .ToList();

            for (int i = 0; i < sortedTiers.Count && totalAttempts < _maxTotalAttempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tier = sortedTiers[i];
                totalAttempts++;
                var attempt = new TierAttempt { TierName = tier.Name };
                var sw = Stopwatch.StartNew();

                try
                {
                    string? response = await ExecuteTierAsync(
                        tier, prompt, systemPrompt, cancellationToken);

                    sw.Stop();
                    attempt.Latency = sw.Elapsed;

                    if (response == null)
                    {
                        attempt.Success = false;
                        attempt.Error = "Model returned null response.";
                        result.Attempts.Add(attempt);
                        continue;
                    }

                    // Quality gate check
                    if (_qualityGate != null && !_qualityGate(response, tier.Name))
                    {
                        attempt.Success = false;
                        attempt.Error = "Response rejected by quality gate.";
                        attempt.Response = response;
                        result.Attempts.Add(attempt);
                        continue;
                    }

                    // Success
                    attempt.Success = true;
                    attempt.Response = response;
                    result.Attempts.Add(attempt);

                    result.Response = response;
                    result.Success = true;
                    result.RespondingTier = tier.Name;
                    result.TierIndex = i;
                    result.FallbackCount = i;
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // Propagate caller cancellation
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    attempt.Latency = sw.Elapsed;
                    attempt.Success = false;
                    attempt.Error = ex.Message;
                    result.Attempts.Add(attempt);
                    // Fall through to next tier
                }
            }

            totalSw.Stop();
            result.TotalLatency = totalSw.Elapsed;
            return result;
        }

        /// <summary>
        /// Executes a single tier with its specific configuration.
        /// Sets environment variables temporarily to redirect <see cref="Main"/>
        /// to the tier's endpoint/key/model.
        /// </summary>
        private async Task<string?> ExecuteTierAsync(
            FallbackTier tier,
            string prompt,
            string? systemPrompt,
            CancellationToken cancellationToken)
        {
            // Store originals
            var origUri = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_URI");
            var origKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
            var origModel = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_MODEL");

            try
            {
                // Override env vars if tier provides custom values
                if (tier.EndpointUri != null)
                    Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", tier.EndpointUri);
                if (tier.ApiKey != null)
                    Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", tier.ApiKey);
                if (tier.Model != null)
                    Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", tier.Model);

                // Force client recreation so it picks up the new env vars
                Main.ResetClient();

                var options = tier.Options ?? _defaultOptions;

                // Apply per-tier timeout if configured
                CancellationToken token = cancellationToken;
                CancellationTokenSource? cts = null;
                if (tier.Timeout.HasValue)
                {
                    cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(tier.Timeout.Value);
                    token = cts.Token;
                }

                try
                {
                    return await Main.GetResponseAsync(
                        prompt,
                        systemPrompt,
                        maxRetries: tier.MaxRetries,
                        options: options,
                        cancellationToken: token);
                }
                catch (OperationCanceledException) when (
                    cts != null && cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Per-tier timeout — convert to a descriptive exception
                    throw new TimeoutException(
                        $"Tier '{tier.Name}' timed out after {tier.Timeout.Value.TotalSeconds:F1}s.");
                }
                finally
                {
                    cts?.Dispose();
                }
            }
            finally
            {
                // Restore original env vars
                Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", origUri);
                Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", origKey);
                Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", origModel);
                Main.ResetClient();
            }
        }

        /// <summary>
        /// Serialises the chain configuration (tier names, priorities, timeouts)
        /// and a result to JSON for logging or auditing.
        /// </summary>
        public string ToJson(FallbackResult? result = null, int indent = 2)
        {
            var obj = new Dictionary<string, object>
            {
                ["tierCount"] = _tiers.Count,
                ["maxTotalAttempts"] = _maxTotalAttempts,
                ["hasQualityGate"] = _qualityGate != null,
                ["tiers"] = _tiers
                    .OrderBy(t => t.Priority)
                    .Select(t => new Dictionary<string, object?>
                    {
                        ["name"] = t.Name,
                        ["priority"] = t.Priority,
                        ["timeout"] = t.Timeout?.TotalSeconds,
                        ["maxRetries"] = t.MaxRetries,
                        ["model"] = t.Model,
                    }).ToList(),
            };

            if (result != null)
            {
                obj["result"] = new Dictionary<string, object?>
                {
                    ["success"] = result.Success,
                    ["respondingTier"] = result.RespondingTier,
                    ["tierIndex"] = result.TierIndex,
                    ["fallbackCount"] = result.FallbackCount,
                    ["totalLatencyMs"] = result.TotalLatency.TotalMilliseconds,
                    ["attempts"] = result.Attempts.Select(a => new Dictionary<string, object?>
                    {
                        ["tier"] = a.TierName,
                        ["success"] = a.Success,
                        ["latencyMs"] = a.Latency.TotalMilliseconds,
                        ["error"] = a.Error,
                    }).ToList(),
                };
            }

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = indent > 0,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
        }
    }
}
