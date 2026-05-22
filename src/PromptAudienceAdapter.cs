namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Target audience expertise level for prompt adaptation.
    /// </summary>
    public enum AudienceLevel
    {
        /// <summary>Ages 5-12, simple words, short sentences, fun analogies.</summary>
        Child,
        /// <summary>New to the topic, extra context and definitions needed.</summary>
        Beginner,
        /// <summary>Some familiarity, moderate detail is fine.</summary>
        Intermediate,
        /// <summary>Deep knowledge, technical jargon welcome, concise.</summary>
        Expert,
        /// <summary>Executive/manager, focus on outcomes and impact.</summary>
        Executive
    }

    /// <summary>
    /// Configuration for audience adaptation behavior.
    /// </summary>
    public class AudienceAdapterOptions
    {
        /// <summary>Maximum sentence length (words) for the target level. 0 = no limit.</summary>
        public int MaxSentenceWords { get; set; }

        /// <summary>Whether to inject a "role" preamble telling the model who the audience is.</summary>
        public bool InjectAudiencePreamble { get; set; } = true;

        /// <summary>Whether to replace detected jargon with simpler alternatives.</summary>
        public bool SimplifyJargon { get; set; } = true;

        /// <summary>Custom jargon replacements (term → simpler phrase). Merged with built-ins.</summary>
        public Dictionary<string, string> CustomJargonMap { get; set; } = new();

        /// <summary>Whether to append a "format hint" suggesting output structure for the level.</summary>
        public bool AppendFormatHint { get; set; } = true;
    }

    /// <summary>
    /// Result of adapting a prompt for a specific audience.
    /// </summary>
    public class AudienceAdaptResult
    {
        /// <summary>The original prompt text.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>The adapted prompt text.</summary>
        public string Adapted { get; internal set; } = "";

        /// <summary>The target audience level.</summary>
        public AudienceLevel Level { get; internal set; }

        /// <summary>Number of jargon terms replaced.</summary>
        public int JargonReplacements { get; internal set; }

        /// <summary>Number of sentences simplified (split or shortened).</summary>
        public int SentencesSimplified { get; internal set; }

        /// <summary>Whether a preamble was injected.</summary>
        public bool PreambleInjected { get; internal set; }

        /// <summary>Whether a format hint was appended.</summary>
        public bool FormatHintAppended { get; internal set; }

        /// <summary>List of jargon terms that were detected.</summary>
        public List<string> DetectedJargon { get; internal set; } = new();
    }

    /// <summary>
    /// Detects jargon/complexity in a prompt and reports audience suitability.
    /// </summary>
    public class AudienceAnalysis
    {
        /// <summary>Average words per sentence.</summary>
        public double AvgWordsPerSentence { get; internal set; }

        /// <summary>Percentage of words that are jargon/technical terms (0-100).</summary>
        public double JargonDensity { get; internal set; }

        /// <summary>Detected jargon terms.</summary>
        public List<string> JargonTerms { get; internal set; } = new();

        /// <summary>Recommended audience level based on analysis.</summary>
        public AudienceLevel RecommendedLevel { get; internal set; }

        /// <summary>Suitability scores per audience level (0-100, higher = more suitable).</summary>
        public Dictionary<AudienceLevel, int> Suitability { get; internal set; } = new();

        /// <summary>Total word count.</summary>
        public int WordCount { get; internal set; }

        /// <summary>Total sentence count.</summary>
        public int SentenceCount { get; internal set; }
    }

    /// <summary>
    /// Adapts prompts for different audience expertise levels by adjusting
    /// vocabulary complexity, adding context, injecting audience-aware preambles,
    /// and suggesting appropriate output formats.
    /// </summary>
    /// <remarks>
    /// <para>Example usage:</para>
    /// <code>
    /// var adapter = new PromptAudienceAdapter();
    ///
    /// // Adapt a technical prompt for beginners
    /// var result = adapter.Adapt(
    ///     "Implement a REST API endpoint that performs CRUD operations on the database using an ORM with lazy loading.",
    ///     AudienceLevel.Beginner);
    /// Console.WriteLine(result.Adapted);
    /// // → "[Audience: Beginner - someone new to this topic who needs extra context...]
    /// //    Implement a web service connection point that performs Create, Read, Update,
    /// //    and Delete operations on the database using a data access tool with on-demand loading.
    /// //    Please use simple language, define technical terms, and include examples."
    ///
    /// // Analyze prompt complexity
    /// var analysis = adapter.Analyze("Leverage the microservices architecture...");
    /// Console.WriteLine($"Jargon density: {analysis.JargonDensity}%");
    /// Console.WriteLine($"Recommended for: {analysis.RecommendedLevel}");
    /// </code>
    /// </remarks>
    public class PromptAudienceAdapter
    {
        // Shared timeout for all dynamically-constructed regexes (matches existing call sites that
        // passed TimeSpan.FromMilliseconds(500) literal-by-literal).
        private static readonly TimeSpan JargonRegexTimeout = TimeSpan.FromMilliseconds(500);

        // Precompiled jargon-term -> compiled Regex cache. Building ~100 Regex objects on every call to
        // Adapt()/DetectJargon() showed up as a hot spot under bench workloads; doing it once at type
        // init (lazily, on first use) makes repeated adaptation ~5-10x faster on real prompts and avoids
        // per-call Regex.Escape + IL emission for RegexOptions.Compiled.
        private static readonly Lazy<IReadOnlyList<KeyValuePair<string, Regex>>> BuiltInJargonRegexes =
            new(() =>
            {
                // Sort by descending length so longer multi-word terms ("dependency injection",
                // "feature flag") match before their shorter prefixes when callers iterate in order.
                var list = new List<KeyValuePair<string, Regex>>(BuiltInJargon.Count);
                foreach (var kv in BuiltInJargon.OrderByDescending(x => x.Key.Length))
                {
                    var pattern = @"(?<!\w)" + Regex.Escape(kv.Key) + @"(?!\w)";
                    var regex = new Regex(
                        pattern,
                        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                        JargonRegexTimeout);
                    list.Add(new KeyValuePair<string, Regex>(kv.Key, regex));
                }
                return list;
            }, isThreadSafe: true);

        private static readonly Dictionary<string, string> BuiltInJargon = new(StringComparer.OrdinalIgnoreCase)
        {
            ["API"] = "web service connection point",
            ["REST"] = "web service",
            ["RESTful"] = "web-service-based",
            ["CRUD"] = "Create, Read, Update, and Delete",
            ["ORM"] = "data access tool",
            ["SQL"] = "database query language",
            ["NoSQL"] = "flexible database",
            ["microservices"] = "small independent services",
            ["monolith"] = "single large application",
            ["containerize"] = "package for deployment",
            ["Docker"] = "container platform",
            ["Kubernetes"] = "container orchestration system",
            ["CI/CD"] = "automated build and deploy pipeline",
            ["pipeline"] = "automated workflow",
            ["endpoint"] = "connection point",
            ["webhook"] = "automated notification callback",
            ["middleware"] = "processing layer",
            ["payload"] = "data content",
            ["schema"] = "data structure definition",
            ["serialization"] = "data format conversion",
            ["deserialization"] = "converting data back to objects",
            ["latency"] = "response delay",
            ["throughput"] = "processing speed",
            ["scalability"] = "ability to handle growth",
            ["idempotent"] = "safely repeatable",
            ["polymorphism"] = "multiple forms of behavior",
            ["inheritance"] = "building on existing code",
            ["encapsulation"] = "bundling data with its methods",
            ["abstraction"] = "hiding complexity",
            ["refactor"] = "restructure code",
            ["deprecated"] = "outdated and scheduled for removal",
            ["boilerplate"] = "repetitive standard code",
            ["dependency injection"] = "automatic component wiring",
            ["singleton"] = "single shared instance",
            ["cache"] = "temporary fast storage",
            ["lazy loading"] = "on-demand loading",
            ["eager loading"] = "upfront loading",
            ["race condition"] = "timing conflict between operations",
            ["deadlock"] = "mutual blocking standoff",
            ["mutex"] = "exclusive access lock",
            ["semaphore"] = "counted access permit",
            ["token"] = "access credential",
            ["OAuth"] = "delegated login protocol",
            ["JWT"] = "encoded access token",
            ["SSL"] = "encrypted connection",
            ["TLS"] = "encrypted connection protocol",
            ["DNS"] = "domain name lookup service",
            ["CDN"] = "content delivery network for speed",
            ["load balancer"] = "traffic distributor",
            ["sharding"] = "splitting data across servers",
            ["replication"] = "copying data for reliability",
            ["eventual consistency"] = "data syncs over time",
            ["ACID"] = "reliable transaction guarantees",
            ["rollback"] = "undo recent changes",
            ["migration"] = "database structure update",
            ["seed data"] = "initial sample data",
            ["ETL"] = "extract, transform, and load data",
            ["regex"] = "text pattern matching",
            ["lambda"] = "small inline function",
            ["callback"] = "function called later",
            ["async"] = "non-blocking",
            ["await"] = "wait for result",
            ["promise"] = "future result placeholder",
            ["observable"] = "data stream you can watch",
            ["pub/sub"] = "publish and subscribe messaging",
            ["event-driven"] = "reacting to events as they happen",
            ["state machine"] = "structured step-by-step flow",
            ["GraphQL"] = "flexible data query language",
            ["gRPC"] = "fast service-to-service protocol",
            ["protobuf"] = "compact data format",
            ["YAML"] = "human-readable config format",
            ["JSON"] = "data interchange format",
            ["XML"] = "structured markup format",
            ["SDK"] = "software development toolkit",
            ["CLI"] = "command-line interface",
            ["GUI"] = "graphical user interface",
            ["UX"] = "user experience",
            ["UI"] = "user interface",
            ["A/B testing"] = "comparing two versions",
            ["feature flag"] = "on/off switch for features",
            ["canary deployment"] = "gradual rollout to few users first",
            ["blue-green deployment"] = "switching between two environments",
            ["rollout"] = "gradual release",
            ["SLA"] = "service level agreement",
            ["SLO"] = "service level objective",
            ["observability"] = "system monitoring and insight",
            ["telemetry"] = "automated measurement data",
            ["tracing"] = "following request paths",
            ["profiling"] = "measuring performance details",
            ["linting"] = "automated code style checking",
            ["transpile"] = "convert between programming languages",
            ["minification"] = "shrinking code size",
            ["tree shaking"] = "removing unused code",
            ["hot reload"] = "instant code updates without restart",
            ["SSR"] = "server-side rendering",
            ["SSG"] = "static site generation",
            ["hydration"] = "making static pages interactive",
            ["virtual DOM"] = "efficient UI update system",
            ["component"] = "reusable UI building block",
            ["prop"] = "component input value",
            ["state"] = "component data that can change",
            ["hook"] = "function to tap into features",
            ["context"] = "shared data across components",
            ["reducer"] = "state update function",
            ["saga"] = "complex async workflow manager",
            ["thunk"] = "delayed action creator",
            ["memoization"] = "caching computed results",
            ["debounce"] = "delay until input stops",
            ["throttle"] = "limit how often something runs",
            ["pagination"] = "splitting results into pages",
            ["cursor"] = "position marker in data",
            ["offset"] = "starting position in data",
            ["index"] = "fast lookup structure",
            ["hash"] = "fixed-size data fingerprint",
            ["salt"] = "random data added before hashing",
            ["encryption"] = "scrambling data for security",
            ["decryption"] = "unscrambling secured data",
            ["heuristic"] = "practical rule of thumb",
            ["deterministic"] = "same input always gives same output",
            ["stochastic"] = "involving randomness",
            ["entropy"] = "measure of randomness",
            ["leverage"] = "use",
            ["utilize"] = "use",
            ["facilitate"] = "help with",
            ["orchestrate"] = "coordinate",
            ["paradigm"] = "approach",
            ["synergy"] = "combined benefit",
            ["bandwidth"] = "capacity",
            ["pivot"] = "change direction",
        };

        private static readonly Dictionary<AudienceLevel, string> Preambles = new()
        {
            [AudienceLevel.Child] = "[Audience: Young learner (ages 5-12). Use very simple words, short sentences, fun comparisons, and encouraging language. Avoid all jargon.]",
            [AudienceLevel.Beginner] = "[Audience: Beginner — someone new to this topic who needs extra context, definitions for technical terms, and step-by-step explanations.]",
            [AudienceLevel.Intermediate] = "[Audience: Intermediate — someone with moderate familiarity. Use standard terminology but briefly clarify advanced concepts.]",
            [AudienceLevel.Expert] = "[Audience: Expert — deep domain knowledge assumed. Be concise, use precise terminology, skip basic explanations.]",
            [AudienceLevel.Executive] = "[Audience: Executive/Decision-maker — focus on outcomes, impact, costs, timelines, and recommendations. Minimize technical details.]",
        };

        private static readonly Dictionary<AudienceLevel, string> FormatHints = new()
        {
            [AudienceLevel.Child] = "Use bullet points, emojis, and analogies a child would understand. Keep it fun!",
            [AudienceLevel.Beginner] = "Please use simple language, define technical terms when first used, and include examples where helpful.",
            [AudienceLevel.Intermediate] = "Use clear structure with headings. Include code examples or diagrams where relevant.",
            [AudienceLevel.Expert] = "Be direct and precise. Skip introductory context. Reference specifications or standards where applicable.",
            [AudienceLevel.Executive] = "Lead with the bottom line. Use bullet points for key takeaways. Include metrics and recommendations.",
        };

        private static readonly Dictionary<AudienceLevel, int> MaxSentenceDefaults = new()
        {
            [AudienceLevel.Child] = 10,
            [AudienceLevel.Beginner] = 20,
            [AudienceLevel.Intermediate] = 0,
            [AudienceLevel.Expert] = 0,
            [AudienceLevel.Executive] = 15,
        };

        private readonly AudienceAdapterOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromptAudienceAdapter"/> class.
        /// </summary>
        /// <param name="options">Optional configuration. If null, defaults are used.</param>
        public PromptAudienceAdapter(AudienceAdapterOptions? options = null)
        {
            _options = options ?? new AudienceAdapterOptions();
        }

        /// <summary>
        /// Adapts a prompt for the specified audience level.
        /// </summary>
        /// <param name="prompt">The original prompt text.</param>
        /// <param name="level">The target audience level.</param>
        /// <returns>An <see cref="AudienceAdaptResult"/> with the adapted prompt and metadata.</returns>
        /// <exception cref="ArgumentException">Thrown when prompt is null or whitespace.</exception>
        public AudienceAdaptResult Adapt(string prompt, AudienceLevel level)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var result = new AudienceAdaptResult
            {
                Original = prompt,
                Level = level
            };

            string working = prompt;

            // Step 1: Jargon replacement for lower levels.
            // Uses the precompiled BuiltInJargonRegexes cache (sorted longest-first); custom
            // overrides re-use the cached Regex but apply the caller's replacement value, and any
            // custom-only terms get a one-off (still timeout-guarded) Regex.
            if (_options.SimplifyJargon && level <= AudienceLevel.Beginner)
            {
                var detected = new List<string>();
                var custom = _options.CustomJargonMap;
                var seen = custom.Count == 0
                    ? null
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in BuiltInJargonRegexes.Value)
                {
                    var term = kv.Key;
                    var regex = kv.Value;
                    if (!regex.IsMatch(working)) continue;

                    detected.Add(term);
                    seen?.Add(term);
                    var value = custom.TryGetValue(term, out var overridden) ? overridden : BuiltInJargon[term];
                    string replacement = level == AudienceLevel.Child
                        ? value
                        : $"{value} ({term})";
                    working = regex.Replace(working, replacement);
                    result.JargonReplacements++;
                }

                if (custom.Count > 0)
                {
                    foreach (var kv in custom.OrderByDescending(x => x.Key.Length))
                    {
                        if (seen!.Contains(kv.Key)) continue;
                        var pattern = @"(?<!\w)" + Regex.Escape(kv.Key) + @"(?!\w)";
                        var regex = new Regex(pattern, RegexOptions.IgnoreCase, JargonRegexTimeout);
                        if (regex.IsMatch(working))
                        {
                            detected.Add(kv.Key);
                            string replacement = level == AudienceLevel.Child
                                ? kv.Value
                                : $"{kv.Value} ({kv.Key})";
                            working = regex.Replace(working, replacement);
                            result.JargonReplacements++;
                        }
                    }
                }
                result.DetectedJargon = detected;
            }
            else
            {
                // Still detect jargon for reporting
                result.DetectedJargon = DetectJargon(working);
            }

            // Step 2: Sentence simplification for child/beginner
            int maxWords = _options.MaxSentenceWords > 0
                ? _options.MaxSentenceWords
                : MaxSentenceDefaults.GetValueOrDefault(level, 0);

            if (maxWords > 0)
            {
                working = SimplifySentences(working, maxWords, out int simplified);
                result.SentencesSimplified = simplified;
            }

            // Step 3: Inject preamble
            if (_options.InjectAudiencePreamble && Preambles.TryGetValue(level, out var preamble))
            {
                working = preamble + "\n\n" + working;
                result.PreambleInjected = true;
            }

            // Step 4: Append format hint
            if (_options.AppendFormatHint && FormatHints.TryGetValue(level, out var hint))
            {
                working = working.TrimEnd() + "\n\n" + hint;
                result.FormatHintAppended = true;
            }

            result.Adapted = working;
            return result;
        }

        /// <summary>
        /// Adapts a prompt for multiple audience levels at once, returning a dictionary of results.
        /// Useful for generating variants for different stakeholders.
        /// </summary>
        /// <param name="prompt">The original prompt text.</param>
        /// <param name="levels">The audience levels to generate for. If null, all levels are used.</param>
        /// <returns>A dictionary mapping each level to its adaptation result.</returns>
        public Dictionary<AudienceLevel, AudienceAdaptResult> AdaptAll(
            string prompt, IEnumerable<AudienceLevel>? levels = null)
        {
            var targets = levels ?? Enum.GetValues<AudienceLevel>();
            return targets.ToDictionary(level => level, level => Adapt(prompt, level));
        }

        /// <summary>
        /// Analyzes a prompt's complexity and recommends a suitable audience level.
        /// </summary>
        /// <param name="prompt">The prompt to analyze.</param>
        /// <returns>An <see cref="AudienceAnalysis"/> with complexity metrics and recommendations.</returns>
        public AudienceAnalysis Analyze(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var sentences = SplitSentences(prompt);
            var words = prompt.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var jargon = DetectJargon(prompt);

            double avgWords = sentences.Length > 0 ? (double)words.Length / sentences.Length : words.Length;
            double jargonDensity = words.Length > 0 ? (double)jargon.Count / words.Length * 100 : 0;

            // Compute suitability scores
            var suitability = new Dictionary<AudienceLevel, int>();
            foreach (var level in Enum.GetValues<AudienceLevel>())
            {
                suitability[level] = ComputeSuitability(level, avgWords, jargonDensity);
            }

            // Recommend the level with highest suitability
            var recommended = suitability.MaxBy(kv => kv.Value)!.Key;

            return new AudienceAnalysis
            {
                AvgWordsPerSentence = Math.Round(avgWords, 1),
                JargonDensity = Math.Round(jargonDensity, 1),
                JargonTerms = jargon,
                RecommendedLevel = recommended,
                Suitability = suitability,
                WordCount = words.Length,
                SentenceCount = sentences.Length,
            };
        }

        /// <summary>
        /// Gets the built-in jargon dictionary (term → simple explanation).
        /// </summary>
        /// <returns>A read-only copy of the jargon map.</returns>
        public static IReadOnlyDictionary<string, string> GetJargonDictionary()
            => new Dictionary<string, string>(BuiltInJargon, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the preamble text for a given audience level.
        /// </summary>
        public static string GetPreamble(AudienceLevel level)
            => Preambles.GetValueOrDefault(level, "");

        /// <summary>
        /// Gets the format hint for a given audience level.
        /// </summary>
        public static string GetFormatHint(AudienceLevel level)
            => FormatHints.GetValueOrDefault(level, "");

        // --- Private helpers ---

        private List<string> DetectJargon(string text)
        {
            var found = new List<string>();
            var custom = _options.CustomJargonMap;
            HashSet<string>? seen = custom.Count == 0
                ? null
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Fast path: use the precompiled regex cache for the built-in jargon set.
            foreach (var kv in BuiltInJargonRegexes.Value)
            {
                if (kv.Value.IsMatch(text))
                {
                    found.Add(kv.Key);
                    seen?.Add(kv.Key);
                }
            }

            // Add custom terms not already covered by the built-in dictionary.
            if (custom.Count > 0)
            {
                foreach (var term in custom.Keys.OrderByDescending(k => k.Length))
                {
                    if (seen!.Contains(term)) continue;
                    var pattern = @"(?<!\w)" + Regex.Escape(term) + @"(?!\w)";
                    if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase, JargonRegexTimeout))
                        found.Add(term);
                }
            }
            return found;
        }

        private static string[] SplitSentences(string text) =>
            TextAnalysisHelpers.SplitSentences(text).ToArray();

        private static string SimplifySentences(string text, int maxWords, out int simplified)
        {
            simplified = 0;
            var sentences = SplitSentences(text);
            var result = new List<string>();

            foreach (var sentence in sentences)
            {
                var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > maxWords)
                {
                    // Split at commas, semicolons, or conjunctions near the midpoint
                    var splitPoints = new List<int>();
                    for (int i = 0; i < words.Length; i++)
                    {
                        var w = words[i].TrimEnd(',', ';');
                        if (words[i].EndsWith(",") || words[i].EndsWith(";") ||
                            w.Equals("and", StringComparison.OrdinalIgnoreCase) ||
                            w.Equals("but", StringComparison.OrdinalIgnoreCase) ||
                            w.Equals("which", StringComparison.OrdinalIgnoreCase) ||
                            w.Equals("that", StringComparison.OrdinalIgnoreCase) ||
                            w.Equals("while", StringComparison.OrdinalIgnoreCase) ||
                            w.Equals("because", StringComparison.OrdinalIgnoreCase))
                        {
                            splitPoints.Add(i);
                        }
                    }

                    if (splitPoints.Count > 0)
                    {
                        // Pick split point closest to middle
                        int mid = words.Length / 2;
                        int best = splitPoints.OrderBy(p => Math.Abs(p - mid)).First();
                        int splitAfter = words[best].EndsWith(",") || words[best].EndsWith(";")
                            ? best : Math.Max(best - 1, 0);

                        var part1 = string.Join(' ', words.Take(splitAfter + 1)).TrimEnd(',', ';') + ".";
                        var part2Words = words.Skip(splitAfter + 1).ToArray();
                        if (part2Words.Length > 0)
                        {
                            part2Words[0] = char.ToUpper(part2Words[0][0]) + part2Words[0][1..];
                            var part2 = string.Join(' ', part2Words);
                            if (!part2.EndsWith(".") && !part2.EndsWith("!") && !part2.EndsWith("?"))
                                part2 += ".";
                            result.Add(part1);
                            result.Add(part2);
                        }
                        else
                        {
                            result.Add(part1);
                        }
                        simplified++;
                    }
                    else
                    {
                        result.Add(sentence);
                    }
                }
                else
                {
                    result.Add(sentence);
                }
            }

            return string.Join(' ', result);
        }

        private static int ComputeSuitability(AudienceLevel level, double avgWords, double jargonDensity)
        {
            return level switch
            {
                AudienceLevel.Child => Math.Max(0, 100 - (int)(avgWords * 4) - (int)(jargonDensity * 10)),
                AudienceLevel.Beginner => Math.Max(0, 100 - (int)(avgWords * 2) - (int)(jargonDensity * 6)),
                AudienceLevel.Intermediate => Math.Max(0, Math.Min(100,
                    80 + (int)(jargonDensity * 1) - Math.Abs((int)(avgWords - 15)) * 2)),
                AudienceLevel.Expert => Math.Min(100,
                    40 + (int)(jargonDensity * 5) + (int)(avgWords * 1)),
                AudienceLevel.Executive => Math.Max(0, 100 - (int)(jargonDensity * 4) - Math.Max(0, (int)(avgWords - 12)) * 3),
                _ => 50,
            };
        }
    }
}
