namespace Prompt.Tests
{
    using Xunit;
    using Prompt;
    using System.Collections.Generic;

    public class PromptWatermarkTests
    {
        [Fact]
        public void ZeroWidth_RoundTrip()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth);
            string original = "Summarize the following document for me.";
            string marked = wm.Embed(original, "v2.1-expA");
            var payload = wm.Extract(marked);

            Assert.NotNull(payload);
            Assert.Equal("v2.1-expA", payload!.Data);
            Assert.Equal(WatermarkStrategy.ZeroWidth, payload.Strategy);
        }

        [Fact]
        public void ZeroWidth_Strip_RestoresOriginal()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth);
            string original = "Hello world";
            string marked = wm.Embed(original, "test-payload");
            Assert.NotEqual(original, marked);
            Assert.Equal(original, wm.Strip(marked));
        }

        [Fact]
        public void ZeroWidth_Contains()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth);
            string marked = wm.Embed("some text here", "id-123");
            Assert.True(wm.Contains(marked));
            Assert.False(wm.Contains("some text here"));
        }

        [Fact]
        public void ZeroWidth_CustomPosition()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth);
            string marked = wm.Embed("abcdef", "x", position: 0);
            var payload = wm.Extract(marked);
            Assert.NotNull(payload);
            Assert.Equal("x", payload!.Data);
            Assert.Equal(0, payload.Position);
        }

        [Fact]
        public void Homoglyph_RoundTrip()
        {
            var wm = new PromptWatermark(WatermarkStrategy.Homoglyph);
            // Need enough eligible characters for the payload
            string text = "please summarize the core concepts of the paper and explain each section clearly";
            string marked = wm.Embed(text, "ab");
            var payload = wm.Extract(marked);

            Assert.NotNull(payload);
            Assert.Equal("ab", payload!.Data);
        }

        [Fact]
        public void Homoglyph_Strip()
        {
            var wm = new PromptWatermark(WatermarkStrategy.Homoglyph);
            string original = "please extract the key concepts from the following text passage";
            string marked = wm.Embed(original, "x");
            string stripped = wm.Strip(marked);
            Assert.Equal(original, stripped);
        }

        [Fact]
        public void Whitespace_RoundTrip()
        {
            var wm = new PromptWatermark(WatermarkStrategy.Whitespace);
            string text = "Line one\nLine two\nLine three\nLine four";
            string marked = wm.Embed(text, "v3");
            var payload = wm.Extract(marked);

            Assert.NotNull(payload);
            Assert.Equal("v3", payload!.Data);
            Assert.Equal(WatermarkStrategy.Whitespace, payload.Strategy);
        }

        [Fact]
        public void Whitespace_Strip()
        {
            var wm = new PromptWatermark(WatermarkStrategy.Whitespace);
            string original = "Line one\nLine two\nLine three";
            string marked = wm.Embed(original, "tag");
            Assert.Equal(original, wm.Strip(marked));
        }

        [Fact]
        public void HmacKey_ValidatesIntegrity()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth, hmacKey: "secret-key");
            string marked = wm.Embed("test text here", "payload-data");
            var payload = wm.Extract(marked);

            Assert.NotNull(payload);
            Assert.Equal("payload-data", payload!.Data);
            Assert.True(payload.IntegrityValid);
        }

        [Fact]
        public void HmacKey_DetectsTampering()
        {
            var wmEmbed = new PromptWatermark(WatermarkStrategy.ZeroWidth, hmacKey: "key-one");
            var wmExtract = new PromptWatermark(WatermarkStrategy.ZeroWidth, hmacKey: "key-two");

            string marked = wmEmbed.Embed("test text here", "my-data");
            var payload = wmExtract.Extract(marked);

            Assert.NotNull(payload);
            Assert.False(payload!.IntegrityValid);
        }

        [Fact]
        public void EmbedMetadata_RoundTrip()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth);
            var meta = new Dictionary<string, string>
            {
                { "version", "2.1" },
                { "variant", "A" },
                { "author", "test" }
            };
            string marked = wm.EmbedMetadata("Analyze this prompt carefully.", meta);
            var extracted = wm.ExtractMetadata(marked);

            Assert.NotNull(extracted);
            Assert.Equal("2.1", extracted!["version"]);
            Assert.Equal("A", extracted["variant"]);
            Assert.Equal("test", extracted["author"]);
        }

        [Fact]
        public void Extract_ReturnsNull_ForCleanText()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth);
            Assert.Null(wm.Extract("just normal text"));
        }

        [Fact]
        public void Extract_ReturnsNull_ForEmptyText()
        {
            var wm = new PromptWatermark(WatermarkStrategy.ZeroWidth);
            Assert.Null(wm.Extract(""));
            Assert.Null(wm.Extract(null!));
        }

        [Fact]
        public void Embed_ThrowsOnEmptyArgs()
        {
            var wm = new PromptWatermark();
            Assert.Throws<ArgumentException>(() => wm.Embed("", "data"));
            Assert.Throws<ArgumentException>(() => wm.Embed("text", ""));
        }

        [Fact]
        public void EmbedMetadata_ThrowsOnEmptyMetadata()
        {
            var wm = new PromptWatermark();
            Assert.Throws<ArgumentException>(() => wm.EmbedMetadata("text", new Dictionary<string, string>()));
        }
    }
}
