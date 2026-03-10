namespace Prompt.Tests
{
    using System.Text.Json;
    using Xunit;

    public class PromptCacheTests
    {
        // ================================================================
        // Constructor
        // ================================================================

        [Fact]
        public void Constructor_DefaultCapacity_Is256()
        {
            var cache = new PromptCache();
            Assert.Equal(256, cache.Capacity);
            Assert.Equal(0, cache.Count);
            Assert.Null(cache.DefaultTtl);
        }

        [Fact]
        public void Constructor_CustomCapacity()
        {
            var cache = new PromptCache(capacity: 50);
            Assert.Equal(50, cache.Capacity);
        }

        [Fact]
        public void Constructor_WithTtl()
        {
            var ttl = TimeSpan.FromMinutes(30);
            var cache = new PromptCache(defaultTtl: ttl);
            Assert.Equal(ttl, cache.DefaultTtl);
        }

        [Fact]
        public void Constructor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptCache(capacity: 0));
        }

        [Fact]
        public void Constructor_NegativeCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptCache(capacity: -1));
        }

        [Fact]
        public void Constructor_ZeroTtl_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PromptCache(defaultTtl: TimeSpan.Zero));
        }

        [Fact]
        public void Constructor_NegativeTtl_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PromptCache(defaultTtl: TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void Constructor_CapacityOne_Works()
        {
            var cache = new PromptCache(capacity: 1);
            Assert.Equal(1, cache.Capacity);
        }

        // ================================================================
        // ComputeKey
        // ================================================================

        [Fact]
        public void ComputeKey_SamePrompt_SameKey()
        {
            var k1 = PromptCache.ComputeKey("Hello world");
            var k2 = PromptCache.ComputeKey("Hello world");
            Assert.Equal(k1, k2);
        }

        [Fact]
        public void ComputeKey_DifferentPrompts_DifferentKeys()
        {
            var k1 = PromptCache.ComputeKey("Hello");
            var k2 = PromptCache.ComputeKey("World");
            Assert.NotEqual(k1, k2);
        }

        [Fact]
        public void ComputeKey_WithModel_DifferentFromWithout()
        {
            var k1 = PromptCache.ComputeKey("Hello", model: null);
            var k2 = PromptCache.ComputeKey("Hello", model: "gpt-4");
            Assert.NotEqual(k1, k2);
        }

        [Fact]
        public void ComputeKey_DifferentModels_DifferentKeys()
        {
            var k1 = PromptCache.ComputeKey("Hello", model: "gpt-4");
            var k2 = PromptCache.ComputeKey("Hello", model: "gpt-3.5");
            Assert.NotEqual(k1, k2);
        }

        [Fact]
        public void ComputeKey_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PromptCache.ComputeKey(null!));
        }

        [Fact]
        public void ComputeKey_EmptyString_Works()
        {
            var key = PromptCache.ComputeKey("");
            Assert.NotNull(key);
            Assert.NotEmpty(key);
            Assert.Equal(64, key.Length); // SHA-256 = 64 hex chars
        }

        [Fact]
        public void ComputeKey_IsHexLowercase()
        {
            var key = PromptCache.ComputeKey("test");
            Assert.Matches("^[0-9a-f]{64}$", key);
        }

        // ================================================================
        // Put + Get
        // ================================================================

        [Fact]
        public void Put_Get_BasicRoundTrip()
        {
            var cache = new PromptCache();
            cache.Put("What is 2+2?", "4");

            var entry = cache.Get("What is 2+2?");
            Assert.NotNull(entry);
            Assert.Equal("4", entry.Response);
            Assert.Equal("What is 2+2?", entry.Prompt);
        }

        [Fact]
        public void Put_Get_WithModel()
        {
            var cache = new PromptCache();
            cache.Put("Hello", "Response A", model: "gpt-4");
            cache.Put("Hello", "Response B", model: "gpt-3.5");

            var a = cache.Get("Hello", model: "gpt-4");
            var b = cache.Get("Hello", model: "gpt-3.5");

            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.Equal("Response A", a.Response);
            Assert.Equal("Response B", b.Response);
        }

        [Fact]
        public void Put_UpdatesExistingEntry()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "old response");
            cache.Put("prompt", "new response");

            Assert.Equal(1, cache.Count);
            var entry = cache.Get("prompt");
            Assert.NotNull(entry);
            Assert.Equal("new response", entry.Response);
        }

        [Fact]
        public void Put_WithMetadata()
        {
            var cache = new PromptCache();
            var meta = new Dictionary<string, string>
            {
                { "source", "test" },
                { "version", "1.0" }
            };
            cache.Put("prompt", "response", metadata: meta);

            var entry = cache.Get("prompt");
            Assert.NotNull(entry);
            Assert.NotNull(entry.Metadata);
            Assert.Equal("test", entry.Metadata["source"]);
            Assert.Equal("1.0", entry.Metadata["version"]);
        }

        [Fact]
        public void Put_NullPrompt_Throws()
        {
            var cache = new PromptCache();
            Assert.Throws<ArgumentNullException>(() => cache.Put(null!, "response"));
        }

        [Fact]
        public void Put_NullResponse_Throws()
        {
            var cache = new PromptCache();
            Assert.Throws<ArgumentNullException>(() => cache.Put("prompt", null!));
        }

        [Fact]
        public void Get_NullPrompt_Throws()
        {
            var cache = new PromptCache();
            Assert.Throws<ArgumentNullException>(() => cache.Get(null!));
        }

        [Fact]
        public void Get_Miss_ReturnsNull()
        {
            var cache = new PromptCache();
            Assert.Null(cache.Get("nonexistent"));
        }

        [Fact]
        public void Get_IncrementsAccessCount()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");

            cache.Get("prompt");
            cache.Get("prompt");
            var entry = cache.Get("prompt");

            Assert.NotNull(entry);
            Assert.Equal(4, entry.AccessCount); // 1 from Put + 3 from Gets
        }

        [Fact]
        public void Get_UpdatesLastAccessedAt()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");
            var first = cache.Get("prompt");
            Assert.NotNull(first);
            var firstAccess = first.LastAccessedAt;

            Thread.Sleep(10);
            var second = cache.Get("prompt");
            Assert.NotNull(second);
            Assert.True(second.LastAccessedAt >= firstAccess);
        }

        // ================================================================
        // LRU Eviction
        // ================================================================

        [Fact]
        public void LruEviction_EvictsOldest()
        {
            var cache = new PromptCache(capacity: 2);
            cache.Put("first", "1");
            cache.Put("second", "2");
            cache.Put("third", "3"); // Should evict "first"

            Assert.Equal(2, cache.Count);
            Assert.Null(cache.Get("first"));
            Assert.NotNull(cache.Get("second"));
            Assert.NotNull(cache.Get("third"));
        }

        [Fact]
        public void LruEviction_AccessRefreshesPosition()
        {
            var cache = new PromptCache(capacity: 2);
            cache.Put("first", "1");
            cache.Put("second", "2");

            // Access "first" to make it most recently used
            cache.Get("first");

            cache.Put("third", "3"); // Should evict "second" (LRU)

            Assert.NotNull(cache.Get("first"));
            Assert.Null(cache.Get("second"));
            Assert.NotNull(cache.Get("third"));
        }

        [Fact]
        public void LruEviction_CapacityOne_AlwaysReplacesEntry()
        {
            var cache = new PromptCache(capacity: 1);
            cache.Put("a", "1");
            cache.Put("b", "2");

            Assert.Equal(1, cache.Count);
            Assert.Null(cache.Get("a"));
            Assert.NotNull(cache.Get("b"));
        }

        [Fact]
        public void LruEviction_IncrementsEvictionCount()
        {
            var cache = new PromptCache(capacity: 1);
            cache.Put("a", "1");
            cache.Put("b", "2"); // evicts "a"

            var stats = cache.GetStats();
            Assert.Equal(1, stats.Evictions);
        }

        // ================================================================
        // TTL Expiration
        // ================================================================

        [Fact]
        public void Ttl_ExpiredEntry_ReturnsMiss()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromMilliseconds(50));
            cache.Put("prompt", "response");

            Thread.Sleep(100);

            Assert.Null(cache.Get("prompt"));
        }

        [Fact]
        public void Ttl_NonExpiredEntry_ReturnsHit()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromHours(1));
            cache.Put("prompt", "response");

            Assert.NotNull(cache.Get("prompt"));
        }

        [Fact]
        public void Ttl_PerEntryOverride()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromHours(1));

            cache.Put("short", "value", ttl: TimeSpan.FromMilliseconds(50));
            cache.Put("long", "value", ttl: TimeSpan.FromHours(2));

            Thread.Sleep(100);

            Assert.Null(cache.Get("short"));
            Assert.NotNull(cache.Get("long"));
        }

        [Fact]
        public void Ttl_NoTtl_NeverExpires()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");

            var entry = cache.Get("prompt");
            Assert.NotNull(entry);
            Assert.Null(entry.ExpiresAt);
        }

        // ================================================================
        // Remove
        // ================================================================

        [Fact]
        public void Remove_ExistingEntry_ReturnsTrue()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");
            Assert.True(cache.Remove("prompt"));
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void Remove_NonExistentEntry_ReturnsFalse()
        {
            var cache = new PromptCache();
            Assert.False(cache.Remove("nonexistent"));
        }

        [Fact]
        public void Remove_WithModel()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response", model: "gpt-4");
            Assert.True(cache.Remove("prompt", model: "gpt-4"));
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void Remove_NullPrompt_Throws()
        {
            var cache = new PromptCache();
            Assert.Throws<ArgumentNullException>(() => cache.Remove(null!));
        }

        [Fact]
        public void Remove_DoesNotAffectOtherEntries()
        {
            var cache = new PromptCache();
            cache.Put("keep", "value1");
            cache.Put("remove", "value2");
            cache.Remove("remove");

            Assert.Equal(1, cache.Count);
            Assert.NotNull(cache.Get("keep"));
        }

        // ================================================================
        // Contains
        // ================================================================

        [Fact]
        public void Contains_ExistingEntry_ReturnsTrue()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");
            Assert.True(cache.Contains("prompt"));
        }

        [Fact]
        public void Contains_NonExistent_ReturnsFalse()
        {
            var cache = new PromptCache();
            Assert.False(cache.Contains("nonexistent"));
        }

        [Fact]
        public void Contains_ExpiredEntry_ReturnsFalse()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromMilliseconds(50));
            cache.Put("prompt", "response");
            Thread.Sleep(100);
            Assert.False(cache.Contains("prompt"));
        }

        [Fact]
        public void Contains_NullPrompt_Throws()
        {
            var cache = new PromptCache();
            Assert.Throws<ArgumentNullException>(() => cache.Contains(null!));
        }

        [Fact]
        public void Contains_DoesNotAffectStats()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");
            cache.Contains("prompt");
            cache.Contains("nonexistent");

            var stats = cache.GetStats();
            Assert.Equal(0, stats.Hits);
            Assert.Equal(0, stats.Misses);
        }

        // ================================================================
        // PurgeExpired
        // ================================================================

        [Fact]
        public void PurgeExpired_RemovesExpiredEntries()
        {
            var cache = new PromptCache();
            cache.Put("short", "value", ttl: TimeSpan.FromMilliseconds(50));
            cache.Put("long", "value", ttl: TimeSpan.FromHours(1));

            Thread.Sleep(100);
            int purged = cache.PurgeExpired();

            Assert.Equal(1, purged);
            Assert.Equal(1, cache.Count);
            Assert.NotNull(cache.Get("long"));
        }

        [Fact]
        public void PurgeExpired_NothingExpired_ReturnsZero()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromHours(1));
            cache.Put("a", "1");
            cache.Put("b", "2");

            Assert.Equal(0, cache.PurgeExpired());
        }

        [Fact]
        public void PurgeExpired_EmptyCache_ReturnsZero()
        {
            var cache = new PromptCache();
            Assert.Equal(0, cache.PurgeExpired());
        }

        // ================================================================
        // Clear
        // ================================================================

        [Fact]
        public void Clear_EmptiesCache()
        {
            var cache = new PromptCache();
            cache.Put("a", "1");
            cache.Put("b", "2");
            cache.Get("a");
            cache.Get("nonexistent");

            cache.Clear();

            Assert.Equal(0, cache.Count);
            Assert.Null(cache.Get("a"));
        }

        [Fact]
        public void Clear_ResetsStats()
        {
            var cache = new PromptCache();
            cache.Put("a", "1");
            cache.Get("a");
            cache.Get("miss");

            cache.Clear();

            var stats = cache.GetStats();
            Assert.Equal(0, stats.Hits);
            Assert.Equal(0, stats.Misses);
            Assert.Equal(0, stats.Evictions);
        }

        // ================================================================
        // GetStats
        // ================================================================

        [Fact]
        public void GetStats_TracksHitsAndMisses()
        {
            var cache = new PromptCache();
            cache.Put("a", "1");

            cache.Get("a");     // hit
            cache.Get("a");     // hit
            cache.Get("miss1"); // miss
            cache.Get("miss2"); // miss
            cache.Get("miss3"); // miss

            var stats = cache.GetStats();
            Assert.Equal(2, stats.Hits);
            Assert.Equal(3, stats.Misses);
            Assert.Equal(40.0, stats.HitRate);
        }

        [Fact]
        public void GetStats_NoLookups_HitRateIsZero()
        {
            var cache = new PromptCache();
            var stats = cache.GetStats();
            Assert.Equal(0.0, stats.HitRate);
        }

        [Fact]
        public void GetStats_CountAndCapacity()
        {
            var cache = new PromptCache(capacity: 10);
            cache.Put("a", "1");
            cache.Put("b", "2");

            var stats = cache.GetStats();
            Assert.Equal(2, stats.Count);
            Assert.Equal(10, stats.Capacity);
        }

        [Fact]
        public void GetStats_ToString_IsHumanReadable()
        {
            var cache = new PromptCache(capacity: 10);
            cache.Put("a", "1");
            cache.Get("a");

            var stats = cache.GetStats();
            string s = stats.ToString();

            Assert.Contains("Hits: 1", s);
            Assert.Contains("Misses: 0", s);
            Assert.Contains("Hit Rate: 100%", s);
            Assert.Contains("Count: 1/10", s);
        }

        // ================================================================
        // GetAll
        // ================================================================

        [Fact]
        public void GetAll_ReturnsAllEntries()
        {
            var cache = new PromptCache();
            cache.Put("a", "1");
            cache.Put("b", "2");
            cache.Put("c", "3");

            var all = cache.GetAll();
            Assert.Equal(3, all.Count);
        }

        [Fact]
        public void GetAll_MruOrder()
        {
            var cache = new PromptCache();
            cache.Put("a", "1");
            cache.Put("b", "2");
            cache.Put("c", "3");

            var all = cache.GetAll();
            Assert.Equal("c", all[0].Prompt);
            Assert.Equal("b", all[1].Prompt);
            Assert.Equal("a", all[2].Prompt);
        }

        [Fact]
        public void GetAll_SkipsExpiredEntries()
        {
            var cache = new PromptCache();
            cache.Put("short", "1", ttl: TimeSpan.FromMilliseconds(50));
            cache.Put("long", "2", ttl: TimeSpan.FromHours(1));

            Thread.Sleep(100);

            var all = cache.GetAll();
            Assert.Single(all);
            Assert.Equal("long", all[0].Prompt);
        }

        [Fact]
        public void GetAll_EmptyCache()
        {
            var cache = new PromptCache();
            Assert.Empty(cache.GetAll());
        }

        // ================================================================
        // Serialization (ToJson / FromJson)
        // ================================================================

        [Fact]
        public void ToJson_FromJson_RoundTrip()
        {
            var cache = new PromptCache(capacity: 50, defaultTtl: TimeSpan.FromHours(1));
            cache.Put("prompt1", "response1");
            cache.Put("prompt2", "response2", model: "gpt-4");

            string json = cache.ToJson();
            var restored = PromptCache.FromJson(json);

            Assert.Equal(50, restored.Capacity);
            Assert.Equal(TimeSpan.FromHours(1), restored.DefaultTtl);
            Assert.Equal(2, restored.Count);

            var e1 = restored.Get("prompt1");
            Assert.NotNull(e1);
            Assert.Equal("response1", e1.Response);

            var e2 = restored.Get("prompt2", model: "gpt-4");
            Assert.NotNull(e2);
            Assert.Equal("response2", e2.Response);
        }

        [Fact]
        public void ToJson_SkipsExpiredEntries()
        {
            var cache = new PromptCache();
            cache.Put("short", "1", ttl: TimeSpan.FromMilliseconds(50));
            cache.Put("long", "2", ttl: TimeSpan.FromHours(1));

            Thread.Sleep(100);

            string json = cache.ToJson();
            var restored = PromptCache.FromJson(json);

            Assert.Equal(1, restored.Count);
            Assert.NotNull(restored.Get("long"));
            Assert.Null(restored.Get("short"));
        }

        [Fact]
        public void ToJson_WithMetadata()
        {
            var cache = new PromptCache();
            var meta = new Dictionary<string, string> { { "key", "val" } };
            cache.Put("prompt", "response", metadata: meta);

            string json = cache.ToJson();
            var restored = PromptCache.FromJson(json);

            var entry = restored.Get("prompt");
            Assert.NotNull(entry);
            Assert.NotNull(entry.Metadata);
            Assert.Equal("val", entry.Metadata["key"]);
        }

        [Fact]
        public void ToJson_IndentedVsCompact()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");

            string indented = cache.ToJson(indented: true);
            string compact = cache.ToJson(indented: false);

            Assert.Contains("\n", indented);
            Assert.DoesNotContain("\n", compact);
        }

        [Fact]
        public void ToJson_IsValidJson()
        {
            var cache = new PromptCache(capacity: 10, defaultTtl: TimeSpan.FromMinutes(5));
            cache.Put("prompt", "response");

            string json = cache.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        [Fact]
        public void FromJson_NullString_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptCache.FromJson(null!));
        }

        [Fact]
        public void FromJson_MalformedJson_Throws()
        {
            Assert.Throws<JsonException>(() => PromptCache.FromJson("{invalid}"));
        }

        [Fact]
        public void FromJson_EmptyEntries()
        {
            string json = """{"capacity":10,"entries":[]}""";
            var cache = PromptCache.FromJson(json);
            Assert.Equal(10, cache.Capacity);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void FromJson_PreservesAccessCount()
        {
            var cache = new PromptCache();
            cache.Put("p", "r");
            cache.Get("p");
            cache.Get("p");

            string json = cache.ToJson();
            var restored = PromptCache.FromJson(json);
            var entry = restored.Get("p");

            Assert.NotNull(entry);
            Assert.True(entry.AccessCount >= 3);
        }

        // ================================================================
        // File I/O (SaveToFileAsync / LoadFromFileAsync)
        // ================================================================

        [Fact]
        public async Task SaveAndLoad_FileRoundTrip()
        {
            var cache = new PromptCache(capacity: 20);
            cache.Put("file-test", "file-response");

            string path = Path.GetTempFileName();
            try
            {
                await cache.SaveToFileAsync(path);
                var loaded = await PromptCache.LoadFromFileAsync(path);

                Assert.Equal(20, loaded.Capacity);
                Assert.Equal(1, loaded.Count);
                var entry = loaded.Get("file-test");
                Assert.NotNull(entry);
                Assert.Equal("file-response", entry.Response);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task SaveToFile_EmptyPath_Throws()
        {
            var cache = new PromptCache();
            await Assert.ThrowsAsync<ArgumentException>(
                () => cache.SaveToFileAsync(""));
        }

        [Fact]
        public async Task LoadFromFile_EmptyPath_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => PromptCache.LoadFromFileAsync(""));
        }

        [Fact]
        public async Task LoadFromFile_NonExistentFile_Throws()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => PromptCache.LoadFromFileAsync("nonexistent-cache-file.json"));
        }

        // ================================================================
        // Thread Safety
        // ================================================================

        [Fact]
        public void ConcurrentPutAndGet_DoesNotThrow()
        {
            var cache = new PromptCache(capacity: 100);
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                int n = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                        cache.Put($"prompt-{n}-{j}", $"response-{n}-{j}");
                }));
            }

            for (int i = 0; i < 10; i++)
            {
                int n = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                        cache.Get($"prompt-{n}-{j}");
                }));
            }

            Task.WaitAll(tasks.ToArray());
            Assert.True(cache.Count <= 100);
            Assert.True(cache.Count > 0);
        }

        [Fact]
        public void ConcurrentPurge_DoesNotThrow()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromMilliseconds(10));
            for (int i = 0; i < 50; i++)
                cache.Put($"p{i}", $"r{i}");

            Thread.Sleep(50);

            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
                tasks.Add(Task.Run(() => cache.PurgeExpired()));

            Task.WaitAll(tasks.ToArray());
        }

        // ================================================================
        // Edge Cases
        // ================================================================

        [Fact]
        public void EmptyStringPrompt_Works()
        {
            var cache = new PromptCache();
            cache.Put("", "response for empty");
            var entry = cache.Get("");
            Assert.NotNull(entry);
            Assert.Equal("response for empty", entry.Response);
        }

        [Fact]
        public void VeryLongPrompt_Works()
        {
            var cache = new PromptCache();
            string longPrompt = new string('x', 100_000);
            cache.Put(longPrompt, "response");

            var entry = cache.Get(longPrompt);
            Assert.NotNull(entry);
        }

        [Fact]
        public void UnicodePrompt_Works()
        {
            var cache = new PromptCache();
            cache.Put("\u3053\u3093\u306B\u3061\u306F\u4E16\u754C \uD83C\uDF0D", "Hello World");

            var entry = cache.Get("\u3053\u3093\u306B\u3061\u306F\u4E16\u754C \uD83C\uDF0D");
            Assert.NotNull(entry);
            Assert.Equal("Hello World", entry.Response);
        }

        [Fact]
        public void ModelWithSpecialChars_Works()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response", model: "my-model/v2.1:latest");

            var entry = cache.Get("prompt", model: "my-model/v2.1:latest");
            Assert.NotNull(entry);
        }

        [Fact]
        public void Put_MetadataIsCopied_NotReferenced()
        {
            var cache = new PromptCache();
            var meta = new Dictionary<string, string> { { "key", "original" } };
            cache.Put("prompt", "response", metadata: meta);

            meta["key"] = "modified";

            var entry = cache.Get("prompt");
            Assert.NotNull(entry);
            Assert.Equal("original", entry.Metadata!["key"]);
        }

        [Fact]
        public void ExpiredEntry_EvictsOnGet_IncrementsMissAndEviction()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromMilliseconds(50));
            cache.Put("prompt", "response");

            Thread.Sleep(100);
            cache.Get("prompt");

            var stats = cache.GetStats();
            Assert.Equal(0, stats.Hits);
            Assert.Equal(1, stats.Misses);
            Assert.Equal(1, stats.Evictions);
            Assert.Equal(0, stats.Count);
        }

        [Fact]
        public void MultipleEvictions_StatsAccumulate()
        {
            var cache = new PromptCache(capacity: 2);
            cache.Put("a", "1");
            cache.Put("b", "2");
            cache.Put("c", "3"); // evicts a
            cache.Put("d", "4"); // evicts b

            var stats = cache.GetStats();
            Assert.Equal(2, stats.Evictions);
            Assert.Equal(2, stats.Count);
        }

        [Fact]
        public void MaxValueTtl_NeverExpires()
        {
            var cache = new PromptCache(defaultTtl: TimeSpan.FromMilliseconds(50));
            cache.Put("never-expire", "value", ttl: TimeSpan.MaxValue);

            Thread.Sleep(100);

            var entry = cache.Get("never-expire");
            Assert.NotNull(entry);
            Assert.Null(entry.ExpiresAt);
        }

        [Fact]
        public void CacheEntry_CreatedAt_IsSet()
        {
            var before = DateTimeOffset.UtcNow;
            var cache = new PromptCache();
            cache.Put("prompt", "response");
            var after = DateTimeOffset.UtcNow;

            var entry = cache.Get("prompt");
            Assert.NotNull(entry);
            Assert.InRange(entry.CreatedAt, before, after.AddSeconds(1));
        }

        [Fact]
        public void CacheEntry_ModelIsStored()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response", model: "gpt-4o");

            var entry = cache.Get("prompt", model: "gpt-4o");
            Assert.NotNull(entry);
            Assert.Equal("gpt-4o", entry.Model);
        }

        [Fact]
        public void CacheEntry_NullMetadata_DefaultsToNull()
        {
            var cache = new PromptCache();
            cache.Put("prompt", "response");

            var entry = cache.Get("prompt");
            Assert.NotNull(entry);
            Assert.Null(entry.Metadata);
        }

        [Fact]
        public void FromJson_DefaultCapacity_WhenZero()
        {
            string json = """{"capacity":0,"entries":[]}""";
            var cache = PromptCache.FromJson(json);
            Assert.Equal(256, cache.Capacity);
        }
    }
}
