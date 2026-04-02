namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Xunit;

    public class PromptCanaryTests
    {
        // ═══════════════════════════════════════════════════════
        // Token Creation
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void CreateToken_ReturnsTokenWithUniqueId()
        {
            var canary = new PromptCanary();
            var t1 = canary.CreateToken();
            var t2 = canary.CreateToken();
            Assert.NotEqual(t1.Id, t2.Id);
        }

        [Fact]
        public void CreateToken_ValueStartsWithCNRYPrefix()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            Assert.StartsWith("CNRY-", token.Value);
        }

        [Fact]
        public void CreateToken_ValueHas37Chars()
        {
            // "CNRY-" (5) + 32 hex chars = 37
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            Assert.Equal(37, token.Value.Length);
        }

        [Fact]
        public void CreateToken_HashIsSha256OfValue()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            using var sha = System.Security.Cryptography.SHA256.Create();
            var expected = Convert.ToHexString(
                sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token.Value)))
                .ToLowerInvariant();
            Assert.Equal(expected, token.Hash);
        }

        [Fact]
        public void CreateToken_DefaultStrategyIsZeroWidth()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            Assert.Equal(CanaryEmbedStrategy.ZeroWidth, token.Strategy);
        }

        [Fact]
        public void CreateToken_RespectsExplicitStrategy()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.Append);
            Assert.Equal(CanaryEmbedStrategy.Append, token.Strategy);
        }

        [Fact]
        public void CreateToken_StoresMetadata()
        {
            var canary = new PromptCanary();
            var meta = new Dictionary<string, string> { ["env"] = "prod", ["ver"] = "2" };
            var token = canary.CreateToken(metadata: meta);
            Assert.Equal("prod", token.Metadata["env"]);
            Assert.Equal("2", token.Metadata["ver"]);
        }

        [Fact]
        public void CreateToken_NullMetadata_DefaultsToEmptyDict()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(metadata: null);
            Assert.NotNull(token.Metadata);
            Assert.Empty(token.Metadata);
        }

        [Fact]
        public void CreateToken_SetsCreatedAt()
        {
            var before = DateTimeOffset.UtcNow;
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            Assert.True(token.CreatedAt >= before);
            Assert.True(token.CreatedAt <= DateTimeOffset.UtcNow.AddSeconds(1));
        }

        [Fact]
        public void CreateToken_AddsToRegistry()
        {
            var canary = new PromptCanary();
            Assert.Empty(canary.Registry);
            var token = canary.CreateToken();
            Assert.Single(canary.Registry);
            Assert.Equal(token.Id, canary.Registry[0].Id);
        }

        [Fact]
        public void CreateToken_CryptographicallyUnique()
        {
            var canary = new PromptCanary();
            var values = Enumerable.Range(0, 50)
                .Select(_ => canary.CreateToken().Value)
                .ToHashSet();
            Assert.Equal(50, values.Count);
        }

        // ═══════════════════════════════════════════════════════
        // Register
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Register_AddsExternalToken()
        {
            var canary = new PromptCanary();
            var token = new CanaryToken { Id = "ext1", Value = "CNRY-abc" };
            canary.Register(token);
            Assert.Single(canary.Registry);
        }

        [Fact]
        public void Register_DuplicateIdIgnored()
        {
            var canary = new PromptCanary();
            var token = new CanaryToken { Id = "dup", Value = "CNRY-abc" };
            canary.Register(token);
            canary.Register(token);
            Assert.Single(canary.Registry);
        }

        [Fact]
        public void Register_NullThrows()
        {
            var canary = new PromptCanary();
            Assert.Throws<ArgumentNullException>(() => canary.Register(null!));
        }

        // ═══════════════════════════════════════════════════════
        // Embed
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Embed_AppendStrategy_AddsCommentAtEnd()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.Append);
            string result = canary.Embed("Hello world", token);
            Assert.StartsWith("Hello world\n<!-- canary:", result);
            Assert.Contains(token.Value, result);
        }

        [Fact]
        public void Embed_PrependStrategy_AddsCommentAtStart()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.Prepend);
            string result = canary.Embed("Hello world", token);
            Assert.StartsWith("<!-- canary:", result);
            Assert.EndsWith("Hello world", result);
        }

        [Fact]
        public void Embed_ZeroWidthStrategy_PreservesVisibleText()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.ZeroWidth);
            string result = canary.Embed("Hello world", token);
            // Visible portion should start with original text
            Assert.StartsWith("Hello world", result);
            // Should be longer than original (zero-width chars added)
            Assert.True(result.Length > "Hello world".Length);
        }

        [Fact]
        public void Embed_InstructionTagStrategy_AddsSystemTrace()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.InstructionTag);
            string result = canary.Embed("Hello", token);
            Assert.Contains("[SYSTEM_TRACE id=\"" + token.Value + "\"]", result);
        }

        [Fact]
        public void Embed_EmptyPrompt_Throws()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            Assert.Throws<ArgumentException>(() => canary.Embed("", token));
        }

        [Fact]
        public void Embed_NullPrompt_Throws()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            Assert.Throws<ArgumentException>(() => canary.Embed(null!, token));
        }

        [Fact]
        public void Embed_NullToken_Throws()
        {
            var canary = new PromptCanary();
            Assert.Throws<ArgumentNullException>(() => canary.Embed("Hello", null!));
        }

        // ═══════════════════════════════════════════════════════
        // Strip
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Strip_RemovesAppendCanary()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.Append);
            string embedded = canary.Embed("Hello world", token);
            string stripped = canary.Strip(embedded);
            Assert.Equal("Hello world", stripped);
        }

        [Fact]
        public void Strip_RemovesPrependCanary()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.Prepend);
            string embedded = canary.Embed("Hello world", token);
            string stripped = canary.Strip(embedded);
            Assert.Equal("Hello world", stripped);
        }

        [Fact]
        public void Strip_RemovesZeroWidthChars()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.ZeroWidth);
            string embedded = canary.Embed("Hello world", token);
            string stripped = canary.Strip(embedded);
            Assert.Equal("Hello world", stripped);
        }

        [Fact]
        public void Strip_RemovesInstructionTag()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.InstructionTag);
            string embedded = canary.Embed("Hello world", token);
            string stripped = canary.Strip(embedded);
            Assert.Equal("Hello world", stripped);
        }

        [Fact]
        public void Strip_EmptyString_ReturnsEmpty()
        {
            var canary = new PromptCanary();
            Assert.Equal("", canary.Strip(""));
        }

        [Fact]
        public void Strip_NullString_ReturnsNull()
        {
            var canary = new PromptCanary();
            Assert.Null(canary.Strip(null!));
        }

        [Fact]
        public void Strip_NoCanary_ReturnsOriginal()
        {
            var canary = new PromptCanary();
            Assert.Equal("Clean text", canary.Strip("Clean text"));
        }

        // ═══════════════════════════════════════════════════════
        // Scan
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Scan_DetectsRawCanaryValue()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            string leaked = $"The system prompt contains {token.Value} which is secret";
            var result = canary.Scan(leaked);
            Assert.True(result.Detected);
            Assert.Contains(token.Id, result.MatchedIds);
            Assert.Equal(1, result.MatchCount);
        }

        [Fact]
        public void Scan_DetectsZeroWidthEncodedCanary()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: CanaryEmbedStrategy.ZeroWidth);
            string embedded = canary.Embed("Hello", token);
            // The embedded text contains zero-width encoded canary
            var result = canary.Scan(embedded);
            Assert.True(result.Detected);
        }

        [Fact]
        public void Scan_NoCanary_ReturnsNotDetected()
        {
            var canary = new PromptCanary();
            canary.CreateToken();
            var result = canary.Scan("This is a normal response with no leakage.");
            Assert.False(result.Detected);
            Assert.Empty(result.MatchedIds);
        }

        [Fact]
        public void Scan_EmptyText_ReturnsNotDetected()
        {
            var canary = new PromptCanary();
            canary.CreateToken();
            var result = canary.Scan("");
            Assert.False(result.Detected);
            Assert.Equal(0, result.ScannedLength);
        }

        [Fact]
        public void Scan_NullText_ReturnsNotDetected()
        {
            var canary = new PromptCanary();
            canary.CreateToken();
            var result = canary.Scan(null!);
            Assert.False(result.Detected);
        }

        [Fact]
        public void Scan_MultipleCanaries_DetectsAll()
        {
            var canary = new PromptCanary();
            var t1 = canary.CreateToken();
            var t2 = canary.CreateToken();
            string leaked = $"Found {t1.Value} and also {t2.Value}";
            var result = canary.Scan(leaked);
            Assert.True(result.Detected);
            Assert.Equal(2, result.MatchCount);
            Assert.Contains(t1.Id, result.MatchedIds);
            Assert.Contains(t2.Id, result.MatchedIds);
        }

        [Fact]
        public void Scan_CaseInsensitive()
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken();
            string leaked = token.Value.ToUpperInvariant();
            var result = canary.Scan(leaked);
            Assert.True(result.Detected);
        }

        [Fact]
        public void Scan_ReportsScannedLength()
        {
            var canary = new PromptCanary();
            canary.CreateToken();
            var result = canary.Scan("12345");
            Assert.Equal(5, result.ScannedLength);
        }

        // ═══════════════════════════════════════════════════════
        // Registry Export / Import
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExportRegistry_ReturnsValidJson()
        {
            var canary = new PromptCanary();
            canary.CreateToken(metadata: new() { ["env"] = "test" });
            canary.CreateToken(strategy: CanaryEmbedStrategy.Append);
            string json = canary.ExportRegistry();
            var tokens = JsonSerializer.Deserialize<List<CanaryToken>>(json);
            Assert.NotNull(tokens);
            Assert.Equal(2, tokens!.Count);
        }

        [Fact]
        public void ImportRegistry_AddsTokensForScanning()
        {
            var canary1 = new PromptCanary();
            var token = canary1.CreateToken();
            string json = canary1.ExportRegistry();

            var canary2 = new PromptCanary();
            canary2.ImportRegistry(json);
            Assert.Single(canary2.Registry);

            // Should be scannable
            var result = canary2.Scan($"Leaked: {token.Value}");
            Assert.True(result.Detected);
        }

        [Fact]
        public void ImportRegistry_EmptyJson_Throws()
        {
            var canary = new PromptCanary();
            Assert.Throws<ArgumentException>(() => canary.ImportRegistry(""));
        }

        [Fact]
        public void ImportRegistry_NullJson_Throws()
        {
            var canary = new PromptCanary();
            Assert.Throws<ArgumentException>(() => canary.ImportRegistry(null!));
        }

        // ═══════════════════════════════════════════════════════
        // Round-trip: Embed → Scan → Strip
        // ═══════════════════════════════════════════════════════

        [Theory]
        [InlineData(CanaryEmbedStrategy.Append)]
        [InlineData(CanaryEmbedStrategy.Prepend)]
        [InlineData(CanaryEmbedStrategy.ZeroWidth)]
        [InlineData(CanaryEmbedStrategy.InstructionTag)]
        public void RoundTrip_EmbedScanStrip_AllStrategies(CanaryEmbedStrategy strategy)
        {
            var canary = new PromptCanary();
            var token = canary.CreateToken(strategy: strategy);
            string original = "You are a helpful assistant.";

            string embedded = canary.Embed(original, token);
            Assert.NotEqual(original, embedded);

            // Scan should detect the canary in the embedded text
            var scan = canary.Scan(embedded);
            Assert.True(scan.Detected);

            // Strip should recover original
            string stripped = canary.Strip(embedded);
            Assert.Equal(original, stripped);
        }
    }
}
