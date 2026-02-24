namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptRouter"/>, <see cref="RouteConfig"/>,
/// and <see cref="RouteMatch"/> — construction, routing, scoring,
/// serialization, presets, and library integration.
/// </summary>
public class PromptRouterTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    private string GetTempFile()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        return path;
    }

    private static RouteConfig MakeConfig(
        string[] keywords,
        string[]? patterns = null,
        string templateName = "test-template",
        double priority = 1.0)
    {
        return new RouteConfig
        {
            Keywords = keywords,
            Patterns = patterns ?? Array.Empty<string>(),
            TemplateName = templateName,
            Priority = priority,
        };
    }

    // ═══════════════════════════════════════════════════════
    //  Constructor
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Constructor_Default_CreatesEmptyRouter()
    {
        var router = new PromptRouter();
        Assert.Equal(0, router.RouteCount);
    }

    [Fact]
    public void Constructor_WithLibrary_StoresReference()
    {
        var library = new PromptLibrary();
        var router = new PromptRouter(library);
        Assert.Equal(0, router.RouteCount);
        // No exception means the library was accepted
    }

    [Fact]
    public void Constructor_NullLibrary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PromptRouter(null!));
    }

    // ═══════════════════════════════════════════════════════
    //  AddRoute
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void AddRoute_ValidRoute_IncreasesRouteCount()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));
        Assert.Equal(1, router.RouteCount);
    }

    [Fact]
    public void AddRoute_NullName_Throws()
    {
        var router = new PromptRouter();
        Assert.Throws<ArgumentException>(() =>
            router.AddRoute(null!, MakeConfig(new[] { "hello" })));
    }

    [Fact]
    public void AddRoute_EmptyName_Throws()
    {
        var router = new PromptRouter();
        Assert.Throws<ArgumentException>(() =>
            router.AddRoute("", MakeConfig(new[] { "hello" })));
    }

    [Fact]
    public void AddRoute_WhitespaceName_Throws()
    {
        var router = new PromptRouter();
        Assert.Throws<ArgumentException>(() =>
            router.AddRoute("   ", MakeConfig(new[] { "hello" })));
    }

    [Fact]
    public void AddRoute_NullConfig_Throws()
    {
        var router = new PromptRouter();
        Assert.Throws<ArgumentNullException>(() =>
            router.AddRoute("test", null!));
    }

    [Fact]
    public void AddRoute_SameName_OverwritesExisting()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }, templateName: "first"));
        router.AddRoute("test", MakeConfig(new[] { "world" }, templateName: "second"));
        Assert.Equal(1, router.RouteCount);
        // Route should use the second config
        var match = router.Route("world");
        Assert.NotNull(match);
        Assert.Equal("second", match!.TemplateName);
    }

    [Fact]
    public void AddRoute_CaseInsensitiveNames()
    {
        var router = new PromptRouter();
        router.AddRoute("Test", MakeConfig(new[] { "hello" }));
        Assert.True(router.HasRoute("test"));
        Assert.True(router.HasRoute("TEST"));
        Assert.True(router.HasRoute("Test"));
    }

    [Fact]
    public void AddRoute_FluentApi_ReturnsSameInstance()
    {
        var router = new PromptRouter();
        var result = router.AddRoute("test", MakeConfig(new[] { "hello" }));
        Assert.Same(router, result);
    }

    // ═══════════════════════════════════════════════════════
    //  RemoveRoute
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void RemoveRoute_Existing_ReturnsTrue()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));
        Assert.True(router.RemoveRoute("test"));
    }

    [Fact]
    public void RemoveRoute_NonExisting_ReturnsFalse()
    {
        var router = new PromptRouter();
        Assert.False(router.RemoveRoute("nope"));
    }

    [Fact]
    public void RemoveRoute_DecreasesRouteCount()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));
        router.RemoveRoute("test");
        Assert.Equal(0, router.RouteCount);
    }

    // ═══════════════════════════════════════════════════════
    //  HasRoute
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void HasRoute_Existing_ReturnsTrue()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));
        Assert.True(router.HasRoute("test"));
    }

    [Fact]
    public void HasRoute_NonExisting_ReturnsFalse()
    {
        var router = new PromptRouter();
        Assert.False(router.HasRoute("nope"));
    }

    [Fact]
    public void HasRoute_CaseInsensitive()
    {
        var router = new PromptRouter();
        router.AddRoute("MyRoute", MakeConfig(new[] { "hello" }));
        Assert.True(router.HasRoute("myroute"));
        Assert.True(router.HasRoute("MYROUTE"));
    }

    // ═══════════════════════════════════════════════════════
    //  GetRouteNames
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void GetRouteNames_ReturnsAllNames()
    {
        var router = new PromptRouter();
        router.AddRoute("alpha", MakeConfig(new[] { "a" }));
        router.AddRoute("beta", MakeConfig(new[] { "b" }));
        router.AddRoute("gamma", MakeConfig(new[] { "c" }));

        var names = router.GetRouteNames();
        Assert.Equal(3, names.Count);
        Assert.Contains("alpha", names);
        Assert.Contains("beta", names);
        Assert.Contains("gamma", names);
    }

    [Fact]
    public void GetRouteNames_Empty_ReturnsEmptyList()
    {
        var router = new PromptRouter();
        var names = router.GetRouteNames();
        Assert.Empty(names);
    }

    [Fact]
    public void GetRouteNames_ReturnsCopy()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));
        var names1 = router.GetRouteNames();
        var names2 = router.GetRouteNames();
        Assert.NotSame(names1, names2);
    }

    // ═══════════════════════════════════════════════════════
    //  Route (core)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Route_MatchesByKeywords()
    {
        var router = new PromptRouter();
        router.AddRoute("greet", MakeConfig(new[] { "hello", "hi", "hey" },
            templateName: "greeting"));

        var match = router.Route("hello world");
        Assert.NotNull(match);
        Assert.Equal("greet", match!.RouteName);
        Assert.Equal("greeting", match.TemplateName);
    }

    [Fact]
    public void Route_MatchesByPatterns()
    {
        var router = new PromptRouter();
        router.AddRoute("greet", new RouteConfig
        {
            Keywords = Array.Empty<string>(),
            Patterns = new[] { @"hello\s+world" },
            TemplateName = "greeting",
            Priority = 1.0,
        });

        var match = router.Route("hello world");
        Assert.NotNull(match);
        Assert.Equal("greet", match!.RouteName);
    }

    [Fact]
    public void Route_MatchesByBothKeywordsAndPatterns()
    {
        var router = new PromptRouter();
        router.AddRoute("greet", new RouteConfig
        {
            Keywords = new[] { "hello" },
            Patterns = new[] { @"hello\s+world" },
            TemplateName = "greeting",
            Priority = 1.0,
        });

        var match = router.Route("hello world");
        Assert.NotNull(match);
        Assert.True(match!.Score > 0);
        Assert.True(match.KeywordHits > 0);
        Assert.True(match.PatternHits > 0);
    }

    [Fact]
    public void Route_HighestScoreWins()
    {
        var router = new PromptRouter();
        router.AddRoute("low", MakeConfig(new[] { "hello", "big", "world", "test", "run" },
            templateName: "low-match"));
        router.AddRoute("high", MakeConfig(new[] { "hello", "world" },
            templateName: "high-match"));

        // "hello world" matches 1/5 keywords in "low" vs 2/2 in "high"
        var match = router.Route("hello world");
        Assert.NotNull(match);
        Assert.Equal("high", match!.RouteName);
    }

    [Fact]
    public void Route_NoMatch_NoFallback_ReturnsNull()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "specific" }));

        var match = router.Route("completely unrelated");
        Assert.Null(match);
    }

    [Fact]
    public void Route_NoMatch_WithFallback_ReturnsFallback()
    {
        var router = new PromptRouter();
        router.AddRoute("fallback-route", MakeConfig(new[] { "specific" },
            templateName: "fallback-template"));
        router.WithFallback("fallback-route");

        var match = router.Route("completely unrelated");
        Assert.NotNull(match);
        Assert.Equal("fallback-route", match!.RouteName);
        Assert.True(match.IsFallback);
    }

    [Fact]
    public void Route_NullInput_NoFallback_ReturnsNull()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));

        var match = router.Route(null!);
        Assert.Null(match);
    }

    [Fact]
    public void Route_NullInput_WithFallback_ReturnsFallback()
    {
        var router = new PromptRouter();
        router.AddRoute("fb", MakeConfig(new[] { "hello" }, templateName: "fb-tmpl"));
        router.WithFallback("fb");

        var match = router.Route(null!);
        Assert.NotNull(match);
        Assert.True(match!.IsFallback);
    }

    [Fact]
    public void Route_EmptyInput_NoFallback_ReturnsNull()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));

        var match = router.Route("");
        Assert.Null(match);
    }

    [Fact]
    public void Route_EmptyInput_WithFallback_ReturnsFallback()
    {
        var router = new PromptRouter();
        router.AddRoute("fb", MakeConfig(new[] { "hello" }, templateName: "fb-tmpl"));
        router.WithFallback("fb");

        var match = router.Route("");
        Assert.NotNull(match);
        Assert.True(match!.IsFallback);
    }

    [Fact]
    public void Route_RespectsMinScoreThreshold()
    {
        var router = new PromptRouter();
        router.MinScore = 0.5;
        // Only 1 of 10 keywords matches → score = 0.06
        router.AddRoute("strict", MakeConfig(
            new[] { "hello", "a", "b", "c", "d", "e", "f", "g", "h", "i" },
            templateName: "strict-tmpl"));

        var match = router.Route("hello");
        Assert.Null(match); // score too low
    }

    [Fact]
    public void Route_IsFallback_True_ForFallbackMatches()
    {
        var router = new PromptRouter();
        router.AddRoute("fb", MakeConfig(new[] { "specific" }, templateName: "fb-tmpl"));
        router.WithFallback("fb");

        var match = router.Route("unrelated input");
        Assert.NotNull(match);
        Assert.True(match!.IsFallback);
    }

    [Fact]
    public void Route_IsFallback_False_ForNormalMatches()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }, templateName: "test-tmpl"));

        var match = router.Route("hello world");
        Assert.NotNull(match);
        Assert.False(match!.IsFallback);
    }

    [Fact]
    public void Route_ScoreReflectsKeywordProportion()
    {
        var router = new PromptRouter();
        router.AddRoute("half", MakeConfig(new[] { "hello", "world", "foo", "bar" }));

        var scores = router.ScoreAll("hello world");
        var score = scores.First();
        // 2/4 keywords * 0.6 = 0.3
        Assert.Equal(0.3, score.Score, 4);
        Assert.Equal(2, score.KeywordHits);
    }

    [Fact]
    public void Route_ScoreReflectsPatternProportion()
    {
        var router = new PromptRouter();
        router.AddRoute("pat", new RouteConfig
        {
            Keywords = Array.Empty<string>(),
            Patterns = new[] { @"hello\s+world", @"foo\s+bar" },
            TemplateName = "pat-tmpl",
            Priority = 1.0,
        });

        var scores = router.ScoreAll("hello world");
        var score = scores.First();
        // 1/2 patterns * 0.4 = 0.2
        Assert.Equal(0.2, score.Score, 4);
        Assert.Equal(1, score.PatternHits);
    }

    [Fact]
    public void Route_PriorityMultiplierAffectsScore()
    {
        var router = new PromptRouter();
        router.AddRoute("normal", MakeConfig(new[] { "hello" }, priority: 1.0));
        router.AddRoute("boosted", MakeConfig(new[] { "hello" }, priority: 2.0,
            templateName: "boosted-tmpl"));

        var scores = router.ScoreAll("hello");
        var normal = scores.First(s => s.RouteName == "normal");
        var boosted = scores.First(s => s.RouteName == "boosted");

        Assert.True(boosted.Score > normal.Score);
        Assert.Equal(normal.Score * 2, boosted.Score, 4);
    }

    [Fact]
    public void Route_HigherPriorityWins()
    {
        var router = new PromptRouter();
        router.AddRoute("low-pri", MakeConfig(new[] { "hello" }, priority: 0.5,
            templateName: "low"));
        router.AddRoute("high-pri", MakeConfig(new[] { "hello" }, priority: 2.0,
            templateName: "high"));

        var match = router.Route("hello");
        Assert.NotNull(match);
        Assert.Equal("high-pri", match!.RouteName);
    }

    // ═══════════════════════════════════════════════════════
    //  ScoreAll
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void ScoreAll_ReturnsScoresForAllRoutes()
    {
        var router = new PromptRouter();
        router.AddRoute("a", MakeConfig(new[] { "hello" }));
        router.AddRoute("b", MakeConfig(new[] { "world" }));
        router.AddRoute("c", MakeConfig(new[] { "foo" }));

        var scores = router.ScoreAll("hello");
        Assert.Equal(3, scores.Count);
    }

    [Fact]
    public void ScoreAll_IncludesZeroScoreRoutes()
    {
        var router = new PromptRouter();
        router.AddRoute("match", MakeConfig(new[] { "hello" }));
        router.AddRoute("nomatch", MakeConfig(new[] { "zzz" }));

        var scores = router.ScoreAll("hello");
        var noMatch = scores.First(s => s.RouteName == "nomatch");
        Assert.Equal(0, noMatch.Score);
    }

    [Fact]
    public void ScoreAll_AllScoresNonNegative()
    {
        var router = new PromptRouter();
        router.AddRoute("a", MakeConfig(new[] { "hello" }));
        router.AddRoute("b", MakeConfig(new[] { "world" }));

        var scores = router.ScoreAll("some random input");
        Assert.All(scores, s => Assert.True(s.Score >= 0));
    }

    // ═══════════════════════════════════════════════════════
    //  MinScore
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void MinScore_DefaultIs01()
    {
        var router = new PromptRouter();
        Assert.Equal(0.1, router.MinScore);
    }

    [Fact]
    public void MinScore_SetterClampsToRange()
    {
        var router = new PromptRouter();

        router.MinScore = -0.5;
        Assert.Equal(0, router.MinScore);

        router.MinScore = 1.5;
        Assert.Equal(1, router.MinScore);

        router.MinScore = 0.5;
        Assert.Equal(0.5, router.MinScore);
    }

    [Fact]
    public void MinScore_AffectsRouteResults()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(
            new[] { "hello", "world", "foo", "bar", "baz" }));

        // 1/5 keywords * 0.6 = 0.12
        router.MinScore = 0.0;
        Assert.NotNull(router.Route("hello"));

        router.MinScore = 0.5;
        Assert.Null(router.Route("hello"));
    }

    // ═══════════════════════════════════════════════════════
    //  WithFallback
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void WithFallback_SetsFallbackRoute()
    {
        var router = new PromptRouter();
        router.AddRoute("fb", MakeConfig(new[] { "x" }, templateName: "fb-tmpl"));
        router.WithFallback("fb");

        var match = router.Route("no match here");
        Assert.NotNull(match);
        Assert.Equal("fb", match!.RouteName);
        Assert.True(match.IsFallback);
    }

    [Fact]
    public void WithFallback_FluentApi_ReturnsSameInstance()
    {
        var router = new PromptRouter();
        var result = router.WithFallback("test");
        Assert.Same(router, result);
    }

    [Fact]
    public void WithFallback_UsedWhenNoMatchAboveThreshold()
    {
        var router = new PromptRouter();
        router.MinScore = 0.9;
        router.AddRoute("low", MakeConfig(new[] { "hello", "a", "b", "c", "d" },
            templateName: "low-tmpl"));
        router.AddRoute("fb", MakeConfig(new[] { "x" }, templateName: "fb-tmpl"));
        router.WithFallback("fb");

        // 1/5 * 0.6 = 0.12 < 0.9
        var match = router.Route("hello");
        Assert.NotNull(match);
        Assert.True(match!.IsFallback);
        Assert.Equal("fb", match.RouteName);
    }

    // ═══════════════════════════════════════════════════════
    //  RouteAndRender
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void RouteAndRender_RendersMatchingTemplate()
    {
        var library = new PromptLibrary();
        library.Add("greet", new PromptTemplate("Hello, {{input}}!"));

        var router = new PromptRouter(library);
        router.AddRoute("greet", MakeConfig(new[] { "greet", "hello" },
            templateName: "greet"));

        var result = router.RouteAndRender("greet me");
        Assert.NotNull(result);
        Assert.Contains("greet me", result!);
    }

    [Fact]
    public void RouteAndRender_ThrowsWhenNoLibrary()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }));

        Assert.Throws<InvalidOperationException>(() =>
            router.RouteAndRender("hello"));
    }

    [Fact]
    public void RouteAndRender_ReturnsNullWhenNoMatch()
    {
        var library = new PromptLibrary();
        library.Add("greet", new PromptTemplate("Hello, {{input}}!"));

        var router = new PromptRouter(library);
        router.AddRoute("greet", MakeConfig(new[] { "greet" }, templateName: "greet"));

        var result = router.RouteAndRender("completely unrelated input");
        Assert.Null(result);
    }

    [Fact]
    public void RouteAndRender_PassesExtraVariables()
    {
        var library = new PromptLibrary();
        library.Add("tmpl", new PromptTemplate("{{input}} in {{lang}}"));

        var router = new PromptRouter(library);
        router.AddRoute("test", MakeConfig(new[] { "translate" }, templateName: "tmpl"));

        var result = router.RouteAndRender("translate this",
            new Dictionary<string, string> { ["lang"] = "Spanish" });
        Assert.NotNull(result);
        Assert.Contains("Spanish", result!);
        Assert.Contains("translate this", result);
    }

    [Fact]
    public void RouteAndRender_ReturnsNullWhenTemplateNotFound()
    {
        var library = new PromptLibrary();
        // Don't add any templates

        var router = new PromptRouter(library);
        router.AddRoute("test", MakeConfig(new[] { "hello" },
            templateName: "nonexistent"));

        var result = router.RouteAndRender("hello there");
        Assert.Null(result);
    }

    // ═══════════════════════════════════════════════════════
    //  Clear
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Clear_RemovesAllRoutes()
    {
        var router = new PromptRouter();
        router.AddRoute("a", MakeConfig(new[] { "x" }));
        router.AddRoute("b", MakeConfig(new[] { "y" }));
        router.Clear();
        Assert.Equal(0, router.RouteCount);
    }

    [Fact]
    public void Clear_ClearsFallback()
    {
        var router = new PromptRouter();
        router.AddRoute("fb", MakeConfig(new[] { "x" }, templateName: "fb-tmpl"));
        router.WithFallback("fb");
        router.Clear();

        // After clear, fallback should be gone
        var match = router.Route("anything");
        Assert.Null(match);
    }

    [Fact]
    public void Clear_RouteCountIsZero()
    {
        var router = new PromptRouter();
        router.AddRoute("a", MakeConfig(new[] { "x" }));
        router.AddRoute("b", MakeConfig(new[] { "y" }));
        router.AddRoute("c", MakeConfig(new[] { "z" }));
        router.Clear();
        Assert.Equal(0, router.RouteCount);
        Assert.Empty(router.GetRouteNames());
    }

    // ═══════════════════════════════════════════════════════
    //  Serialization
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello", "world" },
            new[] { @"hello\s+world" }, "test-tmpl", 1.5));

        var json = router.ToJson();
        Assert.NotNull(json);

        // Should be valid JSON
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void FromJson_RoundtripPreservesRoutes()
    {
        var router = new PromptRouter();
        router.AddRoute("alpha", MakeConfig(new[] { "a", "b" },
            new[] { @"test\d+" }, "alpha-tmpl", 1.0));
        router.AddRoute("beta", MakeConfig(new[] { "c", "d" },
            templateName: "beta-tmpl", priority: 2.0));

        var json = router.ToJson();
        var restored = PromptRouter.FromJson(json);

        Assert.Equal(2, restored.RouteCount);
        Assert.True(restored.HasRoute("alpha"));
        Assert.True(restored.HasRoute("beta"));
    }

    [Fact]
    public void FromJson_RoundtripPreservesFallback()
    {
        var router = new PromptRouter();
        router.AddRoute("fb", MakeConfig(new[] { "x" }, templateName: "fb-tmpl"));
        router.WithFallback("fb");

        var json = router.ToJson();
        var restored = PromptRouter.FromJson(json);

        // The fallback should work
        var match = restored.Route("no match");
        Assert.NotNull(match);
        Assert.True(match!.IsFallback);
        Assert.Equal("fb", match.RouteName);
    }

    [Fact]
    public void FromJson_RoundtripPreservesMinScore()
    {
        var router = new PromptRouter();
        router.MinScore = 0.42;
        router.AddRoute("test", MakeConfig(new[] { "hello" }));

        var json = router.ToJson();
        var restored = PromptRouter.FromJson(json);

        Assert.Equal(0.42, restored.MinScore);
    }

    [Fact]
    public void FromJson_WithLibrary()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello" }, templateName: "test-tmpl"));

        var json = router.ToJson();
        var library = new PromptLibrary();
        var restored = PromptRouter.FromJson(json, library);

        Assert.Equal(1, restored.RouteCount);
    }

    [Fact]
    public async Task SaveToFileAsync_LoadFromFileAsync_Roundtrip()
    {
        var router = new PromptRouter();
        router.AddRoute("test", MakeConfig(new[] { "hello", "world" },
            new[] { @"hello\s+world" }, "test-tmpl", 1.5));
        router.WithFallback("test");
        router.MinScore = 0.25;

        var path = GetTempFile();
        await router.SaveToFileAsync(path);

        var restored = await PromptRouter.LoadFromFileAsync(path);
        Assert.Equal(1, restored.RouteCount);
        Assert.True(restored.HasRoute("test"));
        Assert.Equal(0.25, restored.MinScore);

        // Fallback should work
        var match = restored.Route("no match");
        Assert.NotNull(match);
        Assert.True(match!.IsFallback);
    }

    // ═══════════════════════════════════════════════════════
    //  CreateDefault
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void CreateDefault_Creates6Routes()
    {
        var router = PromptRouter.CreateDefault();
        Assert.Equal(6, router.RouteCount);
    }

    [Fact]
    public void CreateDefault_HasFallbackSet()
    {
        var router = PromptRouter.CreateDefault();

        // Fallback should be "summarize"
        var match = router.Route("xyzzy gibberish nothing matches");
        Assert.NotNull(match);
        Assert.True(match!.IsFallback);
        Assert.Equal("summarize", match.TemplateName);
    }

    [Fact]
    public void CreateDefault_Routes_ReviewMyCode_ToCodeReview()
    {
        var router = PromptRouter.CreateDefault();
        var match = router.Route("review my code for bugs");
        Assert.NotNull(match);
        Assert.Equal("code-review", match!.RouteName);
        Assert.Equal("code-review", match.TemplateName);
        Assert.False(match.IsFallback);
    }

    [Fact]
    public void CreateDefault_Routes_ExplainThisCode_ToExplainCode()
    {
        var router = PromptRouter.CreateDefault();
        var match = router.Route("explain this code please");
        Assert.NotNull(match);
        Assert.Equal("explain-code", match!.RouteName);
        Assert.Equal("explain-code", match.TemplateName);
        Assert.False(match.IsFallback);
    }

    [Fact]
    public void CreateDefault_Routes_SummarizeThis_ToSummarize()
    {
        var router = PromptRouter.CreateDefault();
        var match = router.Route("summarize this article for me");
        Assert.NotNull(match);
        Assert.Equal("summarize", match!.RouteName);
        Assert.Equal("summarize", match.TemplateName);
        Assert.False(match.IsFallback);
    }

    [Fact]
    public void CreateDefault_Routes_TranslateToSpanish_ToTranslate()
    {
        var router = PromptRouter.CreateDefault();
        var match = router.Route("translate to spanish please");
        Assert.NotNull(match);
        Assert.Equal("translate", match!.RouteName);
        Assert.Equal("translate", match.TemplateName);
        Assert.False(match.IsFallback);
    }

    [Fact]
    public void CreateDefault_Routes_WriteUnitTests_ToGenerateTests()
    {
        var router = PromptRouter.CreateDefault();
        var match = router.Route("write unit tests for this");
        Assert.NotNull(match);
        Assert.Equal("generate-tests", match!.RouteName);
        Assert.Equal("generate-tests", match.TemplateName);
        Assert.False(match.IsFallback);
    }

    [Fact]
    public void CreateDefault_Routes_DebugThisError_ToDebugError()
    {
        var router = PromptRouter.CreateDefault();
        var match = router.Route("debug this error with stack trace");
        Assert.NotNull(match);
        Assert.Equal("debug-error", match!.RouteName);
        Assert.Equal("debug-error", match.TemplateName);
        Assert.False(match.IsFallback);
    }

    // ═══════════════════════════════════════════════════════
    //  RouteConfig defaults
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void RouteConfig_DefaultValues()
    {
        var config = new RouteConfig();
        Assert.Empty(config.Keywords);
        Assert.Empty(config.Patterns);
        Assert.Equal("", config.TemplateName);
        Assert.Equal(1.0, config.Priority);
    }

    // ═══════════════════════════════════════════════════════
    //  RouteMatch defaults
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void RouteMatch_DefaultValues()
    {
        var match = new RouteMatch();
        Assert.Equal("", match.RouteName);
        Assert.Equal(0, match.Score);
        Assert.Equal("", match.TemplateName);
        Assert.False(match.IsFallback);
        Assert.Equal(0, match.KeywordHits);
        Assert.Equal(0, match.PatternHits);
    }
}
