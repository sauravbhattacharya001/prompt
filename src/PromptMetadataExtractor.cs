namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// High-level capability requirements that a prompt may need from a model
    /// (e.g. code generation, vision, web search). Used by routing layers to
    /// pick an appropriately capable model/tool.
    /// </summary>
    public enum PromptCapability
    {
        /// <summary>Standard natural-language text generation. Implied for every prompt.</summary>
        TextGeneration,
        /// <summary>Generating, editing, or explaining source code.</summary>
        CodeGeneration,
        /// <summary>Multi-step mathematical or numerical reasoning.</summary>
        MathReasoning,
        /// <summary>Image understanding or generation (vision-enabled models).</summary>
        VisionOrImage,
        /// <summary>Real-time / current information that requires web search.</summary>
        WebSearch,
        /// <summary>Parsing or extracting from documents (PDF, DOCX, spreadsheets).</summary>
        DocumentProcessing,
        /// <summary>Analytics over structured datasets (statistics, visualizations).</summary>
        DataAnalysis,
        /// <summary>Translation between human languages.</summary>
        Translation,
        /// <summary>Condensing text into shorter summaries.</summary>
        Summarization,
        /// <summary>Producing machine-readable output (JSON / XML / CSV / YAML).</summary>
        StructuredOutput,
        /// <summary>Step-by-step or chain-of-thought reasoning.</summary>
        Reasoning,
        /// <summary>Invoking tools / function calls / external APIs.</summary>
        ToolUse
    }

    /// <summary>
    /// Subject-matter domain classification for a prompt. Used to route
    /// requests to domain-specialised models, prompt templates, or experts.
    /// </summary>
    public enum PromptDomain
    {
        /// <summary>No strong domain signal detected; default bucket.</summary>
        General,
        /// <summary>Software engineering, IT, cloud, developer tooling.</summary>
        Technology,
        /// <summary>Clinical or biomedical content (diagnoses, treatments).</summary>
        Medical,
        /// <summary>Contracts, litigation, regulatory compliance.</summary>
        Legal,
        /// <summary>Banking, investing, accounting, economics.</summary>
        Finance,
        /// <summary>Research papers, theses, scholarly writing.</summary>
        Academic,
        /// <summary>Branding, advertising, growth, copywriting.</summary>
        Marketing,
        /// <summary>Fiction, poetry, screenwriting, world-building.</summary>
        Creative,
        /// <summary>Natural science (physics, chemistry, biology, etc.).</summary>
        Science,
        /// <summary>General business operations, strategy, HR, procurement.</summary>
        Business
    }

    /// <summary>
    /// Tone / register detected in a prompt, from highly formal legalese
    /// to casual chat. Used by tone-aware routing and style adapters.
    /// </summary>
    public enum PromptFormality
    {
        /// <summary>Legalistic / archaic register ("hereby", "pursuant", "shall").</summary>
        Formal,
        /// <summary>Polite, business-appropriate ("please", "thank you", "regards").</summary>
        Professional,
        /// <summary>No strong signal in either direction.</summary>
        Neutral,
        /// <summary>Conversational with light informalities ("yeah", "cool").</summary>
        Casual,
        /// <summary>Heavy slang / chat shorthand ("lol", "bruh", emoji-heavy).</summary>
        Informal
    }

    /// <summary>
    /// Result of language detection on a prompt.
    /// </summary>
    public class DetectedLanguage
    {
        /// <summary>ISO 639-1 language code (e.g. "en", "es", "ja"). Defaults to "en".</summary>
        public string Code { get; internal set; } = "en";

        /// <summary>Human-readable language name (e.g. "English").</summary>
        public string Name { get; internal set; } = "English";

        /// <summary>
        /// Confidence score in <c>[0.0, 1.0]</c>. Derived from the number of
        /// language marker tokens matched, capped at 1.0.
        /// </summary>
        public double Confidence { get; internal set; } = 1.0;
    }

    /// <summary>
    /// A single entity extracted from prompt text (email, URL, file path,
    /// date, number, programming language, etc.).
    /// </summary>
    public class ExtractedEntity
    {
        /// <summary>Original substring that matched the entity pattern.</summary>
        public string Text { get; internal set; } = "";

        /// <summary>
        /// Entity type tag. One of <c>"email"</c>, <c>"url"</c>,
        /// <c>"file_path"</c>, <c>"date"</c>, <c>"number"</c>, or
        /// <c>"code_lang"</c>.
        /// </summary>
        public string Type { get; internal set; } = "";

        /// <summary>Zero-based character offset of the match in the source prompt.</summary>
        public int StartIndex { get; internal set; }
    }

    /// <summary>
    /// Full metadata extraction result for a prompt: language, capabilities,
    /// domain, tone, entities, structural counts and routing suggestion.
    /// </summary>
    public class PromptMetadata
    {
        /// <summary>Detected primary language of the prompt.</summary>
        public DetectedLanguage Language { get; internal set; } = new();

        /// <summary>
        /// Capabilities the prompt appears to require. Always contains at
        /// least <see cref="PromptCapability.TextGeneration"/>.
        /// </summary>
        public List<PromptCapability> Capabilities { get; internal set; } = new();

        /// <summary>Most likely subject-matter domain.</summary>
        public PromptDomain Domain { get; internal set; } = PromptDomain.General;

        /// <summary>Confidence of the <see cref="Domain"/> classification in <c>[0.0, 1.0]</c>.</summary>
        public double DomainConfidence { get; internal set; }

        /// <summary>Detected tone / formality register.</summary>
        public PromptFormality Tone { get; internal set; } = PromptFormality.Neutral;

        /// <summary>Entities (URLs, emails, dates, numbers, code-langs, paths) found in the prompt.</summary>
        public List<ExtractedEntity> Entities { get; internal set; } = new();

        /// <summary>Whitespace-delimited word count of the prompt body.</summary>
        public int WordCount { get; internal set; }

        /// <summary>
        /// Token estimate computed via <see cref="PromptGuard.EstimateTokens"/>.
        /// Approximate; intended for budget / routing decisions, not billing.
        /// </summary>
        public int EstimatedTokens { get; internal set; }

        /// <summary>Number of question-mark characters detected.</summary>
        public int QuestionCount { get; internal set; }

        /// <summary>Number of imperative instruction phrases detected (e.g. "write", "build").</summary>
        public int InstructionCount { get; internal set; }

        /// <summary>True when the prompt appears to include a few-shot example or code block.</summary>
        public bool HasExamples { get; internal set; }

        /// <summary>True when the prompt contains system-style directives ("You are", "Act as", "always", "never").</summary>
        public bool HasSystemDirectives { get; internal set; }

        /// <summary>
        /// Suggested routing tier. One of <c>"fast"</c>, <c>"standard"</c>,
        /// <c>"premium"</c>, or <c>"specialist"</c> — based on capability
        /// count, token budget, and domain confidence.
        /// </summary>
        public string RoutingSuggestion { get; internal set; } = "standard";

        /// <summary>
        /// Free-form key/value tags derived from the analysis (e.g.
        /// <c>word_count_bucket=short</c>, <c>pattern=few_shot</c>,
        /// <c>code_languages=python,sql</c>).
        /// </summary>
        public Dictionary<string, string> Tags { get; internal set; } = new();
    }

    /// <summary>
    /// Extracts structured metadata from prompt text: language detection, capability
    /// requirements, domain classification, tone analysis, entity extraction, and
    /// routing suggestions. Useful for prompt routing, analytics, and compliance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All work is local, deterministic, and based on compiled regex / keyword
    /// dictionaries (no network calls, no LLM round-trips). Regex evaluation is
    /// hardened with 500&#160;ms timeouts to avoid ReDoS.
    /// </para>
    /// <para>Example:
    /// <code>
    /// var extractor = new PromptMetadataExtractor();
    /// var meta = extractor.Extract("Write a Python function that calculates compound interest");
    /// // meta.Domain == Technology, meta.Capabilities contains CodeGeneration + MathReasoning
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptMetadataExtractor
    {
        // ── Compiled regex with ReDoS-safe 500ms timeouts ────────────

        private static readonly Regex EmailRx = new(
            @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex UrlRx = new(
            @"https?://[^\s<>""']+",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex FilePathRx = new(
            @"(?:[A-Za-z]:\\[\w\\.\-]+|/(?:[\w.\-]+/)+[\w.\-]+|\b[\w\-]+\.(?:py|js|ts|cs|java|cpp|rb|go|rs|swift|kt|dart|html|css|json|xml|yaml|yml|md|txt|csv|sql|sh|ps1))\b",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex DateRx = new(
            @"\b\d{4}[-/]\d{1,2}[-/]\d{1,2}\b|\b(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\w*\s+\d{1,2},?\s*\d{4}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex NumberRx = new(
            @"\b\d{1,3}(?:,\d{3})*(?:\.\d+)?%?\b|\$\d+(?:,\d{3})*(?:\.\d+)?",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex QuestionRx = new(
            @"[?\uff1f]",
            RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex InstructionRx = new(
            @"^\s*(?:\d+[.\)]\s+|[-*]\s+)?(?:write|create|generate|build|implement|design|explain|analyze|calculate|convert|translate|summarize|list|describe|compare|fix|debug|refactor|optimize|review|check|validate|test|format|parse|extract|find|search|classify|categorize|sort|filter|merge|split|combine|add|remove|update|modify|delete|insert)\b",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex CodeBlockRx = new(
            @"```[\s\S]*?```|`[^`\n]+`",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex ExampleRx = new(
            @"\b(?:for example|e\.g\.|example:|input:|output:|sample:|given:|expected:)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex SystemDirectiveRx = new(
            @"\b(?:you are|act as|behave as|your role|system:|instructions:|rules:|constraints:|you must|you should|always|never)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex JsonStructureRx = new(
            @"\{[\s\S]*?:[\s\S]*?\}|\[[\s\S]*?\]|(?:json|xml|csv|yaml)\s*(?:format|output|schema|response)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        // ── Language markers ─────────────────────────────────────────

        private static readonly Dictionary<string, (string Name, string[] Markers)> LangMarkers = new()
        {
            ["es"] = ("Spanish", new[] { " el ", " la ", " los ", " las ", " de ", " en ", " por ", " para ", " que ", " del ", " con ", " una ", " como " }),
            ["fr"] = ("French", new[] { " le ", " la ", " les ", " des ", " est ", " une ", " dans ", " pour ", " que ", " avec ", " sur ", " pas ", " sont " }),
            ["de"] = ("German", new[] { " der ", " die ", " das ", " ist ", " ein ", " eine ", " und ", " nicht ", " mit ", " auf ", " den ", " sich " }),
            ["pt"] = ("Portuguese", new[] { " de ", " que ", " em ", " para ", " com ", " uma ", " por ", " mais ", " como ", " seu ", " essa ", " isso " }),
            ["it"] = ("Italian", new[] { " il ", " di ", " che ", " la ", " per ", " una ", " del ", " con ", " sono ", " alla ", " della ", " nel " }),
            ["zh"] = ("Chinese", new[] { "\u7684", "\u662f", "\u4e86", "\u5728", "\u6709", "\u548c", "\u4eba", "\u8fd9", "\u4e2d", "\u4e0d" }),
            ["ja"] = ("Japanese", new[] { "\u306e", "\u306f", "\u3092", "\u306b", "\u304c", "\u3067", "\u3068", "\u305f", "\u3059\u308b", "\u3067\u3059" }),
            ["ko"] = ("Korean", new[] { "\uc740", "\ub294", "\uc774", "\uac00", "\ub97c", "\uc744", "\uc5d0", "\uc758", "\ub85c", "\ud558\ub2e4" }),
            ["ru"] = ("Russian", new[] { " \u0438 ", " \u0432 ", " \u043d\u0435 ", " \u043d\u0430 ", " \u0447\u0442\u043e ", " \u043e\u043d ", " \u043a\u0430\u043a ", " \u044d\u0442\u043e ", " \u0434\u043b\u044f ", " \u043f\u043e " }),
            ["ar"] = ("Arabic", new[] { "\u0641\u064a", "\u0645\u0646", "\u0639\u0644\u0649", "\u0625\u0644\u0649", "\u0647\u0630\u0627", "\u0623\u0646", "\u0627\u0644\u062a\u064a", "\u0627\u0644\u062a\u0649" }),
            ["hi"] = ("Hindi", new[] { "\u0939\u0948", "\u0915\u0947", "\u092e\u0947\u0902", "\u0915\u093e", "\u0915\u0940", "\u0915\u094b", "\u0914\u0930", "\u0938\u0947", "\u0939\u0948\u0902", "\u092a\u0930" }),
        };

        // ── Domain keywords ─────────────────────────────────────────

        private static readonly Dictionary<PromptDomain, string[]> DomainKw = new()
        {
            [PromptDomain.Technology] = new[] {
                "code", "function", "api", "database", "sql", "algorithm", "debug", "compile",
                "deploy", "git", "docker", "kubernetes", "python", "javascript", "typescript",
                "java", "csharp", "c#", "rust", "golang", "react", "angular", "vue",
                "html", "css", "server", "client", "frontend", "backend", "microservice",
                "rest", "graphql", "cicd", "devops", "cloud", "aws", "azure", "linux",
                "refactor", "bug", "error", "exception", "class", "interface", "method"
            },
            [PromptDomain.Medical] = new[] {
                "patient", "diagnosis", "symptom", "treatment", "medication", "dosage",
                "clinical", "pathology", "surgery", "therapy", "prescription", "vaccine",
                "anatomy", "disease", "chronic", "acute", "prognosis", "epidemiology",
                "radiology", "oncology", "cardiology", "neurology", "pediatric"
            },
            [PromptDomain.Legal] = new[] {
                "contract", "clause", "statute", "jurisdiction", "plaintiff", "defendant",
                "litigation", "arbitration", "compliance", "regulation", "tort", "liability",
                "precedent", "court", "judge", "attorney", "counsel", "intellectual property",
                "patent", "trademark", "copyright", "indemnify", "fiduciary"
            },
            [PromptDomain.Finance] = new[] {
                "revenue", "profit", "investment", "portfolio", "stock", "bond", "dividend",
                "interest rate", "inflation", "gdp", "balance sheet", "cash flow", "equity",
                "hedge", "derivative", "ipo", "valuation", "roi", "ebitda", "amortization",
                "depreciation", "fiscal", "monetary", "compound interest"
            },
            [PromptDomain.Academic] = new[] {
                "thesis", "dissertation", "peer review", "citation", "bibliography",
                "hypothesis", "methodology", "literature review", "abstract", "journal",
                "conference paper", "empirical", "qualitative", "quantitative", "curriculum",
                "syllabus", "lecture", "semester", "academic", "research paper"
            },
            [PromptDomain.Marketing] = new[] {
                "campaign", "brand", "audience", "conversion", "engagement", "seo",
                "content marketing", "social media", "influencer", "funnel", "cta",
                "landing page", "a/b test", "click-through", "impression", "reach",
                "copywriting", "tagline", "slogan", "demographics", "segmentation"
            },
            [PromptDomain.Creative] = new[] {
                "story", "poem", "novel", "character", "plot", "dialogue", "narrative",
                "fiction", "creative writing", "screenplay", "lyric", "metaphor",
                "protagonist", "antagonist", "setting", "worldbuilding", "genre"
            },
            [PromptDomain.Science] = new[] {
                "experiment", "hypothesis", "molecule", "atom", "quantum", "gravity",
                "thermodynamics", "evolution", "genome", "cell", "photosynthesis",
                "chemical reaction", "physics", "biology", "chemistry", "ecology",
                "geology", "astronomy", "particle", "wavelength", "electromagnetic"
            },
            [PromptDomain.Business] = new[] {
                "strategy", "stakeholder", "kpi", "roadmap", "quarterly", "vendor",
                "procurement", "supply chain", "logistics", "operations", "hr",
                "onboarding", "retention", "churn", "meeting agenda", "proposal",
                "invoice", "milestone", "deliverable", "scope", "budget planning"
            },
        };

        // ── Capability keywords ──────────────────────────────────────

        private static readonly Dictionary<PromptCapability, string[]> CapKw = new()
        {
            [PromptCapability.CodeGeneration] = new[] {
                "code", "function", "class", "method", "implement", "program", "script",
                "debug", "compile", "refactor", "algorithm", "syntax", "api", "sdk",
                "library", "framework", "unittest", "test case"
            },
            [PromptCapability.MathReasoning] = new[] {
                "calculate", "equation", "formula", "derivative", "integral", "probability",
                "statistics", "algebra", "geometry", "matrix", "polynomial", "logarithm",
                "factorial", "permutation", "combination", "arithmetic", "solve"
            },
            [PromptCapability.VisionOrImage] = new[] {
                "image", "photo", "picture", "screenshot", "diagram", "chart", "graph",
                "visual", "draw", "illustrate", "render", "pixel", "resolution"
            },
            [PromptCapability.WebSearch] = new[] {
                "search", "latest", "current", "recent", "today", "news", "update",
                "real-time", "live", "trending", "as of"
            },
            [PromptCapability.DocumentProcessing] = new[] {
                "document", "pdf", "file", "upload", "attachment", "spreadsheet", "docx",
                "parse file", "read file", "extract from"
            },
            [PromptCapability.DataAnalysis] = new[] {
                "analyze data", "dataset", "correlat", "regression", "outlier", "trend",
                "visualization", "histogram", "scatter plot", "pivot", "aggregate",
                "mean", "median", "standard deviation", "percentile"
            },
            [PromptCapability.Translation] = new[] {
                "translate", "translation", "localize", "in english", "in spanish",
                "in french", "in german", "in chinese", "in japanese", "multilingual"
            },
            [PromptCapability.Summarization] = new[] {
                "summarize", "summary", "condense", "brief", "tldr", "key points",
                "main ideas", "overview", "recap", "digest"
            },
            [PromptCapability.StructuredOutput] = new[] {
                "json", "xml", "csv", "yaml", "table", "schema", "structured",
                "formatted output", "markdown table"
            },
            [PromptCapability.Reasoning] = new[] {
                "step by step", "think through", "reason", "chain of thought",
                "logical", "deduce", "infer", "conclude", "prove", "compare and contrast",
                "pros and cons", "evaluate", "critique"
            },
            [PromptCapability.ToolUse] = new[] {
                "call", "invoke", "tool", "function call", "api call", "execute",
                "run command", "plugin", "action"
            },
        };

        /// <summary>
        /// Extracts comprehensive metadata from a single prompt string.
        /// </summary>
        /// <param name="prompt">Raw prompt text. May be empty (but not null).</param>
        /// <returns>A populated <see cref="PromptMetadata"/> instance. Always non-null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompt"/> is null.</exception>
        public PromptMetadata Extract(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            var meta = new PromptMetadata();
            var text = prompt.Trim();
            if (text.Length == 0) return meta;

            meta.WordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            meta.EstimatedTokens = PromptGuard.EstimateTokens(text);
            meta.Language = DetectLanguage(text);
            meta.QuestionCount = CountMatches(QuestionRx, text);
            meta.InstructionCount = CountMatches(InstructionRx, text);
            meta.HasExamples = ExampleRx.IsMatch(text) || CodeBlockRx.IsMatch(text);
            meta.HasSystemDirectives = SystemDirectiveRx.IsMatch(text);
            meta.Entities = ExtractEntities(text);
            meta.Capabilities = DetectCapabilities(text);
            (meta.Domain, meta.DomainConfidence) = ClassifyDomain(text);
            meta.Tone = AnalyzeTone(text);
            meta.RoutingSuggestion = SuggestRouting(meta);
            meta.Tags = BuildTags(meta);
            return meta;
        }

        /// <summary>
        /// Extracts metadata from a batch of prompts. Equivalent to calling
        /// <see cref="Extract"/> on each element; preserves input order.
        /// </summary>
        /// <param name="prompts">Sequence of raw prompt strings.</param>
        /// <returns>A list of <see cref="PromptMetadata"/>, one per input prompt.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompts"/> is null.</exception>
        public List<PromptMetadata> ExtractBatch(IEnumerable<string> prompts)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));
            return prompts.Select(Extract).ToList();
        }

        // ── Internals ────────────────────────────────────────────────

        /// <summary>
        /// Detects the most likely language by counting marker-token hits.
        /// Falls back to English with a 0.5 confidence floor when no other
        /// language clearly wins.
        /// </summary>
        private static DetectedLanguage DetectLanguage(string text)
        {
            var padded = " " + text.ToLowerInvariant() + " ";
            string bestCode = "en", bestName = "English";
            int bestScore = 0;

            foreach (var (code, (name, markers)) in LangMarkers)
            {
                int hits = markers.Count(m => padded.Contains(m));
                if (hits > bestScore) { bestScore = hits; bestCode = code; bestName = name; }
            }

            var enMarkers = new[] { " the ", " is ", " are ", " was ", " have ", " has ", " will ", " would ", " can ", " this ", " that ", " with ", " from " };
            int enHits = enMarkers.Count(m => padded.Contains(m));

            if (bestScore > enHits && bestScore >= 3)
                return new DetectedLanguage { Code = bestCode, Name = bestName, Confidence = Math.Min(1.0, bestScore / 8.0) };

            return new DetectedLanguage { Code = "en", Name = "English", Confidence = enHits >= 3 ? Math.Min(1.0, enHits / 8.0) : 0.5 };
        }

        /// <summary>
        /// Runs each entity regex over the prompt text and collects matches.
        /// Also scans for hard-coded programming-language names.
        /// Results are returned sorted by <see cref="ExtractedEntity.StartIndex"/>.
        /// </summary>
        private static List<ExtractedEntity> ExtractEntities(string text)
        {
            var entities = new List<ExtractedEntity>();
            void Add(Regex rx, string type) { foreach (Match m in rx.Matches(text)) entities.Add(new ExtractedEntity { Text = m.Value, Type = type, StartIndex = m.Index }); }

            Add(EmailRx, "email");
            Add(UrlRx, "url");
            Add(FilePathRx, "file_path");
            Add(DateRx, "date");
            Add(NumberRx, "number");

            var codeLangs = new[] { "python", "javascript", "typescript", "java", "c#", "csharp",
                "rust", "golang", "go", "ruby", "swift", "kotlin", "dart", "php", "scala",
                "haskell", "ocaml", "elixir", "lua", "perl", "r", "matlab", "sql" };
            var lower = text.ToLowerInvariant();
            foreach (var lang in codeLangs)
            {
                int idx = lower.IndexOf(lang, StringComparison.Ordinal);
                if (idx >= 0) entities.Add(new ExtractedEntity { Text = lang, Type = "code_lang", StartIndex = idx });
            }

            return entities.OrderBy(e => e.StartIndex).ToList();
        }

        /// <summary>
        /// Detects required capabilities by counting keyword hits against
        /// per-capability dictionaries. <see cref="PromptCapability.Translation"/>
        /// and <see cref="PromptCapability.Summarization"/> fire on a single
        /// hit; all others require at least two. <see cref="PromptCapability.TextGeneration"/>
        /// is always included; <see cref="PromptCapability.StructuredOutput"/>
        /// is also added when the text contains JSON-like structure.
        /// </summary>
        private static List<PromptCapability> DetectCapabilities(string text)
        {
            var caps = new List<PromptCapability>();
            var lower = text.ToLowerInvariant();

            foreach (var (cap, kw) in CapKw)
            {
                int hits = kw.Count(k => lower.Contains(k));
                int threshold = cap == PromptCapability.Translation || cap == PromptCapability.Summarization ? 1 : 2;
                if (hits >= threshold) caps.Add(cap);
            }

            if (!caps.Contains(PromptCapability.TextGeneration))
                caps.Insert(0, PromptCapability.TextGeneration);

            if (!caps.Contains(PromptCapability.StructuredOutput) && JsonStructureRx.IsMatch(text))
                caps.Add(PromptCapability.StructuredOutput);

            return caps;
        }

        /// <summary>
        /// Classifies the prompt into a <see cref="PromptDomain"/> by counting
        /// keyword hits. Confidence is the top-domain hit count divided by
        /// 60% of total hits across all domains, clamped to <c>[0.0, 1.0]</c>.
        /// Returns <see cref="PromptDomain.General"/> with confidence 0.5 when
        /// no keyword matches at all.
        /// </summary>
        private static (PromptDomain, double) ClassifyDomain(string text)
        {
            var lower = text.ToLowerInvariant();
            var scores = new Dictionary<PromptDomain, int>();
            foreach (var (domain, kw) in DomainKw)
            {
                int hits = kw.Count(k => lower.Contains(k));
                if (hits > 0) scores[domain] = hits;
            }
            if (scores.Count == 0) return (PromptDomain.General, 0.5);

            var top = scores.MaxBy(s => s.Value)!;
            double conf = Math.Min(1.0, top.Value / Math.Max(1.0, scores.Values.Sum() * 0.6));
            return (top.Key, Math.Round(conf, 2));
        }

        /// <summary>
        /// Scores tone using three keyword buckets (formal, professional,
        /// casual) plus emoji and exclamation-mark heuristics. Returns the
        /// first formality bracket whose threshold is met, preferring the
        /// stronger signals (Formal &gt; Informal &gt; Casual &gt; Professional).
        /// </summary>
        private static PromptFormality AnalyzeTone(string text)
        {
            var lower = text.ToLowerInvariant();

            var formalWords = new[] { "hereby", "pursuant", "whereas", "furthermore", "consequently",
                "aforementioned", "notwithstanding", "henceforth", "therein", "shall", "kindly",
                "respectfully", "regarding", "enclosed", "in accordance with", "please be advised" };
            int formal = formalWords.Count(w => lower.Contains(w));

            var proWords = new[] { "please", "would you", "could you", "i would like", "appreciate",
                "thank you", "best regards", "sincerely", "dear" };
            int pro = proWords.Count(w => lower.Contains(w));

            var casualWords = new[] { " hey ", " yo ", "gonna", "wanna", "gotta", " lol ", " btw ", " tbh ",
                " ngl ", " imo ", "bruh", " dude ", " nah ", " yeah ", " cool ", " awesome ", " sup ", " omg ", "lmao", "haha" };
            var paddedLower = " " + lower + " ";
            int casual = casualWords.Count(w => paddedLower.Contains(w));

            if (text.Any(c => (c >= 0x1F600 && c <= 0x1F64F) || (c >= 0x1F300 && c <= 0x1F5FF))) casual += 2;
            if (text.Count(c => c == '!') > 2) casual++;

            if (formal >= 3) return PromptFormality.Formal;
            if (formal >= 1 && casual == 0) return PromptFormality.Professional;
            if (casual >= 3) return PromptFormality.Informal;
            if (casual >= 1) return PromptFormality.Casual;
            if (pro >= 2) return PromptFormality.Professional;
            return PromptFormality.Neutral;
        }

        /// <summary>
        /// Heuristic routing tier selector. Returns one of <c>"specialist"</c>,
        /// <c>"premium"</c>, <c>"fast"</c>, or <c>"standard"</c> based on
        /// capability breadth, token budget, and domain confidence.
        /// </summary>
        private static string SuggestRouting(PromptMetadata m)
        {
            if (m.Capabilities.Count >= 4 ||
                (m.Domain != PromptDomain.General && m.DomainConfidence > 0.7 &&
                 m.Capabilities.Any(c => c == PromptCapability.Reasoning || c == PromptCapability.CodeGeneration)))
                return "specialist";

            if (m.Capabilities.Count >= 3 || m.EstimatedTokens > 2000 ||
                (m.HasExamples && m.InstructionCount > 3))
                return "premium";

            if (m.WordCount < 20 && m.Capabilities.Count <= 1 && m.QuestionCount <= 1)
                return "fast";

            return "standard";
        }

        /// <summary>
        /// Builds derived key/value tags (word-count bucket, pattern,
        /// interaction style, code language list) from a fully populated
        /// <see cref="PromptMetadata"/>.
        /// </summary>
        private static Dictionary<string, string> BuildTags(PromptMetadata m)
        {
            var tags = new Dictionary<string, string>
            {
                ["word_count_bucket"] = m.WordCount switch { < 10 => "micro", < 50 => "short", < 200 => "medium", < 500 => "long", _ => "very_long" }
            };

            if (m.HasExamples) tags["pattern"] = "few_shot";
            if (m.HasSystemDirectives) tags["has_system"] = "true";
            if (m.QuestionCount > 0) tags["interaction"] = "question";
            if (m.InstructionCount > 0) tags["interaction"] = "instruction";

            var codeLangs = string.Join(",", m.Entities.Where(e => e.Type == "code_lang").Select(e => e.Text).Distinct());
            if (codeLangs.Length > 0) tags["code_languages"] = codeLangs;

            return tags;
        }

        /// <summary>
        /// Safe wrapper around <see cref="Regex.Matches(string)"/> that
        /// returns 0 on <see cref="RegexMatchTimeoutException"/> rather than
        /// propagating the exception. Used to keep extraction total-functional.
        /// </summary>
        private static int CountMatches(Regex rx, string text)
        {
            try { return rx.Matches(text).Count; }
            catch (RegexMatchTimeoutException) { return 0; }
        }
    }
}
