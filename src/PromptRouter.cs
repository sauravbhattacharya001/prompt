namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Routes user prompts to appropriate <see cref="PromptTemplate"/> instances
    /// based on intent classification. Uses keyword and regex matching with
    /// configurable scoring to determine the best template for a given input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each route defines a name, keywords (exact word matches, case-insensitive),
    /// optional regex patterns, a priority weight, and a template name. When
    /// <see cref="Route"/> is called, all routes are scored against the input
    /// and the highest-scoring route above the minimum threshold is selected.
    /// </para>
    /// <para>
    /// Integrates with <see cref="PromptLibrary"/> for template lookup:
    /// <code>
    /// var router = new PromptRouter(library);
    /// router.AddRoute("code-review", new RouteConfig
    /// {
    ///     Keywords = new[] { "review", "code", "bug", "fix", "refactor" },
    ///     Patterns = new[] { @"review\s+(this|my)\s+code", @"find\s+bugs?" },
    ///     TemplateName = "code-review",
    ///     Priority = 1.0
    /// });
    /// var match = router.Route("Can you review my code for bugs?");
    /// // match.RouteName == "code-review", match.Score > 0
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptRouter
    {
        private readonly Dictionary<string, RouteConfig> _routes = new(StringComparer.OrdinalIgnoreCase);
        private readonly PromptLibrary? _library;
        private string? _fallbackRoute;
        private double _minScore = 0.1;

        /// <summary>Create a standalone router (no library integration).</summary>
        public PromptRouter() { }

        /// <summary>Create a router backed by a <see cref="PromptLibrary"/>.</summary>
        public PromptRouter(PromptLibrary library)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        /// <summary>Number of registered routes.</summary>
        public int RouteCount => _routes.Count;

        /// <summary>Get or set the minimum score threshold for a match (0-1). Default 0.1.</summary>
        public double MinScore
        {
            get => _minScore;
            set => _minScore = Math.Clamp(value, 0, 1);
        }

        /// <summary>Add a route configuration.</summary>
        public PromptRouter AddRoute(string name, RouteConfig config)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Route name cannot be null or empty.", nameof(name));
            ArgumentNullException.ThrowIfNull(config);
            _routes[name] = config;
            return this;
        }

        /// <summary>Remove a route by name. Returns true if removed.</summary>
        public bool RemoveRoute(string name) => _routes.Remove(name);

        /// <summary>Check if a route exists.</summary>
        public bool HasRoute(string name) => _routes.ContainsKey(name);

        /// <summary>Get all route names.</summary>
        public IReadOnlyList<string> GetRouteNames() => _routes.Keys.ToList().AsReadOnly();

        /// <summary>Set a fallback route used when no route scores above MinScore.</summary>
        public PromptRouter WithFallback(string routeName)
        {
            _fallbackRoute = routeName;
            return this;
        }

        /// <summary>
        /// Route a prompt to the best matching template.
        /// Returns the match result with route name, score, and matched template name.
        /// Returns null if no route matches and no fallback is set.
        /// </summary>
        public RouteMatch? Route(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return _fallbackRoute != null ? _createFallbackMatch(input) : null;

            var scores = ScoreAll(input);
            var best = scores.OrderByDescending(s => s.Score).FirstOrDefault();

            if (best != null && best.Score >= _minScore)
                return best;

            return _fallbackRoute != null ? _createFallbackMatch(input) : null;
        }

        /// <summary>
        /// Score all routes against the input. Returns all scores, even zeros.
        /// Useful for debugging route configurations.
        /// </summary>
        public IReadOnlyList<RouteMatch> ScoreAll(string input)
        {
            var results = new List<RouteMatch>();
            var lowerInput = (input ?? "").ToLowerInvariant();
            var words = Regex.Split(lowerInput, @"\W+").Where(w => w.Length > 0).ToHashSet();

            foreach (var (name, config) in _routes)
            {
                double score = 0;

                // Keyword matching: proportion of keywords found
                if (config.Keywords is { Length: > 0 })
                {
                    int hits = config.Keywords.Count(kw =>
                        words.Contains(kw.ToLowerInvariant()));
                    score += (double)hits / config.Keywords.Length * 0.6;
                }

                // Regex pattern matching: any match adds bonus
                if (config.Patterns is { Length: > 0 })
                {
                    int patternHits = config.Patterns.Count(p =>
                        Regex.IsMatch(input, p, RegexOptions.IgnoreCase));
                    score += (double)patternHits / config.Patterns.Length * 0.4;
                }

                // Apply priority weight
                score *= config.Priority;

                results.Add(new RouteMatch
                {
                    RouteName = name,
                    Score = Math.Round(score, 4),
                    TemplateName = config.TemplateName,
                    IsFallback = false,
                    KeywordHits = config.Keywords?.Count(kw =>
                        words.Contains(kw.ToLowerInvariant())) ?? 0,
                    PatternHits = config.Patterns?.Count(p =>
                        Regex.IsMatch(input, p, RegexOptions.IgnoreCase)) ?? 0,
                });
            }

            return results.AsReadOnly();
        }

        /// <summary>
        /// Route and render: find the best template and render it with the input
        /// as a variable. Requires a PromptLibrary.
        /// </summary>
        public string? RouteAndRender(string input, Dictionary<string, string>? extraVars = null)
        {
            if (_library == null)
                throw new InvalidOperationException(
                    "RouteAndRender requires a PromptLibrary. Use the PromptRouter(PromptLibrary) constructor.");

            var match = Route(input);
            if (match == null) return null;

            if (!_library.TryGet(match.TemplateName, out var entry) || entry == null)
                return null;

            var vars = new Dictionary<string, string> { { "input", input } };
            if (extraVars != null)
                foreach (var (k, v) in extraVars) vars[k] = v;

            return entry.Template.Render(vars, strict: false);
        }

        /// <summary>Clear all routes.</summary>
        public void Clear()
        {
            _routes.Clear();
            _fallbackRoute = null;
        }

        // ── Serialization ───────────────────────────────────────

        /// <summary>Serialize router configuration to JSON.</summary>
        public string ToJson(bool indented = true)
        {
            var data = new RouterData
            {
                Routes = _routes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new RouteData
                    {
                        Keywords = kvp.Value.Keywords ?? Array.Empty<string>(),
                        Patterns = kvp.Value.Patterns ?? Array.Empty<string>(),
                        TemplateName = kvp.Value.TemplateName,
                        Priority = kvp.Value.Priority,
                    }),
                FallbackRoute = _fallbackRoute,
                MinScore = _minScore,
            };

            return JsonSerializer.Serialize(data, SerializationGuards.WriteOptions(indented));
        }

        /// <summary>Deserialize router configuration from JSON.</summary>
        public static PromptRouter FromJson(string json, PromptLibrary? library = null)
        {
            var data = JsonSerializer.Deserialize<RouterData>(json, SerializationGuards.ReadCamelCase);

            var router = library != null ? new PromptRouter(library) : new PromptRouter();

            if (data?.Routes != null)
            {
                foreach (var (name, rd) in data.Routes)
                {
                    router.AddRoute(name, new RouteConfig
                    {
                        Keywords = rd.Keywords,
                        Patterns = rd.Patterns,
                        TemplateName = rd.TemplateName,
                        Priority = rd.Priority,
                    });
                }
            }

            if (data?.FallbackRoute != null)
                router.WithFallback(data.FallbackRoute);
            if (data != null)
                router.MinScore = data.MinScore;

            return router;
        }

        /// <summary>Save router config to a JSON file.</summary>
        public async Task SaveToFileAsync(string path)
        {
            var json = ToJson();
            await File.WriteAllTextAsync(path, json);
        }

        /// <summary>Load router config from a JSON file.</summary>
        public static async Task<PromptRouter> LoadFromFileAsync(string path, PromptLibrary? library = null)
        {
            var json = await File.ReadAllTextAsync(path);
            return FromJson(json, library);
        }

        // ── Presets ─────────────────────────────────────────────

        /// <summary>
        /// Create a router pre-configured with common programming-related routes.
        /// Works with PromptLibrary.CreateDefault() template names.
        /// </summary>
        public static PromptRouter CreateDefault(PromptLibrary? library = null)
        {
            var router = library != null ? new PromptRouter(library) : new PromptRouter();

            router.AddRoute("code-review", new RouteConfig
            {
                Keywords = new[] { "review", "code", "bug", "fix", "refactor", "improve", "quality" },
                Patterns = new[] { @"review\s+(this|my)\s+code", @"find\s+bugs?" },
                TemplateName = "code-review",
                Priority = 1.0,
            });

            router.AddRoute("explain-code", new RouteConfig
            {
                Keywords = new[] { "explain", "understand", "what", "does", "how", "works", "mean" },
                Patterns = new[] { @"explain\s+(this|the)\s+code", @"what\s+does\s+this" },
                TemplateName = "explain-code",
                Priority = 1.0,
            });

            router.AddRoute("summarize", new RouteConfig
            {
                Keywords = new[] { "summarize", "summary", "tldr", "brief", "overview", "key", "points" },
                Patterns = new[] { @"summar(ize|y)", @"tl;?dr" },
                TemplateName = "summarize",
                Priority = 1.0,
            });

            router.AddRoute("translate", new RouteConfig
            {
                Keywords = new[] { "translate", "convert", "language", "spanish", "french", "german", "chinese", "japanese" },
                Patterns = new[] { @"translate\s+(to|into)", @"in\s+(spanish|french|german|chinese|japanese)" },
                TemplateName = "translate",
                Priority = 1.0,
            });

            router.AddRoute("generate-tests", new RouteConfig
            {
                Keywords = new[] { "test", "tests", "testing", "unit", "spec", "coverage", "generate" },
                Patterns = new[] { @"(write|generate|create)\s+tests?", @"unit\s+tests?" },
                TemplateName = "generate-tests",
                Priority = 1.0,
            });

            router.AddRoute("debug-error", new RouteConfig
            {
                Keywords = new[] { "error", "exception", "crash", "debug", "stack", "trace", "failing" },
                Patterns = new[] { @"(fix|debug)\s+(this|the)\s+error", @"stack\s*trace" },
                TemplateName = "debug-error",
                Priority = 1.0,
            });

            router.WithFallback("summarize");

            return router;
        }

        private RouteMatch _createFallbackMatch(string? input)
        {
            var config = _fallbackRoute != null && _routes.TryGetValue(_fallbackRoute, out var fc) ? fc : null;
            return new RouteMatch
            {
                RouteName = _fallbackRoute ?? "",
                Score = 0,
                TemplateName = config?.TemplateName ?? _fallbackRoute ?? "",
                IsFallback = true,
                KeywordHits = 0,
                PatternHits = 0,
            };
        }

        // ── Internal DTOs ───────────────────────────────────────

        private class RouterData
        {
            public Dictionary<string, RouteData> Routes { get; set; } = new();
            public string? FallbackRoute { get; set; }
            public double MinScore { get; set; } = 0.1;
        }

        private class RouteData
        {
            public string[] Keywords { get; set; } = Array.Empty<string>();
            public string[] Patterns { get; set; } = Array.Empty<string>();
            public string TemplateName { get; set; } = "";
            public double Priority { get; set; } = 1.0;
        }
    }

    /// <summary>Configuration for a single route.</summary>
    public class RouteConfig
    {
        /// <summary>Keywords to match (case-insensitive word matching).</summary>
        public string[] Keywords { get; set; } = Array.Empty<string>();

        /// <summary>Regex patterns to match (case-insensitive).</summary>
        public string[] Patterns { get; set; } = Array.Empty<string>();

        /// <summary>Template name to route to (looked up in PromptLibrary).</summary>
        public string TemplateName { get; set; } = "";

        /// <summary>Priority weight multiplier (default 1.0, higher = preferred).</summary>
        public double Priority { get; set; } = 1.0;
    }

    /// <summary>Result of routing a prompt.</summary>
    public class RouteMatch
    {
        /// <summary>Name of the matched route.</summary>
        public string RouteName { get; set; } = "";

        /// <summary>Match score (0-1, after priority weighting).</summary>
        public double Score { get; set; }

        /// <summary>Template name to use.</summary>
        public string TemplateName { get; set; } = "";

        /// <summary>Whether this is a fallback match.</summary>
        public bool IsFallback { get; set; }

        /// <summary>Number of keywords matched.</summary>
        public int KeywordHits { get; set; }

        /// <summary>Number of regex patterns matched.</summary>
        public int PatternHits { get; set; }
    }
}
