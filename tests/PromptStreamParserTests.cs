namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptStreamParser"/> — real-time streaming
/// content extraction (code blocks, JSON, lists, tables, key-value
/// pairs, headings, plain text). 40 tests.
/// </summary>
public class PromptStreamParserTests
{
    private static StreamChunk Chunk(string delta, bool complete = false) =>
        new() { Delta = delta, IsComplete = complete };

    // ── Code blocks ──

    [Fact]
    public void ExtractsCodeBlock()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Here is code:\n```python\nprint('hello')\n```\nDone."));
        var summary = parser.Complete();
        Assert.Single(summary.CodeBlocks);
        Assert.Equal("print('hello')", summary.CodeBlocks[0].Content);
        Assert.Equal("python", summary.CodeBlocks[0].Tag);
    }

    [Fact]
    public void ExtractsCodeBlockAcrossChunks()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("```js\ncon"));
        parser.Feed(Chunk("sole.log(1)\n"));
        parser.Feed(Chunk("```\n"));
        var summary = parser.Complete();
        Assert.Single(summary.CodeBlocks);
        Assert.Equal("console.log(1)", summary.CodeBlocks[0].Content);
        Assert.Equal("js", summary.CodeBlocks[0].Tag);
    }

    [Fact]
    public void CodeBlockNoLanguage()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("```\nhello\n```\n"));
        var summary = parser.Complete();
        Assert.Single(summary.CodeBlocks);
        Assert.Null(summary.CodeBlocks[0].Tag);
        Assert.Equal("hello", summary.CodeBlocks[0].Content);
    }

    [Fact]
    public void MultipleCodeBlocks()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("```py\na=1\n```\ntext\n```rust\nfn main(){}\n```\n"));
        var summary = parser.Complete();
        Assert.Equal(2, summary.CodeBlocks.Count);
        Assert.Equal("py", summary.CodeBlocks[0].Tag);
        Assert.Equal("rust", summary.CodeBlocks[1].Tag);
    }

    // ── JSON ──

    [Fact]
    public void ExtractsJsonObject()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Result:\n{\"name\": \"test\", \"value\": 42}\ndone\n"));
        var summary = parser.Complete();
        Assert.Single(summary.JsonObjects);
        Assert.NotNull(summary.JsonObjects[0].Parsed);
    }

    [Fact]
    public void ExtractsNestedJson()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("{\"a\": {\"b\": {\"c\": 1}}}"));
        var summary = parser.Complete();
        Assert.Single(summary.JsonObjects);
    }

    [Fact]
    public void ExtractsJsonArray()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("[\"one\", \"two\", \"three\"]"));
        var summary = parser.Complete();
        Assert.Single(summary.JsonArrays);
    }

    [Fact]
    public void JsonAcrossChunks()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("{\"na"));
        parser.Feed(Chunk("me\": \"te"));
        parser.Feed(Chunk("st\"}"));
        var summary = parser.Complete();
        Assert.Single(summary.JsonObjects);
    }

    [Fact]
    public void JsonWithEscapedQuotes()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("{\"msg\": \"he said \\\"hi\\\"\"}"));
        var summary = parser.Complete();
        Assert.Single(summary.JsonObjects);
    }

    [Fact]
    public void InvalidJsonNotEmitted()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("{not json at all}"));
        var summary = parser.Complete();
        Assert.Empty(summary.JsonObjects);
    }

    // ── Headings ──

    [Fact]
    public void ExtractsHeadings()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("# Title\nSome text\n## Subtitle\nMore text\n"));
        var summary = parser.Complete();
        Assert.Equal(2, summary.Headings.Count);
        Assert.Equal("Title", summary.Headings[0].Content);
        Assert.Equal("1", summary.Headings[0].Tag);
        Assert.Equal("Subtitle", summary.Headings[1].Content);
        Assert.Equal("2", summary.Headings[1].Tag);
    }

    [Fact]
    public void HeadingH6()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("###### Deep\n"));
        var summary = parser.Complete();
        Assert.Single(summary.Headings);
        Assert.Equal("6", summary.Headings[0].Tag);
    }

    // ── Tables ──

    [Fact]
    public void ExtractsTable()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("| Name | Age |\n| --- | --- |\n| Alice | 30 |\n| Bob | 25 |\nDone\n"));
        var summary = parser.Complete();
        Assert.Single(summary.Tables);
        Assert.Contains("Alice", summary.Tables[0].Content);
    }

    [Fact]
    public void SingleRowTableNotEmitted()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("| just one row |\ntext\n"));
        var summary = parser.Complete();
        Assert.Empty(summary.Tables);
    }

    // ── Lists ──

    [Fact]
    public void ExtractsBulletList()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Items:\n- Apple\n- Banana\n- Cherry\nEnd\n"));
        var summary = parser.Complete();
        Assert.Single(summary.Lists);
        Assert.Contains("Apple", summary.Lists[0].Content);
        Assert.Contains("Cherry", summary.Lists[0].Content);
    }

    [Fact]
    public void ExtractsNumberedList()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Steps:\n1. First\n2. Second\n3. Third\nDone\n"));
        var summary = parser.Complete();
        Assert.Single(summary.Lists);
        Assert.Contains("1. First", summary.Lists[0].Content);
    }

    [Fact]
    public void ExtractsStarList()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("* foo\n* bar\nend\n"));
        var summary = parser.Complete();
        Assert.Single(summary.Lists);
    }

    // ── Key-Value ──

    [Fact]
    public void ExtractsKeyValues()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Name: Alice\nAge: 30\nCity: Seattle\n"));
        var summary = parser.Complete();
        Assert.Equal(3, summary.KeyValues.Count);
        Assert.Equal("Alice", summary.KeyValues["Name"]);
        Assert.Equal("30", summary.KeyValues["Age"]);
        Assert.Equal("Seattle", summary.KeyValues["City"]);
    }

    [Fact]
    public void BoldKeyValue()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("**Status**: Active\n"));
        var summary = parser.Complete();
        Assert.Equal("Active", summary.KeyValues["Status"]);
    }

    [Fact]
    public void UrlNotKeyValue()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("https://example.com/path\n"));
        var summary = parser.Complete();
        Assert.Empty(summary.KeyValues);
    }

    // ── Options ──

    [Fact]
    public void EnabledTypesFilters()
    {
        var parser = new PromptStreamParser(new StreamParserOptions
        {
            EnabledTypes = new HashSet<StreamContentType> { StreamContentType.CodeBlock }
        });
        parser.Feed(Chunk("# Heading\n```py\nx=1\n```\nName: Val\n"));
        var summary = parser.Complete();
        Assert.Single(summary.CodeBlocks);
        Assert.Empty(summary.Headings);
        Assert.Empty(summary.KeyValues);
    }

    [Fact]
    public void MaxContentLengthTruncates()
    {
        var parser = new PromptStreamParser(new StreamParserOptions { MaxContentLength = 5 });
        parser.Feed(Chunk("```\nabcdefghij\n```\n"));
        var summary = parser.Complete();
        Assert.Equal(5, summary.CodeBlocks[0].Content.Length);
    }

    [Fact]
    public void EmitPartialCodeBlock()
    {
        var events = new List<StreamContent>();
        var parser = new PromptStreamParser(new StreamParserOptions { EmitPartial = true });
        parser.OnPartialContent += (_, e) => events.Add(e.Content);
        parser.Feed(Chunk("```py\nincomplete code"));
        parser.Complete();
        Assert.Single(events);
        Assert.True(events[0].IsPartial);
    }

    [Fact]
    public void EmitPartialJson()
    {
        var events = new List<StreamContent>();
        var parser = new PromptStreamParser(new StreamParserOptions { EmitPartial = true });
        parser.OnPartialContent += (_, e) => events.Add(e.Content);
        parser.Feed(Chunk("{\"incomplete\": "));
        parser.Complete();
        Assert.Single(events);
        Assert.True(events[0].IsPartial);
    }

    // ── Events ──

    [Fact]
    public void OnContentFires()
    {
        var events = new List<StreamContent>();
        var parser = new PromptStreamParser();
        parser.OnContent += (_, e) => events.Add(e.Content);
        parser.Feed(Chunk("# Hello\n"));
        parser.Complete();
        Assert.Single(events);
        Assert.Equal(StreamContentType.Heading, events[0].Type);
    }

    [Fact]
    public void EventIndexIncreases()
    {
        var indices = new List<int>();
        var parser = new PromptStreamParser();
        parser.OnContent += (_, e) => indices.Add(e.Index);
        parser.Feed(Chunk("# One\n# Two\n"));
        parser.Complete();
        Assert.Equal(new[] { 0, 1 }, indices);
    }

    // ── Summary ──

    [Fact]
    public void SummaryTypeCounts()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("# Title\n```py\nx=1\n```\n- a\n- b\nKey: Val\n"));
        var summary = parser.Complete();
        Assert.Equal(1, summary.TypeCounts[StreamContentType.Heading]);
        Assert.Equal(1, summary.TypeCounts[StreamContentType.CodeBlock]);
        Assert.Equal(1, summary.TypeCounts[StreamContentType.List]);
        Assert.Equal(1, summary.TypeCounts[StreamContentType.KeyValue]);
    }

    [Fact]
    public void SummaryTotalChunks()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("a"));
        parser.Feed(Chunk("b"));
        parser.Feed(Chunk("c"));
        var summary = parser.Complete();
        Assert.Equal(3, summary.TotalChunks);
    }

    [Fact]
    public void SummaryTotalCharacters()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("hello"));
        var summary = parser.Complete();
        Assert.Equal(5, summary.TotalCharacters);
    }

    // ── Text extraction ──

    [Fact]
    public void ExtractsPlainText()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Hello world\n"));
        var summary = parser.Complete();
        Assert.Contains(summary.Items, i => i.Type == StreamContentType.Text);
    }

    // ── Reset ──

    [Fact]
    public void ResetClearsState()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("# Title\n"));
        parser.Complete();
        Assert.NotEmpty(parser.CurrentContent);
        parser.Reset();
        Assert.Empty(parser.CurrentContent);
    }

    [Fact]
    public void ResetAllowsReuse()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("# First\n"));
        parser.Complete();
        parser.Reset();
        parser.Feed(Chunk("# Second\n"));
        var summary = parser.Complete();
        Assert.Single(summary.Headings);
        Assert.Equal("Second", summary.Headings[0].Content);
    }

    // ── Raw text Feed ──

    [Fact]
    public void FeedStringWorks()
    {
        var parser = new PromptStreamParser();
        parser.Feed("# Hello\n");
        var summary = parser.Complete();
        Assert.Single(summary.Headings);
    }

    // ── Edge cases ──

    [Fact]
    public void NullChunkThrows()
    {
        var parser = new PromptStreamParser();
        Assert.Throws<ArgumentNullException>(() => parser.Feed((StreamChunk)null!));
    }

    [Fact]
    public void NullStringThrows()
    {
        var parser = new PromptStreamParser();
        Assert.Throws<ArgumentNullException>(() => parser.Feed((string)null!));
    }

    [Fact]
    public void NullOptionsThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new PromptStreamParser(null!));
    }

    [Fact]
    public void EmptyStreamProducesEmptySummary()
    {
        var parser = new PromptStreamParser();
        var summary = parser.Complete();
        Assert.Empty(summary.Items);
        Assert.Equal(0, summary.TotalChunks);
    }

    [Fact]
    public void ComplexMixedContent()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("# Analysis\nModel: GPT-4\nStatus: Ready\n\n"));
        parser.Feed(Chunk("```json\n{\"score\": 0.95}\n```\n\n"));
        parser.Feed(Chunk("## Steps\n- Step 1\n- Step 2\n- Step 3\n\n"));
        parser.Feed(Chunk("| Metric | Value |\n| --- | --- |\n| Acc | 95% |\n\nDone\n"));
        var summary = parser.Complete();
        Assert.Equal(2, summary.Headings.Count);
        Assert.Equal(2, summary.KeyValues.Count);
        Assert.Single(summary.CodeBlocks);
        Assert.Single(summary.Lists);
        Assert.Single(summary.Tables);
    }

    [Fact]
    public void CharByCharFeeding()
    {
        var text = "# Hi\nKey: Val\n";
        var parser = new PromptStreamParser();
        foreach (var c in text)
            parser.Feed(Chunk(c.ToString()));
        var summary = parser.Complete();
        Assert.Single(summary.Headings);
        Assert.Single(summary.KeyValues);
    }

    [Fact]
    public void TrimContentFalse()
    {
        var parser = new PromptStreamParser(new StreamParserOptions { TrimContent = false });
        parser.Feed(Chunk("```\n  spaced  \n```\n"));
        var summary = parser.Complete();
        Assert.Contains("  spaced  ", summary.CodeBlocks[0].Content);
    }

    [Fact]
    public void ParseJsonFalse()
    {
        var parser = new PromptStreamParser(new StreamParserOptions { ParseJson = false });
        parser.Feed(Chunk("{\"a\": 1}"));
        var summary = parser.Complete();
        Assert.Single(summary.JsonObjects);
        Assert.Null(summary.JsonObjects[0].Parsed);
    }

    [Fact]
    public void OffsetTracking()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Hello\n# Title\nEnd\n"));
        var summary = parser.Complete();
        var heading = summary.Headings[0];
        Assert.Equal(6, heading.StartOffset);
    }

    [Fact]
    public void CurrentContentDuringParsing()
    {
        var parser = new PromptStreamParser();
        Assert.Empty(parser.CurrentContent);
        parser.Feed(Chunk("# Live\n"));
        Assert.Single(parser.CurrentContent);
        Assert.Equal("Live", parser.CurrentContent[0].Content);
    }

    [Fact]
    public void CompleteChunkFlushesIncomplete()
    {
        var parser = new PromptStreamParser(new StreamParserOptions { EmitPartial = true });
        var partials = new List<StreamContent>();
        parser.OnPartialContent += (_, e) => partials.Add(e.Content);
        parser.Feed(Chunk("```py\nincomplete", complete: true));
        Assert.Single(partials);
    }

    [Fact]
    public void JsonInCodeBlockNotExtracted()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("```json\n{\"in\": \"code\"}\n```\n"));
        var summary = parser.Complete();
        Assert.Single(summary.CodeBlocks);
        Assert.Empty(summary.JsonObjects);
    }

    [Fact]
    public void DuplicateKeyTakesLast()
    {
        var parser = new PromptStreamParser();
        parser.Feed(Chunk("Status: Draft\nStatus: Published\n"));
        var summary = parser.Complete();
        Assert.Equal("Published", summary.KeyValues["Status"]);
    }
}
