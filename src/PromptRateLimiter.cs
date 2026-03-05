namespace Prompt
{
    using System.Collections.Concurrent;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Configuration for a rate-limited model or endpoint.
    /// </summary>
    public class RateLimitProfile
    {
        /// <summary>Gets or sets the profile name (e.g., "gpt-4", "gpt-3.5-turbo").</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the maximum requests per minute.</summary>
        public int RequestsPerMinute { get; set; } = 60;

        /// <summary>Gets or sets the maximum tokens per minute.</summary>
        public int TokensPerMinute { get; set; } = 90_000;

        /// <summary>Gets or sets the maximum concurrent requests.</summary>
        public int MaxConcurrent { get; set; } = 10;

        /// <summary>Gets or sets the priority (lower = higher priority). Default is 5.</summary>
        public int Priority { get; set; } = 5;
    }

    /// <summary>
    /// Result of attempting to acquire a rate limit permit.
    /// </summary>
    public class RateLimitResult
    {
        /// <summary>Gets whether the request was permitted.</summary>
        public bool Permitted { get; internal set; }

        /// <summary>Gets the wait time in milliseconds before retrying (0 if permitted).</summary>
        public long WaitMs { get; internal set; }

        /// <summary>Gets the reason for denial, if not permitted.</summary>
        public string? DenialReason { get; internal set; }

        /// <summary>Gets the current request count in the window.</summary>
        public int CurrentRequests { get; internal set; }

        /// <summary>Gets the current token count in the window.</summary>
        public long CurrentTokens { get; internal set; }

        /// <summary>Gets the number of concurrent requests active.</summary>
        public int ConcurrentRequests { get; internal set; }

        /// <summary>Gets the profile name this result applies to.</summary>
        public string ProfileName { get; internal set; } = "";

        /// <summary>Gets the acquisition timestamp (milliseconds since epoch).
        /// Pass this to <see cref="PromptRateLimiter.RecordCompletion"/> to
        /// correctly identify which token record to update under concurrency.</summary>
        public long AcquireTimestamp { get; internal set; }
    }

    /// <summary>
    /// Snapshot of rate limiter usage statistics for a single profile.
    /// </summary>
    public class RateLimitUsage
    {
        /// <summary>Gets the profile name.</summary>
        public string ProfileName { get; internal set; } = "";

        /// <summary>Gets the total requests made.</summary>
        public long TotalRequests { get; internal set; }

        /// <summary>Gets the total tokens consumed.</summary>
        public long TotalTokens { get; internal set; }

        /// <summary>Gets the total requests denied.</summary>
        public long DeniedRequests { get; internal set; }

        /// <summary>Gets the total requests that succeeded (completed).</summary>
        public long CompletedRequests { get; internal set; }

        /// <summary>Gets the current requests in the active window.</summary>
        public int WindowRequests { get; internal set; }

        /// <summary>Gets the current tokens in the active window.</summary>
        public long WindowTokens { get; internal set; }

        /// <summary>Gets the current number of concurrent requests.</summary>
        public int ConcurrentRequests { get; internal set; }

        /// <summary>Gets the requests-per-minute limit.</summary>
        public int RequestsPerMinuteLimit { get; internal set; }

        /// <summary>Gets the tokens-per-minute limit.</summary>
        public int TokensPerMinuteLimit { get; internal set; }

        /// <summary>Gets the max concurrent limit.</summary>
        public int MaxConcurrentLimit { get; internal set; }

        /// <summary>
        /// Gets the utilization percentage for requests (0-100).
        /// </summary>
        public double RequestUtilization =>
            RequestsPerMinuteLimit > 0
                ? Math.Min(100.0, (double)WindowRequests / RequestsPerMinuteLimit * 100.0)
                : 0.0;

        /// <summary>
        /// Gets the utilization percentage for tokens (0-100).
        /// </summary>
        public double TokenUtilization =>
            TokensPerMinuteLimit > 0
                ? Math.Min(100.0, (double)WindowTokens / TokensPerMinuteLimit * 100.0)
                : 0.0;
    }

    /// <summary>
    /// Thread-safe rate limiter for LLM API calls with per-model profiles,
    /// sliding window tracking, concurrency control, and usage reporting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var limiter = new PromptRateLimiter();
    /// limiter.AddProfile(new RateLimitProfile
    /// {
    ///     Name = "gpt-4",
    ///     RequestsPerMinute = 40,
    ///     TokensPerMinute = 40_000,
    ///     MaxConcurrent = 5
    /// });
    ///
    /// // Check if a request is allowed
    /// var result = limiter.TryAcquire("gpt-4", estimatedTokens: 500);
    /// if (result.Permitted)
    /// {
    ///     try
    ///     {
    ///         // Make API call...
    ///         limiter.RecordCompletion("gpt-4", actualTokens: 450);
    ///     }
    ///     catch
    ///     {
    ///         limiter.RecordCompletion("gpt-4", actualTokens: 0);
    ///     }
    /// }
    /// else
    /// {
    ///     Console.WriteLine($"Rate limited: {result.DenialReason}, retry in {result.WaitMs}ms");
    /// }
    ///
    /// // Async with automatic waiting
    /// await limiter.WaitAndAcquireAsync("gpt-4", estimatedTokens: 500);
    /// // ... make call ...
    /// limiter.RecordCompletion("gpt-4", actualTokens: 450);
    ///
    /// // Check usage
    /// var usage = limiter.GetUsage("gpt-4");
    /// Console.WriteLine($"Requests: {usage.TotalRequests}, Utilization: {usage.RequestUtilization:F1}%");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptRateLimiter
    {
        private readonly ConcurrentDictionary<string, ProfileState> _profiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        private class ProfileState
        {
            public RateLimitProfile Profile { get; set; } = new();
            public List<long> RequestTimestamps { get; set; } = new();
            public List<(long Timestamp, int Tokens)> TokenRecords { get; set; } = new();
            public int ConcurrentCount { get; set; }
            public long TotalRequests { get; set; }
            public long TotalTokens { get; set; }
            public long DeniedRequests { get; set; }
            public long CompletedRequests { get; set; }
        }

        /// <summary>
        /// Creates a new rate limiter instance.
        /// </summary>
        public PromptRateLimiter()
        {
        }

        /// <summary>
        /// Creates a new rate limiter with preset profiles for common models.
        /// </summary>
        /// <returns>A rate limiter with GPT-3.5-turbo, GPT-4, and GPT-4-turbo profiles.</returns>
        public static PromptRateLimiter WithDefaults()
        {
            var limiter = new PromptRateLimiter();
            limiter.AddProfile(new RateLimitProfile
            {
                Name = "gpt-3.5-turbo",
                RequestsPerMinute = 3500,
                TokensPerMinute = 90_000,
                MaxConcurrent = 50
            });
            limiter.AddProfile(new RateLimitProfile
            {
                Name = "gpt-4",
                RequestsPerMinute = 500,
                TokensPerMinute = 40_000,
                MaxConcurrent = 20
            });
            limiter.AddProfile(new RateLimitProfile
            {
                Name = "gpt-4-turbo",
                RequestsPerMinute = 500,
                TokensPerMinute = 150_000,
                MaxConcurrent = 20
            });
            limiter.AddProfile(new RateLimitProfile
            {
                Name = "gpt-4o",
                RequestsPerMinute = 500,
                TokensPerMinute = 150_000,
                MaxConcurrent = 30
            });
            limiter.AddProfile(new RateLimitProfile
            {
                Name = "claude-3-opus",
                RequestsPerMinute = 60,
                TokensPerMinute = 80_000,
                MaxConcurrent = 10
            });
            limiter.AddProfile(new RateLimitProfile
            {
                Name = "claude-3-sonnet",
                RequestsPerMinute = 120,
                TokensPerMinute = 160_000,
                MaxConcurrent = 20
            });
            return limiter;
        }

        /// <summary>
        /// Adds or updates a rate limit profile for a model.
        /// </summary>
        /// <param name="profile">The profile to add.</param>
        /// <exception cref="ArgumentNullException">Thrown if profile is null.</exception>
        /// <exception cref="ArgumentException">Thrown if profile name is empty.</exception>
        public void AddProfile(RateLimitProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.Name))
                throw new ArgumentException("Profile name cannot be empty.", nameof(profile));

            lock (_lock)
            {
                _profiles[profile.Name] = new ProfileState { Profile = profile };
            }
        }

        /// <summary>
        /// Removes a rate limit profile.
        /// </summary>
        /// <param name="profileName">The name of the profile to remove.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return false;
            return _profiles.TryRemove(profileName, out _);
        }

        /// <summary>
        /// Gets all registered profile names.
        /// </summary>
        /// <returns>List of profile names.</returns>
        public IReadOnlyList<string> GetProfileNames()
        {
            return _profiles.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the profile configuration for a model.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        /// <returns>The profile, or null if not found.</returns>
        public RateLimitProfile? GetProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return null;
            return _profiles.TryGetValue(profileName, out var state) ? state.Profile : null;
        }

        /// <summary>
        /// Attempts to acquire a permit for an API request.
        /// Does not block — returns immediately with the result.
        /// </summary>
        /// <param name="profileName">The model/profile to acquire for.</param>
        /// <param name="estimatedTokens">Estimated token count for the request.</param>
        /// <returns>A result indicating whether the request is permitted.</returns>
        public RateLimitResult TryAcquire(string profileName, int estimatedTokens = 0)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return Denied(profileName ?? "", "Profile name is required.");

            if (!_profiles.TryGetValue(profileName, out var state))
                return Denied(profileName, $"Unknown profile: {profileName}");

            lock (_lock)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                PruneWindow(state, now);

                var profile = state.Profile;

                // Check concurrent limit
                if (state.ConcurrentCount >= profile.MaxConcurrent)
                {
                    state.DeniedRequests++;
                    return new RateLimitResult
                    {
                        Permitted = false,
                        WaitMs = 1000, // suggest 1s retry
                        DenialReason = $"Concurrent limit reached ({state.ConcurrentCount}/{profile.MaxConcurrent})",
                        CurrentRequests = state.RequestTimestamps.Count,
                        CurrentTokens = WindowTokens(state),
                        ConcurrentRequests = state.ConcurrentCount,
                        ProfileName = profileName
                    };
                }

                // Check RPM limit
                if (state.RequestTimestamps.Count >= profile.RequestsPerMinute)
                {
                    var oldestMs = state.RequestTimestamps[0];
                    var waitMs = Math.Max(0, 60_000 - (now - oldestMs));
                    state.DeniedRequests++;
                    return new RateLimitResult
                    {
                        Permitted = false,
                        WaitMs = waitMs,
                        DenialReason = $"RPM limit reached ({state.RequestTimestamps.Count}/{profile.RequestsPerMinute})",
                        CurrentRequests = state.RequestTimestamps.Count,
                        CurrentTokens = WindowTokens(state),
                        ConcurrentRequests = state.ConcurrentCount,
                        ProfileName = profileName
                    };
                }

                // Check TPM limit
                var currentTokens = WindowTokens(state);
                if (estimatedTokens > 0 && currentTokens + estimatedTokens > profile.TokensPerMinute)
                {
                    long waitMs = 0;
                    if (state.TokenRecords.Count > 0)
                    {
                        var oldestTokenMs = state.TokenRecords[0].Timestamp;
                        waitMs = Math.Max(0, 60_000 - (now - oldestTokenMs));
                    }
                    state.DeniedRequests++;
                    return new RateLimitResult
                    {
                        Permitted = false,
                        WaitMs = waitMs,
                        DenialReason = $"TPM limit would be exceeded ({currentTokens + estimatedTokens}/{profile.TokensPerMinute})",
                        CurrentRequests = state.RequestTimestamps.Count,
                        CurrentTokens = currentTokens,
                        ConcurrentRequests = state.ConcurrentCount,
                        ProfileName = profileName
                    };
                }

                // Acquire
                state.RequestTimestamps.Add(now);
                if (estimatedTokens > 0)
                    state.TokenRecords.Add((now, estimatedTokens));
                state.ConcurrentCount++;
                state.TotalRequests++;
                state.TotalTokens += estimatedTokens;

                return new RateLimitResult
                {
                    Permitted = true,
                    WaitMs = 0,
                    CurrentRequests = state.RequestTimestamps.Count,
                    CurrentTokens = WindowTokens(state),
                    ConcurrentRequests = state.ConcurrentCount,
                    ProfileName = profileName,
                    AcquireTimestamp = now
                };
            }
        }

        /// <summary>
        /// Waits until a permit is available, then acquires it.
        /// Uses exponential backoff with jitter.
        /// </summary>
        /// <param name="profileName">The model/profile name.</param>
        /// <param name="estimatedTokens">Estimated token count.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="maxWaitMs">Maximum total wait time in milliseconds (default: 60000).</param>
        /// <returns>The acquisition result (always Permitted=true on success).</returns>
        /// <exception cref="TimeoutException">Thrown if max wait time is exceeded.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancelled.</exception>
        public async Task<RateLimitResult> WaitAndAcquireAsync(
            string profileName,
            int estimatedTokens = 0,
            CancellationToken cancellationToken = default,
            int maxWaitMs = 60_000)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var attempt = 0;
            var random = new Random();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = TryAcquire(profileName, estimatedTokens);
                if (result.Permitted)
                    return result;

                if (stopwatch.ElapsedMilliseconds >= maxWaitMs)
                    throw new TimeoutException(
                        $"Rate limit wait exceeded {maxWaitMs}ms for profile '{profileName}'. " +
                        $"Last denial: {result.DenialReason}");

                // Exponential backoff with jitter: base * 2^attempt + jitter
                var baseDelay = Math.Min(result.WaitMs, 5000);
                var backoff = Math.Min(baseDelay * (1L << Math.Min(attempt, 5)), 10_000);
                var jitter = random.Next(0, (int)Math.Max(1, backoff / 4));
                var delay = Math.Min(backoff + jitter, maxWaitMs - stopwatch.ElapsedMilliseconds);

                if (delay > 0)
                    await Task.Delay((int)delay, cancellationToken);

                attempt++;
            }
        }

        /// <summary>
        /// Records that a request has completed (releases the concurrency slot).
        /// Should be called in a finally block after TryAcquire returns Permitted=true.
        /// </summary>
        /// <param name="profileName">The model/profile name.</param>
        /// <param name="actualTokens">Actual tokens used. When provided, replaces
        /// the original estimate so the sliding window and lifetime totals stay
        /// accurate.  Pass 0 (default) to keep the original estimate.</param>
        /// <param name="acquireTimestamp">Timestamp from the acquire call to
        /// identify which token record to update. When 0 (default), falls back
        /// to replacing the most recent record (legacy behaviour).</param>
        public void RecordCompletion(string profileName, int actualTokens = 0, long acquireTimestamp = 0)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return;
            if (!_profiles.TryGetValue(profileName, out var state)) return;

            lock (_lock)
            {
                if (state.ConcurrentCount > 0)
                    state.ConcurrentCount--;
                state.CompletedRequests++;

                // Replace the estimated token entry with the actual value so
                // the sliding window TPM check stays correct and TotalTokens
                // reflects reality rather than doubling up.
                if (actualTokens > 0 && state.TokenRecords.Count > 0)
                {
                    // Find the matching entry by timestamp when available,
                    // otherwise fall back to the last entry.  With concurrent
                    // requests the last entry may belong to a *different*
                    // request, so timestamp-based lookup is more correct.
                    var targetIdx = -1;
                    if (acquireTimestamp > 0)
                    {
                        for (int i = state.TokenRecords.Count - 1; i >= 0; i--)
                        {
                            if (state.TokenRecords[i].Timestamp == acquireTimestamp)
                            {
                                targetIdx = i;
                                break;
                            }
                        }
                    }

                    // Fall back to last entry if no timestamp match
                    if (targetIdx < 0)
                        targetIdx = state.TokenRecords.Count - 1;

                    var (ts, estimated) = state.TokenRecords[targetIdx];
                    state.TokenRecords[targetIdx] = (ts, actualTokens);
                    // Adjust lifetime total: remove estimate, add actual
                    state.TotalTokens = state.TotalTokens - estimated + actualTokens;
                }
                else if (actualTokens > 0)
                {
                    // No prior estimate (TryAcquire was called with 0 tokens)
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    state.TokenRecords.Add((now, actualTokens));
                    state.TotalTokens += actualTokens;
                }
            }
        }

        /// <summary>
        /// Gets current usage statistics for a profile.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        /// <returns>Usage snapshot, or null if profile not found.</returns>
        public RateLimitUsage? GetUsage(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return null;
            if (!_profiles.TryGetValue(profileName, out var state)) return null;

            lock (_lock)
            {
                PruneWindow(state, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                return new RateLimitUsage
                {
                    ProfileName = profileName,
                    TotalRequests = state.TotalRequests,
                    TotalTokens = state.TotalTokens,
                    DeniedRequests = state.DeniedRequests,
                    CompletedRequests = state.CompletedRequests,
                    WindowRequests = state.RequestTimestamps.Count,
                    WindowTokens = WindowTokens(state),
                    ConcurrentRequests = state.ConcurrentCount,
                    RequestsPerMinuteLimit = state.Profile.RequestsPerMinute,
                    TokensPerMinuteLimit = state.Profile.TokensPerMinute,
                    MaxConcurrentLimit = state.Profile.MaxConcurrent
                };
            }
        }

        /// <summary>
        /// Gets usage statistics for all profiles.
        /// </summary>
        /// <returns>Dictionary of profile name to usage snapshot.</returns>
        public IReadOnlyDictionary<string, RateLimitUsage> GetAllUsage()
        {
            var result = new Dictionary<string, RateLimitUsage>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in _profiles.Keys)
            {
                var usage = GetUsage(name);
                if (usage != null)
                    result[name] = usage;
            }
            return result;
        }

        /// <summary>
        /// Generates a human-readable usage report for all profiles.
        /// </summary>
        /// <returns>Formatted report string.</returns>
        public string GenerateReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Rate Limiter Usage Report ===");
            sb.AppendLine();

            var allUsage = GetAllUsage();
            if (allUsage.Count == 0)
            {
                sb.AppendLine("No profiles configured.");
                return sb.ToString();
            }

            foreach (var (name, usage) in allUsage.OrderBy(kv => kv.Key))
            {
                sb.AppendLine($"Profile: {name}");
                sb.AppendLine($"  Requests:   {usage.TotalRequests} total, {usage.CompletedRequests} completed, {usage.DeniedRequests} denied");
                sb.AppendLine($"  Tokens:     {usage.TotalTokens:N0} total");
                sb.AppendLine($"  Window:     {usage.WindowRequests}/{usage.RequestsPerMinuteLimit} RPM ({usage.RequestUtilization:F1}%)");
                sb.AppendLine($"  Tokens:     {usage.WindowTokens:N0}/{usage.TokensPerMinuteLimit:N0} TPM ({usage.TokenUtilization:F1}%)");
                sb.AppendLine($"  Concurrent: {usage.ConcurrentRequests}/{usage.MaxConcurrentLimit}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Resets all usage counters for a profile (keeps the profile config).
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        /// <returns>True if reset, false if profile not found.</returns>
        public bool ResetUsage(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return false;
            if (!_profiles.TryGetValue(profileName, out var state)) return false;

            lock (_lock)
            {
                state.RequestTimestamps.Clear();
                state.TokenRecords.Clear();
                state.ConcurrentCount = 0;
                state.TotalRequests = 0;
                state.TotalTokens = 0;
                state.DeniedRequests = 0;
                state.CompletedRequests = 0;
            }
            return true;
        }

        /// <summary>
        /// Resets all profiles' usage counters.
        /// </summary>
        public void ResetAllUsage()
        {
            foreach (var name in _profiles.Keys.ToList())
                ResetUsage(name);
        }

        /// <summary>
        /// Serializes the rate limiter configuration (profiles only) to JSON.
        /// </summary>
        /// <returns>JSON string of all profiles.</returns>
        public string ToJson()
        {
            var profiles = _profiles.Values
                .Select(s => s.Profile)
                .OrderBy(p => p.Name)
                .ToList();
            return JsonSerializer.Serialize(profiles, SerializationGuards.WriteIndented);
        }

        /// <summary>
        /// Creates a rate limiter from a JSON profile configuration.
        /// </summary>
        /// <param name="json">JSON string of profiles array.</param>
        /// <returns>A new rate limiter with the deserialized profiles.</returns>
        public static PromptRateLimiter FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty.", nameof(json));

            var profiles = JsonSerializer.Deserialize<List<RateLimitProfile>>(json, SerializationGuards.WriteIndented);
            var limiter = new PromptRateLimiter();
            if (profiles != null)
            {
                foreach (var profile in profiles)
                    limiter.AddProfile(profile);
            }
            return limiter;
        }

        // --- Private helpers ---

        private static RateLimitResult Denied(string profileName, string reason) =>
            new RateLimitResult
            {
                Permitted = false,
                WaitMs = 1000,
                DenialReason = reason,
                ProfileName = profileName
            };

        private static void PruneWindow(ProfileState state, long nowMs)
        {
            var cutoff = nowMs - 60_000; // 1-minute sliding window

            // Remove expired request timestamps
            var idx = 0;
            while (idx < state.RequestTimestamps.Count && state.RequestTimestamps[idx] < cutoff)
                idx++;
            if (idx > 0)
                state.RequestTimestamps.RemoveRange(0, idx);

            // Remove expired token records
            idx = 0;
            while (idx < state.TokenRecords.Count && state.TokenRecords[idx].Timestamp < cutoff)
                idx++;
            if (idx > 0)
                state.TokenRecords.RemoveRange(0, idx);
        }

        private static long WindowTokens(ProfileState state) =>
            state.TokenRecords.Sum(r => (long)r.Tokens);
    }
}
