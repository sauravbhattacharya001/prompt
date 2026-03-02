namespace Prompt
{
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Statistics for cache operations.
    /// </summary>
    public class CacheStats
    {
        /// <summary>Gets the number of cache hits.</summary>
        public long Hits { get; internal set; }

        /// <summary>Gets the number of cache misses.</summary>
        public long Misses { get; internal set; }

        /// <summary>Gets the number of entries evicted (LRU or expired).</summary>
        public long Evictions { get; internal set; }

        /// <summary>Gets the current number of entries in the cache.</summary>
        public int Count { get; internal set; }

        /// <summary>Gets the maximum capacity of the cache.</summary>
        public int Capacity { get; internal set; }

        /// <summary>
        /// Gets the hit rate as a percentage (0-100).
        /// Returns 0 if no lookups have been performed.
        /// </summary>
        public double HitRate
        {
            get
            {
                long total = Hits + Misses;
                return total == 0 ? 0.0 : Math.Round((double)Hits / total * 100, 2);
            }
        }

        /// <summary>Returns a human-readable summary of cache statistics.</summary>
        public override string ToString()
            => $"Hits: {Hits}, Misses: {Misses}, Hit Rate: {HitRate}%, Count: {Count}/{Capacity}, Evictions: {Evictions}";
    }

    /// <summary>
    /// A cached entry containing the response and metadata.
    /// </summary>
    public class CacheEntry
    {
        /// <summary>Gets the cached response text.</summary>
        public string Response { get; internal set; } = "";

        /// <summary>Gets the prompt that produced this response.</summary>
        public string Prompt { get; internal set; } = "";

        /// <summary>Gets the model name used, if specified.</summary>
        public string? Model { get; internal set; }

        /// <summary>Gets when this entry was created.</summary>
        public DateTimeOffset CreatedAt { get; internal set; }

        /// <summary>Gets when this entry was last accessed.</summary>
        public DateTimeOffset LastAccessedAt { get; internal set; }

        /// <summary>Gets how many times this entry has been accessed.</summary>
        public int AccessCount { get; internal set; }

        /// <summary>Gets optional metadata stored with the entry.</summary>
        public Dictionary<string, string>? Metadata { get; internal set; }

        /// <summary>Gets when this entry expires (null = no expiration).</summary>
        public DateTimeOffset? ExpiresAt { get; internal set; }

        /// <summary>Returns true if this entry has expired.</summary>
        internal bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow >= ExpiresAt.Value;
    }

    /// <summary>
    /// Thread-safe LRU cache for LLM prompt responses with optional TTL expiration
    /// and file-based persistence.
    /// <para>
    /// Caching responses avoids redundant API calls for identical prompts, saving
    /// both time and cost during development, testing, and production use.
    /// </para>
    /// <example>
    /// <code>
    /// // Create a cache with capacity 100 and 1-hour TTL
    /// var cache = new PromptCache(capacity: 100, defaultTtl: TimeSpan.FromHours(1));
    ///
    /// // Check cache before calling the API
    /// string prompt = "Summarize this article: ...";
    /// var cached = cache.Get(prompt);
    /// if (cached != null)
    /// {
    ///     Console.WriteLine($"Cache hit: {cached.Response}");
    /// }
    /// else
    /// {
    ///     string response = await GetLlmResponse(prompt);
    ///     cache.Put(prompt, response);
    /// }
    ///
    /// // Check statistics
    /// Console.WriteLine(cache.GetStats());
    ///
    /// // Persist to disk
    /// await cache.SaveToFileAsync("prompt-cache.json");
    /// var loaded = await PromptCache.LoadFromFileAsync("prompt-cache.json");
    /// </code>
    /// </example>
    /// </summary>
    public class PromptCache
    {
        private readonly int _capacity;
        private readonly TimeSpan? _defaultTtl;
        private readonly object _lock = new();
        private readonly Dictionary<string, LinkedListNode<KeyedCacheEntry>> _map;
        private readonly LinkedList<KeyedCacheEntry> _order; // most-recent at First
        private long _hits;
        private long _misses;
        private long _evictions;

        /// <summary>
        /// Creates a new prompt cache.
        /// </summary>
        /// <param name="capacity">
        /// Maximum number of entries. When exceeded, the least-recently-used entry
        /// is evicted. Must be at least 1. Default is 256.
        /// </param>
        /// <param name="defaultTtl">
        /// Default time-to-live for entries. Null means entries never expire.
        /// Can be overridden per-entry in <see cref="Put"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="capacity"/> is less than 1.
        /// </exception>
        public PromptCache(int capacity = 256, TimeSpan? defaultTtl = null)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity),
                    "Capacity must be at least 1.");

            if (defaultTtl.HasValue && defaultTtl.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(defaultTtl),
                    "TTL must be positive.");

            _capacity = capacity;
            _defaultTtl = defaultTtl;
            _map = new Dictionary<string, LinkedListNode<KeyedCacheEntry>>(capacity);
            _order = new LinkedList<KeyedCacheEntry>();
        }

        /// <summary>Gets the maximum capacity of this cache.</summary>
        public int Capacity => _capacity;

        /// <summary>Gets the current number of entries.</summary>
        public int Count
        {
            get { lock (_lock) { return _map.Count; } }
        }

        /// <summary>Gets the default TTL, or null if entries don't expire.</summary>
        public TimeSpan? DefaultTtl => _defaultTtl;

        /// <summary>
        /// Generates a deterministic cache key from a prompt string and optional model name.
        /// Uses SHA-256 for collision resistance.
        /// </summary>
        /// <param name="prompt">The prompt text.</param>
        /// <param name="model">Optional model name to include in the key.</param>
        /// <returns>A hex-encoded SHA-256 hash.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="prompt"/> is null.
        /// </exception>
        public static string ComputeKey(string prompt, string? model = null)
        {
            if (prompt == null)
                throw new ArgumentNullException(nameof(prompt));

            string input = model != null ? $"{model}::{prompt}" : prompt;
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Retrieves a cached response for the given prompt.
        /// Returns null on cache miss or if the entry has expired.
        /// Moves the entry to the most-recently-used position on hit.
        /// </summary>
        /// <param name="prompt">The prompt text to look up.</param>
        /// <param name="model">Optional model name (must match what was used in Put).</param>
        /// <returns>The cached entry, or null if not found or expired.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="prompt"/> is null.
        /// </exception>
        public CacheEntry? Get(string prompt, string? model = null)
        {
            if (prompt == null)
                throw new ArgumentNullException(nameof(prompt));

            string key = ComputeKey(prompt, model);

            lock (_lock)
            {
                if (!_map.TryGetValue(key, out var node))
                {
                    _misses++;
                    return null;
                }

                // Check expiration
                if (node.Value.Entry.IsExpired)
                {
                    _order.Remove(node);
                    _map.Remove(key);
                    _evictions++;
                    _misses++;
                    return null;
                }

                // Move to front (most recently used)
                _order.Remove(node);
                _order.AddFirst(node);

                node.Value.Entry.LastAccessedAt = DateTimeOffset.UtcNow;
                node.Value.Entry.AccessCount++;
                _hits++;

                return node.Value.Entry;
            }
        }

        /// <summary>
        /// Stores a prompt-response pair in the cache. If the cache is full,
        /// the least-recently-used entry is evicted first.
        /// </summary>
        /// <param name="prompt">The prompt text (used as the cache key).</param>
        /// <param name="response">The LLM response to cache.</param>
        /// <param name="model">Optional model name to include in the key.</param>
        /// <param name="ttl">
        /// Override TTL for this entry. If null, the cache's default TTL is used.
        /// Use <see cref="TimeSpan.MaxValue"/> to prevent expiration for a specific entry.
        /// </param>
        /// <param name="metadata">Optional key-value metadata to store with the entry.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="prompt"/> or <paramref name="response"/> is null.
        /// </exception>
        public void Put(string prompt, string response, string? model = null,
            TimeSpan? ttl = null, Dictionary<string, string>? metadata = null)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (response == null) throw new ArgumentNullException(nameof(response));

            string key = ComputeKey(prompt, model);
            var effectiveTtl = ttl ?? _defaultTtl;

            var entry = new CacheEntry
            {
                Prompt = prompt,
                Response = response,
                Model = model,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow,
                AccessCount = 1,
                Metadata = metadata != null ? new Dictionary<string, string>(metadata) : null,
                ExpiresAt = effectiveTtl.HasValue && effectiveTtl.Value != TimeSpan.MaxValue
                    ? DateTimeOffset.UtcNow + effectiveTtl.Value
                    : null
            };

            lock (_lock)
            {
                // Update existing entry
                if (_map.TryGetValue(key, out var existing))
                {
                    _order.Remove(existing);
                    existing.Value = new KeyedCacheEntry(key, entry);
                    _order.AddFirst(existing);
                    return;
                }

                // Evict LRU if at capacity
                while (_map.Count >= _capacity)
                {
                    var lru = _order.Last;
                    if (lru != null)
                    {
                        _order.RemoveLast();
                        _map.Remove(lru.Value.Key);
                        _evictions++;
                    }
                }

                var keyed = new KeyedCacheEntry(key, entry);
                var node = new LinkedListNode<KeyedCacheEntry>(keyed);
                _order.AddFirst(node);
                _map[key] = node;
            }
        }

        /// <summary>
        /// Removes a specific entry from the cache.
        /// </summary>
        /// <param name="prompt">The prompt text to remove.</param>
        /// <param name="model">Optional model name.</param>
        /// <returns>True if the entry was found and removed.</returns>
        public bool Remove(string prompt, string? model = null)
        {
            if (prompt == null)
                throw new ArgumentNullException(nameof(prompt));

            string key = ComputeKey(prompt, model);

            lock (_lock)
            {
                if (!_map.TryGetValue(key, out var node))
                    return false;

                _order.Remove(node);
                _map.Remove(key);
                return true;
            }
        }

        /// <summary>
        /// Checks whether the cache contains a non-expired entry for the given prompt.
        /// Does not count as a hit/miss and does not affect LRU ordering.
        /// </summary>
        /// <param name="prompt">The prompt text to check.</param>
        /// <param name="model">Optional model name.</param>
        /// <returns>True if a non-expired entry exists.</returns>
        public bool Contains(string prompt, string? model = null)
        {
            if (prompt == null)
                throw new ArgumentNullException(nameof(prompt));

            string key = ComputeKey(prompt, model);

            lock (_lock)
            {
                if (!_map.TryGetValue(key, out var node))
                    return false;
                return !node.Value.Entry.IsExpired;
            }
        }

        /// <summary>
        /// Removes all expired entries from the cache.
        /// </summary>
        /// <returns>The number of entries removed.</returns>
        public int PurgeExpired()
        {
            lock (_lock)
            {
                var expired = new List<string>();
                foreach (var kvp in _map)
                {
                    if (kvp.Value.Value.Entry.IsExpired)
                        expired.Add(kvp.Key);
                }

                foreach (var key in expired)
                {
                    if (_map.TryGetValue(key, out var node))
                    {
                        _order.Remove(node);
                        _map.Remove(key);
                        _evictions++;
                    }
                }

                return expired.Count;
            }
        }

        /// <summary>
        /// Removes all entries from the cache and resets statistics.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _map.Clear();
                _order.Clear();
                _hits = 0;
                _misses = 0;
                _evictions = 0;
            }
        }

        /// <summary>
        /// Returns a snapshot of current cache statistics.
        /// </summary>
        /// <returns>A <see cref="CacheStats"/> with current metrics.</returns>
        public CacheStats GetStats()
        {
            lock (_lock)
            {
                return new CacheStats
                {
                    Hits = _hits,
                    Misses = _misses,
                    Evictions = _evictions,
                    Count = _map.Count,
                    Capacity = _capacity
                };
            }
        }

        /// <summary>
        /// Returns all non-expired entries in most-recently-used order.
        /// </summary>
        /// <returns>An enumerable of cache entries.</returns>
        public IReadOnlyList<CacheEntry> GetAll()
        {
            lock (_lock)
            {
                var result = new List<CacheEntry>();
                foreach (var keyed in _order)
                {
                    if (!keyed.Entry.IsExpired)
                        result.Add(keyed.Entry);
                }
                return result;
            }
        }

        /// <summary>
        /// Serializes the cache to a JSON string.
        /// Only non-expired entries are included.
        /// </summary>
        /// <param name="indented">Whether to indent the JSON output.</param>
        /// <returns>A JSON string representing the cache state.</returns>
        public string ToJson(bool indented = true)
        {
            List<CacheEntryDto> entries;

            lock (_lock)
            {
                entries = new List<CacheEntryDto>();
                foreach (var keyed in _order)
                {
                    if (keyed.Entry.IsExpired) continue;
                    entries.Add(new CacheEntryDto
                    {
                        Prompt = keyed.Entry.Prompt,
                        Response = keyed.Entry.Response,
                        Model = keyed.Entry.Model,
                        CreatedAt = keyed.Entry.CreatedAt,
                        LastAccessedAt = keyed.Entry.LastAccessedAt,
                        AccessCount = keyed.Entry.AccessCount,
                        Metadata = keyed.Entry.Metadata,
                        ExpiresAt = keyed.Entry.ExpiresAt
                    });
                }
            }

            var dto = new CacheDto
            {
                Capacity = _capacity,
                DefaultTtlMs = _defaultTtl.HasValue ? (long)_defaultTtl.Value.TotalMilliseconds : null,
                Entries = entries
            };

            return JsonSerializer.Serialize(dto, SerializationGuards.WriteOptions(indented));
        }

        /// <summary>
        /// Deserializes a cache from a JSON string.
        /// Expired entries in the JSON are silently skipped.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A new <see cref="PromptCache"/> populated from the JSON.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="json"/> is null.
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown when the JSON is malformed.
        /// </exception>
        public static PromptCache FromJson(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            var dto = JsonSerializer.Deserialize<CacheDto>(json, SerializationGuards.ReadCamelCase);

            if (dto == null)
                throw new JsonException("Failed to deserialize cache JSON.");

            TimeSpan? ttl = dto.DefaultTtlMs.HasValue
                ? TimeSpan.FromMilliseconds(dto.DefaultTtlMs.Value)
                : null;

            var cache = new PromptCache(dto.Capacity > 0 ? dto.Capacity : 256, ttl);

            if (dto.Entries != null)
            {
                // JSON entries are in MRU-first order (index 0 = most recently used).
                // We load the first `capacity` non-expired entries, adding in
                // reverse so that index 0 ends up at the front of the linked list
                // (most recently used position).

                // First pass: collect up to `capacity` non-expired entries
                // from the front of the list (MRU end) to preserve recency.
                var toLoad = new List<(string key, CacheEntry entry)>();
                foreach (var e in dto.Entries)
                {
                    if (e.ExpiresAt.HasValue && DateTimeOffset.UtcNow >= e.ExpiresAt.Value)
                        continue; // Skip expired

                    var entry = new CacheEntry
                    {
                        Prompt = e.Prompt ?? "",
                        Response = e.Response ?? "",
                        Model = e.Model,
                        CreatedAt = e.CreatedAt,
                        LastAccessedAt = e.LastAccessedAt,
                        AccessCount = e.AccessCount,
                        Metadata = e.Metadata,
                        ExpiresAt = e.ExpiresAt
                    };

                    string key = ComputeKey(entry.Prompt, entry.Model);
                    toLoad.Add((key, entry));

                    if (toLoad.Count >= cache._capacity)
                        break; // Capacity reached — drop remaining (LRU) entries
                }

                // Second pass: insert in reverse order so that index 0 (MRU)
                // is AddFirst'd last, placing it at the front of the list.
                lock (cache._lock)
                {
                    for (int i = toLoad.Count - 1; i >= 0; i--)
                    {
                        var (key, entry) = toLoad[i];
                        if (!cache._map.ContainsKey(key))
                        {
                            var keyed = new KeyedCacheEntry(key, entry);
                            var node = new LinkedListNode<KeyedCacheEntry>(keyed);
                            cache._order.AddFirst(node);
                            cache._map[key] = node;
                        }
                    }
                }
            }

            return cache;
        }

        /// <summary>
        /// Saves the cache to a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the output file.</param>
        /// <param name="indented">Whether to indent the JSON output.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task SaveToFileAsync(string filePath, bool indented = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));

            string json = ToJson(indented);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
        }

        /// <summary>
        /// Loads a cache from a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the JSON file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A new <see cref="PromptCache"/> populated from the file.</returns>
        public static async Task<PromptCache> LoadFromFileAsync(string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));

            string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
            return FromJson(json);
        }

        // ---- Internal types ----

        /// <summary>
        /// Pairs a cache key with its entry so we can remove from the map
        /// in O(1) when evicting the LRU node.
        /// </summary>
        internal sealed class KeyedCacheEntry
        {
            public string Key { get; }
            public CacheEntry Entry { get; }

            public KeyedCacheEntry(string key, CacheEntry entry)
            {
                Key = key;
                Entry = entry;
            }
        }

        internal class CacheDto
        {
            public int Capacity { get; set; }
            public long? DefaultTtlMs { get; set; }
            public List<CacheEntryDto>? Entries { get; set; }
        }

        internal class CacheEntryDto
        {
            public string? Prompt { get; set; }
            public string? Response { get; set; }
            public string? Model { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset LastAccessedAt { get; set; }
            public int AccessCount { get; set; }
            public Dictionary<string, string>? Metadata { get; set; }
            public DateTimeOffset? ExpiresAt { get; set; }
        }
    }
}
