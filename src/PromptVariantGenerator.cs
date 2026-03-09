namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ── Enums ────────────────────────────────────────────────

    /// <summary>
    /// Tone presets for prompt rephrasing.
    /// </summary>
    public enum PromptTone
    {
        /// <summary>Professional, precise language.</summary>
        Formal,
        /// <summary>Relaxed, conversational language.</summary>
        Casual,
        /// <summary>Domain-expert jargon and concise style.</summary>
        Expert,
        /// <summary>Warm, encouraging, approachable.</summary>
        Friendly
    }

    /// <summary>
    /// Instruction framing style.
    /// </summary>
    public enum InstructionStyle
    {
        /// <summary>"Do X." / "Generate X."</summary>
        Imperative,
        /// <summary>"Can you X?" / "Would you X?"</summary>
        Question,
        /// <summary>"The task is to X." / "Your goal is X."</summary>
        Descriptive
    }

    /// <summary>
    /// Reasoning-strategy prefixes injected before the main prompt.
    /// </summary>
    public enum ReasoningStrategy
    {
        /// <summary>No prefix.</summary>
        None,
        /// <summary>"Let's think step by step."</summary>
        ChainOfThought,
        /// <summary>"First, list what you know. Then reason through each point."</summary>
        Structured,
        /// <summary>"Consider multiple approaches before answering."</summary>
        Exploratory,
        /// <summary>"Take a deep breath and work through this carefully."</summary>
        Calm
    }

    /// <summary>
    /// Controls the verbosity of generated variants.
    /// </summary>
    public enum VerbosityLevel
    {
        /// <summary>Strip filler words and reduce to essentials.</summary>
        Concise,
        /// <summary>Keep original length.</summary>
        Normal,
        /// <summary>Expand with clarifications and elaboration cues.</summary>
        Detailed
    }

    // ── Result types ─────────────────────────────────────────

    /// <summary>
    /// A single generated prompt variant with metadata.
    /// </summary>
    public class GeneratedVariant
    {
        /// <summary>Unique label for this variant (e.g., "formal-cot-concise").</summary>
        public string Label { get; }

        /// <summary>The transformed prompt text.</summary>
        public string Text { get; }

        /// <summary>Estimated token count (chars / 4 heuristic).</summary>
        public int EstimatedTokens { get; }

        /// <summary>List of transforms applied.</summary>
        public IReadOnlyList<string> Transforms { get; }

        /// <summary>Character-length delta from the original prompt.</summary>
        public int LengthDelta { get; }

        /// <summary>
        /// Creates a new generated variant.
        /// </summary>
        public GeneratedVariant(string label, string text,
            int estimatedTokens, IReadOnlyList<string> transforms, int lengthDelta)
        {
            Label = label;
            Text = text;
            EstimatedTokens = estimatedTokens;
            Transforms = transforms;
            LengthDelta = lengthDelta;
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"[{Label}] {EstimatedTokens} tokens ({(LengthDelta >= 0 ? "+" : "")}{LengthDelta} chars) — {string.Join(", ", Transforms)}";
    }

    /// <summary>
    /// Result of a variant generation run.
    /// </summary>
    public class VariantGenerationResult
    {
        /// <summary>The original prompt text.</summary>
        public string Original { get; }

        /// <summary>Estimated tokens in the original prompt.</summary>
        public int OriginalTokens { get; }

        /// <summary>All generated variants.</summary>
        public IReadOnlyList<GeneratedVariant> Variants { get; }

        /// <summary>Shortest variant by estimated tokens.</summary>
        public GeneratedVariant? Shortest { get; }

        /// <summary>Longest variant by estimated tokens.</summary>
        public GeneratedVariant? Longest { get; }

        /// <summary>
        /// Creates a new variant generation result.
        /// </summary>
        public VariantGenerationResult(string original, int originalTokens,
            IReadOnlyList<GeneratedVariant> variants)
        {
            Original = original;
            OriginalTokens = originalTokens;
            Variants = variants;
            Shortest = variants.OrderBy(v => v.EstimatedTokens).FirstOrDefault();
            Longest = variants.OrderByDescending(v => v.EstimatedTokens).FirstOrDefault();
        }

        /// <summary>
        /// Returns a human-readable summary of all variants.
        /// </summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Variant Generation Summary ===");
            sb.AppendLine($"Original: {OriginalTokens} estimated tokens");
            sb.AppendLine($"Variants generated: {Variants.Count}");
            if (Shortest != null)
                sb.AppendLine($"Shortest: [{Shortest.Label}] {Shortest.EstimatedTokens} tokens");
            if (Longest != null)
                sb.AppendLine($"Longest:  [{Longest.Label}] {Longest.EstimatedTokens} tokens");
            sb.AppendLine();
            foreach (var v in Variants)
            {
                sb.AppendLine(v.ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the result to JSON.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var obj = new
            {
                originalTokens = OriginalTokens,
                variantCount = Variants.Count,
                variants = Variants.Select(v => new
                {
                    label = v.Label,
                    text = v.Text,
                    estimatedTokens = v.EstimatedTokens,
                    transforms = v.Transforms,
                    lengthDelta = v.LengthDelta
                }).ToArray()
            };
            return JsonSerializer.Serialize(obj,
                indented ? SerializationGuards.WriteIndented : new JsonSerializerOptions());
        }
    }

    // ── Configuration ────────────────────────────────────────

    /// <summary>
    /// Configuration for which variant axes to generate.
    /// </summary>
    public class VariantConfig
    {
        /// <summary>Tone shifts to apply. Empty = skip tone variants.</summary>
        public List<PromptTone> Tones { get; set; } = new();

        /// <summary>Instruction styles to apply. Empty = skip instruction variants.</summary>
        public List<InstructionStyle> Styles { get; set; } = new();

        /// <summary>Reasoning strategies to prepend. Empty = skip.</summary>
        public List<ReasoningStrategy> Strategies { get; set; } = new();

        /// <summary>Verbosity levels. Empty = skip.</summary>
        public List<VerbosityLevel> Verbosities { get; set; } = new();

        /// <summary>Custom prefix strings to prepend to the prompt.</summary>
        public List<string> CustomPrefixes { get; set; } = new();

        /// <summary>Custom suffix strings to append to the prompt.</summary>
        public List<string> CustomSuffixes { get; set; } = new();

        /// <summary>
        /// If true, generate the cartesian product of all axes.
        /// If false (default), apply each axis independently.
        /// </summary>
        public bool Combinatorial { get; set; } = false;

        /// <summary>Maximum number of variants to generate (safety limit).</summary>
        public int MaxVariants { get; set; } = 200;

        /// <summary>
        /// Returns a config that generates one variant per axis value.
        /// </summary>
        public static VariantConfig AllAxes() => new()
        {
            Tones = new List<PromptTone>
                { PromptTone.Formal, PromptTone.Casual, PromptTone.Expert, PromptTone.Friendly },
            Styles = new List<InstructionStyle>
                { InstructionStyle.Imperative, InstructionStyle.Question, InstructionStyle.Descriptive },
            Strategies = new List<ReasoningStrategy>
                { ReasoningStrategy.ChainOfThought, ReasoningStrategy.Structured,
                  ReasoningStrategy.Exploratory, ReasoningStrategy.Calm },
            Verbosities = new List<VerbosityLevel>
                { VerbosityLevel.Concise, VerbosityLevel.Detailed }
        };

        /// <summary>
        /// Returns a minimal config with just tone and verbosity.
        /// </summary>
        public static VariantConfig Quick() => new()
        {
            Tones = new List<PromptTone> { PromptTone.Formal, PromptTone.Casual },
            Verbosities = new List<VerbosityLevel>
                { VerbosityLevel.Concise, VerbosityLevel.Detailed }
        };
    }

    // ── Generator ────────────────────────────────────────────

    /// <summary>
    /// Generates systematic prompt variants by applying text transformations
    /// along configurable axes: tone, instruction style, reasoning strategy,
    /// verbosity, and custom prefix/suffix injection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All transformations are pure text operations — no API calls required.
    /// Pairs well with <see cref="PromptABTester"/> for evaluating which
    /// variant performs best with a given model.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var gen = new PromptVariantGenerator();
    /// var result = gen.Generate(
    ///     "You are a helpful assistant. Summarize the article.",
    ///     VariantConfig.Quick()
    /// );
    /// foreach (var v in result.Variants)
    ///     Console.WriteLine($"{v.Label}: {v.Text}");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptVariantGenerator
    {
        /// <summary>Maximum prompt length accepted (500 KB).</summary>
        public const int MaxPromptLength = 500_000;

        // ── Tone dictionaries ────────────────────────────────

        private static readonly Dictionary<string, string> FormalReplacements = new(StringComparer.OrdinalIgnoreCase)
        {
            ["you are"] = "You shall serve as",
            ["help the user"] = "assist the user",
            ["make sure"] = "ensure",
            ["a lot of"] = "numerous",
            ["get"] = "obtain",
            ["fix"] = "rectify",
            ["find out"] = "determine",
            ["look at"] = "examine",
            ["come up with"] = "devise",
            ["put together"] = "assemble",
            ["figure out"] = "ascertain",
            ["kind of"] = "somewhat",
            ["sort of"] = "to some extent",
            ["really"] = "significantly",
            ["pretty"] = "fairly",
            ["stuff"] = "material",
            ["things"] = "items",
            ["big"] = "substantial",
            ["good"] = "satisfactory",
            ["bad"] = "unsatisfactory",
            ["show"] = "demonstrate",
            ["tell"] = "inform",
        };

        private static readonly Dictionary<string, string> CasualReplacements = new(StringComparer.OrdinalIgnoreCase)
        {
            ["You shall serve as"] = "You're",
            ["shall"] = "should",
            ["ensure"] = "make sure",
            ["assist"] = "help",
            ["obtain"] = "get",
            ["rectify"] = "fix",
            ["determine"] = "figure out",
            ["examine"] = "look at",
            ["utilize"] = "use",
            ["implement"] = "set up",
            ["subsequently"] = "then",
            ["therefore"] = "so",
            ["however"] = "but",
            ["additionally"] = "also",
            ["furthermore"] = "plus",
            ["demonstrate"] = "show",
            ["provide"] = "give",
            ["regarding"] = "about",
            ["accomplish"] = "do",
            ["commence"] = "start",
            ["terminate"] = "end",
            ["substantial"] = "big",
            ["sufficient"] = "enough",
        };

        private static readonly Dictionary<string, string> ExpertReplacements = new(StringComparer.OrdinalIgnoreCase)
        {
            ["summarize"] = "synthesize",
            ["list"] = "enumerate",
            ["explain"] = "elucidate",
            ["check"] = "validate",
            ["improve"] = "optimize",
            ["create"] = "architect",
            ["review"] = "audit",
            ["look at"] = "analyze",
            ["think about"] = "evaluate",
            ["write"] = "compose",
            ["good"] = "optimal",
            ["bad"] = "suboptimal",
            ["problem"] = "constraint",
            ["answer"] = "solution",
            ["simple"] = "trivial",
            ["hard"] = "non-trivial",
            ["error"] = "exception",
        };

        private static readonly Dictionary<string, string> FriendlyReplacements = new(StringComparer.OrdinalIgnoreCase)
        {
            ["You are"] = "Hey, you're",
            ["must"] = "please",
            ["shall"] = "could you",
            ["ensure"] = "just make sure",
            ["do not"] = "try not to",
            ["required"] = "needed",
            ["error"] = "hiccup",
            ["failure"] = "issue",
            ["immediately"] = "when you get a chance",
            ["generate"] = "whip up",
            ["analyze"] = "take a look at",
        };

        // ── Reasoning prefixes ───────────────────────────────

        private static readonly Dictionary<ReasoningStrategy, string> StrategyPrefixes = new()
        {
            [ReasoningStrategy.None] = "",
            [ReasoningStrategy.ChainOfThought] = "Let's think step by step.\n\n",
            [ReasoningStrategy.Structured] = "First, list what you know. Then reason through each point.\n\n",
            [ReasoningStrategy.Exploratory] = "Consider multiple approaches before answering.\n\n",
            [ReasoningStrategy.Calm] = "Take a deep breath and work through this carefully.\n\n",
        };

        // ── Filler words for concise mode ────────────────────

        private static readonly string[] FillerPatterns = new[]
        {
            @"\bbasically\b",
            @"\bjust\b",
            @"\bactually\b",
            @"\breally\b",
            @"\bvery\b",
            @"\bquite\b",
            @"\bsimply\b",
            @"\bliterally\b",
            @"\bobviously\b",
            @"\bclearly\b",
            @"\bperhaps\b",
            @"\bpossibly\b",
            @"\bgenerally speaking\b",
            @"\bin general\b",
            @"\bas a matter of fact\b",
            @"\bit should be noted that\b",
            @"\bit is important to note that\b",
            @"\bplease note that\b",
            @"\bkeep in mind that\b",
        };

        private static readonly Regex[] CompiledFillerPatterns =
            FillerPatterns.Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500))).ToArray();

        // ── Elaboration suffixes for detailed mode ───────────

        private static readonly string[] ElaborationSuffixes = new[]
        {
            "\n\nBe thorough in your response. Include examples where helpful.",
            "\n\nProvide detailed explanations. If there are edge cases, address them.",
            "\n\nElaborate on your reasoning. Show your work.",
        };

        private readonly Random _random;

        // ── Pre-compiled regex patterns ─────────────────────

        private static readonly Regex QuestionPattern = new(
            @"^(Can|Could|Would|Will)\s+you\s+(.+?)(\?)?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex DescriptiveTaskPattern = new(
            @"^(Your\s+task\s+is\s+to|The\s+(?:task|goal)\s+is\s+to)\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex MultiSpacePattern = new(
            @"  +", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex ExcessiveNewlinePattern = new(
            @"\n{3,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Creates a new variant generator.
        /// </summary>
        public PromptVariantGenerator()
        {
            _random = new Random();
        }

        /// <summary>
        /// Creates a new variant generator with a fixed seed for reproducibility.
        /// </summary>
        /// <param name="seed">Random seed.</param>
        public PromptVariantGenerator(int seed)
        {
            _random = new Random(seed);
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// Generates prompt variants according to the given configuration.
        /// </summary>
        /// <param name="prompt">The base prompt to transform.</param>
        /// <param name="config">Configuration specifying which axes to vary.</param>
        /// <returns>A <see cref="VariantGenerationResult"/> containing all generated variants.</returns>
        /// <exception cref="ArgumentException">If the prompt is null, empty, or too long.</exception>
        /// <exception cref="ArgumentNullException">If config is null.</exception>
        public VariantGenerationResult Generate(string prompt, VariantConfig config)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            if (prompt.Length > MaxPromptLength)
                throw new ArgumentException(
                    $"Prompt exceeds maximum length of {MaxPromptLength} characters.", nameof(prompt));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            int originalTokens = PromptGuard.EstimateTokens(prompt);
            var variants = new List<GeneratedVariant>();

            if (config.Combinatorial)
            {
                GenerateCombinatorialVariants(prompt, config, variants);
            }
            else
            {
                GenerateIndependentVariants(prompt, config, variants);
            }

            // Enforce max variants limit
            if (variants.Count > config.MaxVariants)
            {
                variants = variants.Take(config.MaxVariants).ToList();
            }

            return new VariantGenerationResult(prompt, originalTokens, variants.AsReadOnly());
        }

        /// <summary>
        /// Applies a single tone transformation to a prompt.
        /// </summary>
        /// <param name="prompt">The prompt to transform.</param>
        /// <param name="tone">The target tone.</param>
        /// <returns>The transformed prompt text.</returns>
        public string ApplyTone(string prompt, PromptTone tone)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            return tone switch
            {
                PromptTone.Formal => ApplyReplacements(prompt, FormalReplacements),
                PromptTone.Casual => ApplyReplacements(prompt, CasualReplacements),
                PromptTone.Expert => ApplyReplacements(prompt, ExpertReplacements),
                PromptTone.Friendly => ApplyReplacements(prompt, FriendlyReplacements),
                _ => prompt
            };
        }

        /// <summary>
        /// Applies a verbosity transformation to a prompt.
        /// </summary>
        /// <param name="prompt">The prompt to transform.</param>
        /// <param name="level">The target verbosity level.</param>
        /// <returns>The transformed prompt text.</returns>
        public string ApplyVerbosity(string prompt, VerbosityLevel level)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            return level switch
            {
                VerbosityLevel.Concise => MakeConcise(prompt),
                VerbosityLevel.Normal => prompt,
                VerbosityLevel.Detailed => MakeDetailed(prompt),
                _ => prompt
            };
        }

        /// <summary>
        /// Rephrases the first sentence using the given instruction style.
        /// </summary>
        /// <param name="prompt">The prompt to transform.</param>
        /// <param name="style">The target instruction style.</param>
        /// <returns>The transformed prompt text.</returns>
        public string ApplyInstructionStyle(string prompt, InstructionStyle style)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            return style switch
            {
                InstructionStyle.Imperative => ToImperative(prompt),
                InstructionStyle.Question => ToQuestion(prompt),
                InstructionStyle.Descriptive => ToDescriptive(prompt),
                _ => prompt
            };
        }

        /// <summary>
        /// Prepends a reasoning strategy prefix to the prompt.
        /// </summary>
        /// <param name="prompt">The prompt to transform.</param>
        /// <param name="strategy">The reasoning strategy.</param>
        /// <returns>The transformed prompt text.</returns>
        public string ApplyStrategy(string prompt, ReasoningStrategy strategy)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            if (strategy == ReasoningStrategy.None)
                return prompt;

            string prefix = StrategyPrefixes[strategy];
            return prefix + prompt;
        }

        // ── Independent generation ───────────────────────────

        private void GenerateIndependentVariants(string prompt, VariantConfig config,
            List<GeneratedVariant> variants)
        {
            foreach (var tone in config.Tones)
            {
                string text = ApplyTone(prompt, tone);
                AddVariant(variants, prompt, text,
                    tone.ToString().ToLower(CultureInfo.InvariantCulture),
                    new[] { $"tone:{tone}" });
            }

            foreach (var style in config.Styles)
            {
                string text = ApplyInstructionStyle(prompt, style);
                AddVariant(variants, prompt, text,
                    style.ToString().ToLower(CultureInfo.InvariantCulture),
                    new[] { $"style:{style}" });
            }

            foreach (var strategy in config.Strategies)
            {
                if (strategy == ReasoningStrategy.None) continue;
                string text = ApplyStrategy(prompt, strategy);
                AddVariant(variants, prompt, text,
                    strategy.ToString().ToLower(CultureInfo.InvariantCulture),
                    new[] { $"strategy:{strategy}" });
            }

            foreach (var verbosity in config.Verbosities)
            {
                if (verbosity == VerbosityLevel.Normal) continue;
                string text = ApplyVerbosity(prompt, verbosity);
                AddVariant(variants, prompt, text,
                    verbosity.ToString().ToLower(CultureInfo.InvariantCulture),
                    new[] { $"verbosity:{verbosity}" });
            }

            for (int i = 0; i < config.CustomPrefixes.Count; i++)
            {
                string prefix = config.CustomPrefixes[i];
                if (string.IsNullOrEmpty(prefix)) continue;
                string text = prefix + "\n\n" + prompt;
                AddVariant(variants, prompt, text,
                    $"prefix-{i + 1}",
                    new[] { $"prefix:\"{Truncate(prefix, 40)}\"" });
            }

            for (int i = 0; i < config.CustomSuffixes.Count; i++)
            {
                string suffix = config.CustomSuffixes[i];
                if (string.IsNullOrEmpty(suffix)) continue;
                string text = prompt + "\n\n" + suffix;
                AddVariant(variants, prompt, text,
                    $"suffix-{i + 1}",
                    new[] { $"suffix:\"{Truncate(suffix, 40)}\"" });
            }
        }

        // ── Combinatorial generation ─────────────────────────

        private void GenerateCombinatorialVariants(string prompt, VariantConfig config,
            List<GeneratedVariant> variants)
        {
            var tones = config.Tones.Count > 0
                ? config.Tones.Cast<PromptTone?>().ToList()
                : new List<PromptTone?> { null };
            var styles = config.Styles.Count > 0
                ? config.Styles.Cast<InstructionStyle?>().ToList()
                : new List<InstructionStyle?> { null };
            var strategies = config.Strategies.Count > 0
                ? config.Strategies.Cast<ReasoningStrategy?>().ToList()
                : new List<ReasoningStrategy?> { null };
            var verbosities = config.Verbosities.Count > 0
                ? config.Verbosities.Cast<VerbosityLevel?>().ToList()
                : new List<VerbosityLevel?> { null };

            int count = 0;
            foreach (var tone in tones)
            {
                foreach (var style in styles)
                {
                    foreach (var strategy in strategies)
                    {
                        foreach (var verbosity in verbosities)
                        {
                            if (count >= config.MaxVariants) return;

                            if (tone == null && style == null &&
                                strategy == null && verbosity == null)
                                continue;

                            string text = prompt;
                            var transforms = new List<string>();
                            var labelParts = new List<string>();

                            if (tone.HasValue)
                            {
                                text = ApplyTone(text, tone.Value);
                                transforms.Add($"tone:{tone.Value}");
                                labelParts.Add(tone.Value.ToString().ToLower(CultureInfo.InvariantCulture));
                            }

                            if (style.HasValue)
                            {
                                text = ApplyInstructionStyle(text, style.Value);
                                transforms.Add($"style:{style.Value}");
                                labelParts.Add(style.Value.ToString().ToLower(CultureInfo.InvariantCulture));
                            }

                            if (strategy.HasValue && strategy.Value != ReasoningStrategy.None)
                            {
                                text = ApplyStrategy(text, strategy.Value);
                                transforms.Add($"strategy:{strategy.Value}");
                                labelParts.Add(strategy.Value.ToString().ToLower(CultureInfo.InvariantCulture));
                            }

                            if (verbosity.HasValue && verbosity.Value != VerbosityLevel.Normal)
                            {
                                text = ApplyVerbosity(text, verbosity.Value);
                                transforms.Add($"verbosity:{verbosity.Value}");
                                labelParts.Add(verbosity.Value.ToString().ToLower(CultureInfo.InvariantCulture));
                            }

                            if (transforms.Count == 0) continue;

                            string label = string.Join("-", labelParts);
                            AddVariant(variants, prompt, text, label, transforms.ToArray());
                            count++;
                        }
                    }
                }
            }
        }

        // ── Transform helpers ────────────────────────────────

        private static string ApplyReplacements(string text,
            Dictionary<string, string> replacements)
        {
            foreach (var kvp in replacements)
            {
                string pattern = @"(?<=\b|^)" + Regex.Escape(kvp.Key) + @"(?=\b|$)";
                text = Regex.Replace(text, pattern, kvp.Value, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
            }
            return text;
        }

        private string MakeConcise(string prompt)
        {
            string result = prompt;
            foreach (var pattern in CompiledFillerPatterns)
            {
                result = pattern.Replace(result, "");
            }
            result = MultiSpacePattern.Replace(result, " ");
            result = ExcessiveNewlinePattern.Replace(result, "\n\n");
            return result.Trim();
        }

        private string MakeDetailed(string prompt)
        {
            int idx = Math.Abs(prompt.GetHashCode()) % ElaborationSuffixes.Length;
            return prompt + ElaborationSuffixes[idx];
        }

        private static string ToImperative(string prompt)
        {
            string trimmed = prompt.TrimStart();
            string[] imperativeStarters = { "Do", "Generate", "Create", "Write", "List",
                "Summarize", "Explain", "Analyze", "Review", "Build", "Find",
                "Make", "Check", "Fix", "Provide", "Show", "Give", "Return" };
            foreach (string verb in imperativeStarters)
            {
                if (trimmed.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                    return prompt;
            }

            var questionMatch = QuestionPattern.Match(trimmed);
            if (questionMatch.Success)
            {
                string task = questionMatch.Groups[2].Value.TrimEnd('?', '.', ' ');
                return char.ToUpper(task[0], CultureInfo.InvariantCulture) + task.Substring(1) + ".";
            }

            var descriptiveMatch = DescriptiveTaskPattern.Match(trimmed);
            if (descriptiveMatch.Success)
            {
                string task = descriptiveMatch.Groups[2].Value.TrimEnd('.', ' ');
                return char.ToUpper(task[0], CultureInfo.InvariantCulture) + task.Substring(1) + ".";
            }

            return prompt;
        }

        private static string ToQuestion(string prompt)
        {
            string trimmed = prompt.TrimStart();

            if (trimmed.TrimEnd().EndsWith("?"))
                return prompt;

            string[] imperativeVerbs = { "Summarize", "Explain", "Analyze", "Review",
                "Generate", "Create", "Write", "List", "Find", "Build", "Check",
                "Fix", "Provide", "Show", "Give", "Return", "Make", "Do" };

            foreach (string verb in imperativeVerbs)
            {
                if (trimmed.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = trimmed.Substring(verb.Length).TrimEnd('.', ' ');
                    string lowerVerb = verb.ToLower(CultureInfo.InvariantCulture);
                    return $"Can you {lowerVerb}{rest}?";
                }
            }

            var descriptiveMatch = DescriptiveTaskPattern.Match(trimmed);
            if (descriptiveMatch.Success)
            {
                string task = descriptiveMatch.Groups[2].Value.TrimEnd('.', ' ');
                return $"Can you {task}?";
            }

            if (trimmed.Length < 200)
            {
                string task = trimmed.TrimEnd('.', ' ').ToLower(CultureInfo.InvariantCulture);
                return $"Can you {task}?";
            }

            return prompt;
        }

        private static string ToDescriptive(string prompt)
        {
            string trimmed = prompt.TrimStart();

            if (trimmed.StartsWith("Your task", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("The task", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("The goal", StringComparison.OrdinalIgnoreCase))
                return prompt;

            var questionMatch = QuestionPattern.Match(trimmed);
            if (questionMatch.Success)
            {
                string task = questionMatch.Groups[2].Value.TrimEnd('?', '.', ' ');
                return $"Your task is to {task}.";
            }

            string[] imperativeVerbs = { "Summarize", "Explain", "Analyze", "Review",
                "Generate", "Create", "Write", "List", "Find", "Build", "Check",
                "Fix", "Provide", "Show", "Give", "Return", "Make", "Do" };

            foreach (string verb in imperativeVerbs)
            {
                if (trimmed.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = trimmed.TrimEnd('.', ' ');
                    string lowerRest = char.ToLower(rest[0], CultureInfo.InvariantCulture)
                                       + rest.Substring(1);
                    return $"Your task is to {lowerRest}.";
                }
            }

            return prompt;
        }

        // ── Utility ──────────────────────────────────────────

        private static void AddVariant(List<GeneratedVariant> variants,
            string original, string text, string label, string[] transforms)
        {
            if (text == original) return;

            int tokens = PromptGuard.EstimateTokens(text);
            int delta = text.Length - original.Length;
            variants.Add(new GeneratedVariant(label, text, tokens,
                Array.AsReadOnly(transforms), delta));
        }

        private static string Truncate(string text, int maxLen)
        {
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen - 3) + "...";
        }
    }
}
