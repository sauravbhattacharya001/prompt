namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    // ── Enums ────────────────────────────────────────────────────────────

    /// <summary>Strategy used to mutate prompt organisms.</summary>
    public enum MutationStrategy
    {
        /// <summary>Randomly reorder words within a sentence.</summary>
        WordShuffle,
        /// <summary>Replace common words with synonyms.</summary>
        SynonymReplace,
        /// <summary>Reorder sentences in the prompt.</summary>
        SentenceReorder,
        /// <summary>Shift formality level (add or remove formal markers).</summary>
        ToneShift,
        /// <summary>Remove filler words and redundancy.</summary>
        Compress,
        /// <summary>Add clarifying or emphatic phrases.</summary>
        Expand,
        /// <summary>Rephrase imperative sentences with different verb forms.</summary>
        InstructionRephrase
    }

    /// <summary>Method used to combine two parent organisms.</summary>
    public enum CrossoverMethod
    {
        /// <summary>Split at midpoint sentence boundary.</summary>
        SinglePoint,
        /// <summary>Randomly pick sentences from each parent.</summary>
        Uniform,
        /// <summary>Interleave sentences alternating parents.</summary>
        SemanticBlend
    }

    /// <summary>Method for selecting parents from the population.</summary>
    public enum SelectionMethod
    {
        /// <summary>Tournament selection among random candidates.</summary>
        Tournament,
        /// <summary>Probability proportional to fitness.</summary>
        RouletteWheel,
        /// <summary>Rank-based probability (linear ranking).</summary>
        RankBased,
        /// <summary>Top-N organisms pass through unchanged.</summary>
        Elitism
    }

    // ── Data classes ─────────────────────────────────────────────────────

    /// <summary>An individual prompt in the evolving population.</summary>
    public class PromptOrganism
    {
        /// <summary>Unique identifier for this organism.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>The prompt text carried by this organism.</summary>
        public string Text { get; set; } = "";

        /// <summary>Generation number when this organism was created.</summary>
        public int Generation { get; set; }

        /// <summary>Fitness score in the range 0.0 – 1.0.</summary>
        public double Fitness { get; set; }

        /// <summary>Id of the first parent, if any.</summary>
        public string? ParentAId { get; set; }

        /// <summary>Id of the second parent, if any.</summary>
        public string? ParentBId { get; set; }

        /// <summary>Names of mutations applied to this organism.</summary>
        public List<string> MutationsApplied { get; set; } = new();

        /// <summary>Timestamp when this organism was created.</summary>
        public DateTime Born { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Configuration for the evolution engine.</summary>
    public class EvolutionConfig
    {
        /// <summary>Number of organisms per generation (minimum 4).</summary>
        public int PopulationSize { get; set; } = 20;

        /// <summary>Maximum number of generations to run.</summary>
        public int MaxGenerations { get; set; } = 50;

        /// <summary>Base probability of mutation per offspring (0.0 – 1.0).</summary>
        public double MutationRate { get; set; } = 0.15;

        /// <summary>Probability of crossover per offspring pair (0.0 – 1.0).</summary>
        public double CrossoverRate { get; set; } = 0.7;

        /// <summary>Number of top organisms that pass unchanged each generation.</summary>
        public int EliteCount { get; set; } = 2;

        /// <summary>Parent selection method.</summary>
        public SelectionMethod Selection { get; set; } = SelectionMethod.Tournament;

        /// <summary>Fitness score at which evolution stops early.</summary>
        public double FitnessTargetThreshold { get; set; } = 0.95;

        /// <summary>Number of candidates in tournament selection.</summary>
        public int TournamentSize { get; set; } = 3;

        /// <summary>When true, mutation rate adapts to population diversity.</summary>
        public bool AdaptiveMutationRate { get; set; } = true;

        /// <summary>Validate configuration and throw if invalid.</summary>
        internal void Validate()
        {
            if (PopulationSize < 4) throw new ArgumentException("PopulationSize must be >= 4.");
            if (MaxGenerations < 1) throw new ArgumentException("MaxGenerations must be >= 1.");
            if (MutationRate < 0 || MutationRate > 1) throw new ArgumentException("MutationRate must be 0.0 – 1.0.");
            if (CrossoverRate < 0 || CrossoverRate > 1) throw new ArgumentException("CrossoverRate must be 0.0 – 1.0.");
            if (EliteCount < 0) throw new ArgumentException("EliteCount must be >= 0.");
            if (TournamentSize < 2) throw new ArgumentException("TournamentSize must be >= 2.");
        }
    }

    /// <summary>Statistics for a single generation.</summary>
    public class GenerationStats
    {
        /// <summary>Zero-based generation index.</summary>
        public int Generation { get; set; }

        /// <summary>Best fitness in this generation.</summary>
        public double BestFitness { get; set; }

        /// <summary>Average fitness of the population.</summary>
        public double AvgFitness { get; set; }

        /// <summary>Worst fitness in this generation.</summary>
        public double WorstFitness { get; set; }

        /// <summary>Population diversity (0.0 = clones, 1.0 = all unique).</summary>
        public double Diversity { get; set; }

        /// <summary>Id of the best organism in this generation.</summary>
        public string BestOrganismId { get; set; } = "";

        /// <summary>Population size in this generation.</summary>
        public int PopulationSize { get; set; }

        /// <summary>Effective mutation rate used in this generation.</summary>
        public double MutationRateUsed { get; set; }
    }

    /// <summary>Result of an evolution run.</summary>
    public class EvolutionResult
    {
        /// <summary>The best organism found across all generations.</summary>
        public PromptOrganism Champion { get; set; } = new();

        /// <summary>Per-generation statistics.</summary>
        public List<GenerationStats> History { get; set; } = new();

        /// <summary>Total number of generations executed.</summary>
        public int TotalGenerations { get; set; }

        /// <summary>Wall-clock time elapsed.</summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>Whether the fitness target threshold was reached.</summary>
        public bool ReachedTarget { get; set; }

        /// <summary>Human-readable summary of the evolution run.</summary>
        public string Summary { get; set; } = "";
    }

    // ── Engine ───────────────────────────────────────────────────────────

    /// <summary>
    /// Autonomous genetic-algorithm-inspired prompt evolution engine.
    /// Manages a population of prompt variants, scores fitness, selects parents,
    /// applies crossover and mutation operators, and tracks generational progress
    /// to evolve prompts toward optimality.
    /// </summary>
    public class PromptEvolutionEngine
    {
        private readonly EvolutionConfig _config;

        /// <summary>
        /// Optional custom fitness function. Receives prompt text, returns 0.0 – 1.0.
        /// When null, a built-in heuristic is used.
        /// </summary>
        public Func<string, double>? FitnessFunction { get; set; }

        /// <summary>Create a new evolution engine with the given configuration.</summary>
        public PromptEvolutionEngine(EvolutionConfig? config = null)
        {
            _config = config ?? new EvolutionConfig();
            _config.Validate();
        }

        // ── Synonym map ──────────────────────────────────────────────

        private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["use"] = new[] { "utilize", "employ", "leverage" },
            ["make"] = new[] { "create", "build", "construct" },
            ["get"] = new[] { "obtain", "retrieve", "acquire" },
            ["show"] = new[] { "display", "present", "reveal" },
            ["help"] = new[] { "assist", "aid", "support" },
            ["find"] = new[] { "locate", "discover", "identify" },
            ["give"] = new[] { "provide", "supply", "offer" },
            ["tell"] = new[] { "inform", "notify", "explain" },
            ["good"] = new[] { "excellent", "effective", "optimal" },
            ["bad"] = new[] { "poor", "inadequate", "subpar" },
            ["big"] = new[] { "large", "significant", "substantial" },
            ["small"] = new[] { "minor", "compact", "minimal" },
            ["fast"] = new[] { "rapid", "swift", "efficient" },
            ["slow"] = new[] { "gradual", "measured", "deliberate" },
            ["start"] = new[] { "begin", "initiate", "launch" },
            ["stop"] = new[] { "halt", "cease", "terminate" },
            ["change"] = new[] { "modify", "adjust", "alter" },
            ["check"] = new[] { "verify", "validate", "inspect" },
            ["fix"] = new[] { "repair", "resolve", "correct" },
            ["remove"] = new[] { "eliminate", "discard", "exclude" },
            ["add"] = new[] { "include", "append", "insert" },
            ["run"] = new[] { "execute", "perform", "operate" },
            ["try"] = new[] { "attempt", "endeavor", "test" },
            ["keep"] = new[] { "retain", "maintain", "preserve" },
            ["send"] = new[] { "transmit", "deliver", "dispatch" },
            ["pick"] = new[] { "select", "choose", "opt for" },
            ["need"] = new[] { "require", "demand", "necessitate" },
            ["want"] = new[] { "desire", "seek", "prefer" },
            ["think"] = new[] { "consider", "evaluate", "assess" },
            ["look"] = new[] { "examine", "review", "inspect" },
        };

        private static readonly string[] FillerWords = new[]
        {
            "just", "really", "very", "quite", "basically", "actually",
            "simply", "literally", "honestly", "clearly", "obviously",
            "essentially", "practically", "somewhat", "rather"
        };

        private static readonly string[] ClarifyingPhrases = new[]
        {
            "Ensure that", "Note that", "Be specific about",
            "Pay attention to", "Focus on", "Consider",
            "Take into account", "Keep in mind that"
        };

        private static readonly string[] FormalMarkers = new[]
        {
            "Please", "Kindly", "I would appreciate if you",
            "It is recommended that", "One should"
        };

        private static readonly string[] ActionVerbs = new[]
        {
            "analyze", "generate", "create", "list", "describe", "explain",
            "compare", "evaluate", "summarize", "classify", "identify",
            "write", "design", "implement", "calculate", "optimize",
            "review", "suggest", "recommend", "outline", "provide"
        };

        // ── Public API ───────────────────────────────────────────────

        /// <summary>
        /// Evolve a single seed prompt into an optimized version.
        /// </summary>
        /// <param name="seedPrompt">The initial prompt text.</param>
        /// <returns>Evolution result with champion and history.</returns>
        public EvolutionResult Evolve(string seedPrompt)
        {
            if (string.IsNullOrWhiteSpace(seedPrompt))
                throw new ArgumentException("Seed prompt must not be empty.", nameof(seedPrompt));
            return Evolve(new List<string> { seedPrompt });
        }

        /// <summary>
        /// Evolve a population initialized from multiple seed prompts.
        /// </summary>
        /// <param name="seedPrompts">One or more starting prompt texts.</param>
        /// <returns>Evolution result with champion and history.</returns>
        public EvolutionResult Evolve(List<string> seedPrompts)
        {
            if (seedPrompts == null || seedPrompts.Count == 0)
                throw new ArgumentException("At least one seed prompt is required.", nameof(seedPrompts));
            if (seedPrompts.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Seed prompts must not be empty.", nameof(seedPrompts));

            var sw = Stopwatch.StartNew();
            var population = InitializePopulation(seedPrompts);
            EvaluatePopulation(population);

            var history = new List<GenerationStats>();
            PromptOrganism globalBest = population.OrderByDescending(o => o.Fitness).First();
            double currentMutationRate = _config.MutationRate;
            bool reachedTarget = false;

            for (int gen = 0; gen < _config.MaxGenerations; gen++)
            {
                // Record stats
                var stats = BuildStats(population, gen, currentMutationRate);
                history.Add(stats);

                // Track global best
                var genBest = population.OrderByDescending(o => o.Fitness).First();
                if (genBest.Fitness > globalBest.Fitness)
                    globalBest = genBest;

                // Early stop
                if (globalBest.Fitness >= _config.FitnessTargetThreshold)
                {
                    reachedTarget = true;
                    break;
                }

                // Adaptive mutation
                if (_config.AdaptiveMutationRate)
                    currentMutationRate = AdaptMutationRate(stats.Diversity, _config.MutationRate);

                // Next generation
                population = ProduceNextGeneration(population, gen + 1, currentMutationRate);
                EvaluatePopulation(population);
            }

            // Final stats if didn't early-stop
            if (!reachedTarget)
            {
                var finalBest = population.OrderByDescending(o => o.Fitness).First();
                if (finalBest.Fitness > globalBest.Fitness)
                    globalBest = finalBest;
            }

            sw.Stop();
            return new EvolutionResult
            {
                Champion = globalBest,
                History = history,
                TotalGenerations = history.Count,
                Elapsed = sw.Elapsed,
                ReachedTarget = reachedTarget,
                Summary = BuildSummary(globalBest, history, sw.Elapsed, reachedTarget)
            };
        }

        // ── Population initialization ────────────────────────────────

        internal List<PromptOrganism> InitializePopulation(List<string> seeds)
        {
            var pop = new List<PromptOrganism>();
            // Add seeds as-is
            foreach (var seed in seeds)
            {
                pop.Add(new PromptOrganism { Text = seed, Generation = 0 });
            }
            // Fill remaining slots with mutations of seeds
            int idx = 0;
            while (pop.Count < _config.PopulationSize)
            {
                var baseSeed = seeds[idx % seeds.Count];
                var mutated = ApplyRandomMutation(baseSeed);
                pop.Add(new PromptOrganism
                {
                    Text = mutated.Text,
                    Generation = 0,
                    MutationsApplied = new List<string> { mutated.Strategy.ToString() }
                });
                idx++;
            }
            return pop;
        }

        // ── Evaluation ───────────────────────────────────────────────

        internal void EvaluatePopulation(List<PromptOrganism> population)
        {
            foreach (var org in population)
            {
                org.Fitness = FitnessFunction != null
                    ? Math.Clamp(FitnessFunction(org.Text), 0.0, 1.0)
                    : DefaultFitness(org.Text);
            }
        }

        internal static double DefaultFitness(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;

            double score = 0.0;

            // Length score: prefer 100-500 chars (peak at 300)
            double len = text.Length;
            if (len >= 100 && len <= 500)
                score += 0.25 * (1.0 - Math.Abs(len - 300) / 200.0);
            else if (len > 0)
                score += 0.05;

            // Action verbs presence
            var lower = text.ToLowerInvariant();
            int verbCount = ActionVerbs.Count(v => lower.Contains(v));
            score += Math.Min(0.25, verbCount * 0.05);

            // Specificity: numbers and concrete nouns (capitalized words)
            int numbers = Regex.Matches(text, @"\d+").Count;
            int capitalWords = Regex.Matches(text, @"\b[A-Z][a-z]{2,}\b").Count;
            score += Math.Min(0.25, (numbers + capitalWords) * 0.03);

            // Structure: has newlines, bullets, or numbered lists
            bool hasStructure = text.Contains('\n') || text.Contains("- ") || text.Contains("1.");
            if (hasStructure) score += 0.15;

            // Sentence count: prefer 3-8 sentences
            int sentences = SplitSentences(text).Count;
            if (sentences >= 3 && sentences <= 8) score += 0.10;
            else if (sentences >= 1) score += 0.03;

            return Math.Clamp(score, 0.0, 1.0);
        }

        // ── Selection ────────────────────────────────────────────────

        internal List<PromptOrganism> SelectParents(List<PromptOrganism> population)
        {
            return _config.Selection switch
            {
                SelectionMethod.Tournament => TournamentSelect(population),
                SelectionMethod.RouletteWheel => RouletteSelect(population),
                SelectionMethod.RankBased => RankSelect(population),
                SelectionMethod.Elitism => ElitismSelect(population),
                _ => TournamentSelect(population)
            };
        }

        private List<PromptOrganism> TournamentSelect(List<PromptOrganism> pop)
        {
            var parents = new List<PromptOrganism>();
            int needed = _config.PopulationSize - _config.EliteCount;
            for (int i = 0; i < needed; i++)
            {
                PromptOrganism? best = null;
                for (int t = 0; t < _config.TournamentSize; t++)
                {
                    var candidate = pop[Random.Shared.Next(pop.Count)];
                    if (best == null || candidate.Fitness > best.Fitness)
                        best = candidate;
                }
                parents.Add(best!);
            }
            return parents;
        }

        private List<PromptOrganism> RouletteSelect(List<PromptOrganism> pop)
        {
            double totalFitness = pop.Sum(o => o.Fitness + 0.01); // avoid zero
            var parents = new List<PromptOrganism>();
            int needed = _config.PopulationSize - _config.EliteCount;
            for (int i = 0; i < needed; i++)
            {
                double spin = Random.Shared.NextDouble() * totalFitness;
                double cumulative = 0;
                foreach (var org in pop)
                {
                    cumulative += org.Fitness + 0.01;
                    if (cumulative >= spin)
                    {
                        parents.Add(org);
                        break;
                    }
                }
                if (parents.Count <= i) parents.Add(pop[^1]); // fallback
            }
            return parents;
        }

        private List<PromptOrganism> RankSelect(List<PromptOrganism> pop)
        {
            var ranked = pop.OrderBy(o => o.Fitness).ToList();
            double totalRank = ranked.Count * (ranked.Count + 1) / 2.0;
            var parents = new List<PromptOrganism>();
            int needed = _config.PopulationSize - _config.EliteCount;
            for (int i = 0; i < needed; i++)
            {
                double spin = Random.Shared.NextDouble() * totalRank;
                double cumulative = 0;
                for (int r = 0; r < ranked.Count; r++)
                {
                    cumulative += r + 1;
                    if (cumulative >= spin)
                    {
                        parents.Add(ranked[r]);
                        break;
                    }
                }
                if (parents.Count <= i) parents.Add(ranked[^1]);
            }
            return parents;
        }

        private List<PromptOrganism> ElitismSelect(List<PromptOrganism> pop)
        {
            // For elitism, everyone is a potential parent, sorted by fitness
            return pop.OrderByDescending(o => o.Fitness).Take(_config.PopulationSize).ToList();
        }

        // ── Crossover ────────────────────────────────────────────────

        internal PromptOrganism Crossover(PromptOrganism a, PromptOrganism b, int generation)
        {
            var methods = Enum.GetValues<CrossoverMethod>();
            var method = methods[Random.Shared.Next(methods.Length)];
            string childText = method switch
            {
                CrossoverMethod.SinglePoint => SinglePointCrossover(a.Text, b.Text),
                CrossoverMethod.Uniform => UniformCrossover(a.Text, b.Text),
                CrossoverMethod.SemanticBlend => SemanticBlendCrossover(a.Text, b.Text),
                _ => SinglePointCrossover(a.Text, b.Text)
            };

            return new PromptOrganism
            {
                Text = childText,
                Generation = generation,
                ParentAId = a.Id,
                ParentBId = b.Id,
                MutationsApplied = new List<string> { $"Crossover:{method}" }
            };
        }

        internal static string SinglePointCrossover(string a, string b)
        {
            var sentA = SplitSentences(a);
            var sentB = SplitSentences(b);
            if (sentA.Count <= 1 && sentB.Count <= 1)
                return a.Length > b.Length ? a[..(a.Length / 2)] + " " + b[(b.Length / 2)..] : b;

            int splitA = Math.Max(1, sentA.Count / 2);
            int splitB = Math.Max(1, sentB.Count / 2);
            var result = sentA.Take(splitA).Concat(sentB.Skip(splitB));
            return string.Join(" ", result).Trim();
        }

        internal static string UniformCrossover(string a, string b)
        {
            var sentA = SplitSentences(a);
            var sentB = SplitSentences(b);
            int maxLen = Math.Max(sentA.Count, sentB.Count);
            var result = new List<string>();
            for (int i = 0; i < maxLen; i++)
            {
                bool pickA = Random.Shared.NextDouble() < 0.5;
                if (pickA && i < sentA.Count) result.Add(sentA[i]);
                else if (i < sentB.Count) result.Add(sentB[i]);
                else if (i < sentA.Count) result.Add(sentA[i]);
            }
            return string.Join(" ", result).Trim();
        }

        internal static string SemanticBlendCrossover(string a, string b)
        {
            var sentA = SplitSentences(a);
            var sentB = SplitSentences(b);
            var result = new List<string>();
            int maxLen = Math.Max(sentA.Count, sentB.Count);
            for (int i = 0; i < maxLen; i++)
            {
                if (i % 2 == 0 && i < sentA.Count) result.Add(sentA[i]);
                else if (i < sentB.Count) result.Add(sentB[i]);
                else if (i < sentA.Count) result.Add(sentA[i]);
            }
            return string.Join(" ", result).Trim();
        }

        // ── Mutation ─────────────────────────────────────────────────

        internal (string Text, MutationStrategy Strategy) ApplyRandomMutation(string text)
        {
            var strategies = Enum.GetValues<MutationStrategy>();
            var strategy = strategies[Random.Shared.Next(strategies.Length)];
            return (ApplyMutation(text, strategy), strategy);
        }

        internal static string ApplyMutation(string text, MutationStrategy strategy)
        {
            return strategy switch
            {
                MutationStrategy.WordShuffle => MutateWordShuffle(text),
                MutationStrategy.SynonymReplace => MutateSynonymReplace(text),
                MutationStrategy.SentenceReorder => MutateSentenceReorder(text),
                MutationStrategy.ToneShift => MutateToneShift(text),
                MutationStrategy.Compress => MutateCompress(text),
                MutationStrategy.Expand => MutateExpand(text),
                MutationStrategy.InstructionRephrase => MutateInstructionRephrase(text),
                _ => text
            };
        }

        private static string MutateWordShuffle(string text)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count == 0) return text;
            int idx = Random.Shared.Next(sentences.Count);
            var words = sentences[idx].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 2) return text;
            // Fisher-Yates on a subset to keep it somewhat coherent
            for (int i = words.Length - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (words[i], words[j]) = (words[j], words[i]);
            }
            sentences[idx] = string.Join(" ", words);
            return string.Join(" ", sentences);
        }

        private static string MutateSynonymReplace(string text)
        {
            var words = text.Split(' ');
            bool replaced = false;
            for (int i = 0; i < words.Length && !replaced; i++)
            {
                var clean = words[i].TrimEnd('.', ',', '!', '?', ';', ':');
                if (Synonyms.TryGetValue(clean, out var syns))
                {
                    var suffix = words[i][clean.Length..];
                    words[i] = syns[Random.Shared.Next(syns.Length)] + suffix;
                    replaced = true;
                }
            }
            // If nothing replaced, try from the end
            if (!replaced)
            {
                for (int i = words.Length - 1; i >= 0; i--)
                {
                    var clean = words[i].TrimEnd('.', ',', '!', '?', ';', ':');
                    if (Synonyms.TryGetValue(clean, out var syns))
                    {
                        var suffix = words[i][clean.Length..];
                        words[i] = syns[Random.Shared.Next(syns.Length)] + suffix;
                        break;
                    }
                }
            }
            return string.Join(" ", words);
        }

        private static string MutateSentenceReorder(string text)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count <= 1) return text;
            // Fisher-Yates shuffle
            for (int i = sentences.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (sentences[i], sentences[j]) = (sentences[j], sentences[i]);
            }
            return string.Join(" ", sentences);
        }

        private static string MutateToneShift(string text)
        {
            // If already formal, make casual; else make formal
            bool hasFormal = FormalMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
            if (hasFormal)
            {
                // Remove a formal marker
                foreach (var marker in FormalMarkers)
                {
                    if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        text = text.Replace(marker, "", StringComparison.OrdinalIgnoreCase).Trim();
                        break;
                    }
                }
            }
            else
            {
                // Add a formal marker
                var marker = FormalMarkers[Random.Shared.Next(FormalMarkers.Length)];
                var sentences = SplitSentences(text);
                if (sentences.Count > 0)
                {
                    int idx = Random.Shared.Next(sentences.Count);
                    sentences[idx] = marker + " " + sentences[idx][..1].ToLower() + sentences[idx][1..];
                    text = string.Join(" ", sentences);
                }
            }
            return text;
        }

        private static string MutateCompress(string text)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            // Remove filler words
            words.RemoveAll(w => FillerWords.Contains(w.TrimEnd('.', ',', '!', '?').ToLower()));
            return string.Join(" ", words);
        }

        private static string MutateExpand(string text)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count == 0) return text;
            int idx = Random.Shared.Next(sentences.Count);
            var phrase = ClarifyingPhrases[Random.Shared.Next(ClarifyingPhrases.Length)];
            sentences.Insert(idx, phrase + " the details.");
            return string.Join(" ", sentences);
        }

        private static string MutateInstructionRephrase(string text)
        {
            // Find imperative sentences (start with verb) and rephrase
            var sentences = SplitSentences(text);
            for (int i = 0; i < sentences.Count; i++)
            {
                var firstWord = sentences[i].Split(' ')[0].TrimEnd('.', ',').ToLower();
                if (ActionVerbs.Contains(firstWord))
                {
                    // Rephrase: "Analyze X" → "You should analyze X"
                    sentences[i] = "You should " + sentences[i][..1].ToLower() + sentences[i][1..];
                    break;
                }
            }
            return string.Join(" ", sentences);
        }

        // ── Next generation ──────────────────────────────────────────

        private List<PromptOrganism> ProduceNextGeneration(
            List<PromptOrganism> population, int generation, double mutationRate)
        {
            var sorted = population.OrderByDescending(o => o.Fitness).ToList();
            var nextGen = new List<PromptOrganism>();

            // Elite carry-over
            int eliteCount = Math.Min(_config.EliteCount, sorted.Count);
            for (int i = 0; i < eliteCount; i++)
            {
                var elite = sorted[i];
                nextGen.Add(new PromptOrganism
                {
                    Text = elite.Text,
                    Generation = generation,
                    Fitness = elite.Fitness,
                    ParentAId = elite.Id,
                    MutationsApplied = new List<string> { "Elite" }
                });
            }

            // Fill rest via selection + crossover/mutation
            var parents = SelectParents(population);
            int idx = 0;
            while (nextGen.Count < _config.PopulationSize)
            {
                var parentA = parents[idx % parents.Count];
                var parentB = parents[(idx + 1) % parents.Count];
                idx += 2;

                PromptOrganism child;
                if (Random.Shared.NextDouble() < _config.CrossoverRate && parentA.Id != parentB.Id)
                {
                    child = Crossover(parentA, parentB, generation);
                }
                else
                {
                    child = new PromptOrganism
                    {
                        Text = parentA.Text,
                        Generation = generation,
                        ParentAId = parentA.Id
                    };
                }

                // Mutation
                if (Random.Shared.NextDouble() < mutationRate)
                {
                    var (mutatedText, strategy) = ApplyRandomMutation(child.Text);
                    child.Text = mutatedText;
                    child.MutationsApplied.Add(strategy.ToString());
                }

                nextGen.Add(child);
            }

            return nextGen;
        }

        // ── Diversity ────────────────────────────────────────────────

        internal static double CalculateDiversity(List<PromptOrganism> population)
        {
            if (population.Count <= 1) return 0.0;

            // Average pairwise Jaccard distance on word sets
            double totalDistance = 0;
            int pairs = 0;
            var wordSets = population.Select(o =>
                new HashSet<string>(o.Text.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            ).ToList();

            for (int i = 0; i < wordSets.Count; i++)
            {
                for (int j = i + 1; j < wordSets.Count; j++)
                {
                    int intersection = wordSets[i].Intersect(wordSets[j]).Count();
                    int union = wordSets[i].Union(wordSets[j]).Count();
                    double jaccard = union > 0 ? (double)intersection / union : 1.0;
                    totalDistance += 1.0 - jaccard;
                    pairs++;
                }
            }

            return pairs > 0 ? Math.Clamp(totalDistance / pairs, 0.0, 1.0) : 0.0;
        }

        internal static double AdaptMutationRate(double diversity, double baseRate)
        {
            // Low diversity → increase mutation; high diversity → decrease
            if (diversity < 0.2)
                return Math.Min(0.8, baseRate * 2.5);
            if (diversity < 0.4)
                return Math.Min(0.6, baseRate * 1.5);
            if (diversity > 0.8)
                return Math.Max(0.02, baseRate * 0.5);
            return baseRate;
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static List<string> SplitSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            // Split on sentence-ending punctuation or newlines
            var parts = Regex.Split(text, @"(?<=[.!?])\s+|\n+")
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            return parts.Count > 0 ? parts : new List<string> { text.Trim() };
        }

        private GenerationStats BuildStats(List<PromptOrganism> population, int gen, double mutationRate)
        {
            var best = population.OrderByDescending(o => o.Fitness).First();
            return new GenerationStats
            {
                Generation = gen,
                BestFitness = best.Fitness,
                AvgFitness = population.Average(o => o.Fitness),
                WorstFitness = population.Min(o => o.Fitness),
                Diversity = CalculateDiversity(population),
                BestOrganismId = best.Id,
                PopulationSize = population.Count,
                MutationRateUsed = mutationRate
            };
        }

        private static string BuildSummary(
            PromptOrganism champion, List<GenerationStats> history, TimeSpan elapsed, bool reachedTarget)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══ Evolution Summary ═══");
            sb.AppendLine($"Generations: {history.Count}");
            sb.AppendLine($"Elapsed: {elapsed.TotalMilliseconds:F0}ms");
            sb.AppendLine($"Target reached: {(reachedTarget ? "Yes ✓" : "No")}");
            if (history.Count > 0)
            {
                sb.AppendLine($"Initial best fitness: {history[0].BestFitness:F4}");
                sb.AppendLine($"Final best fitness:   {champion.Fitness:F4}");
                double improvement = champion.Fitness - history[0].BestFitness;
                sb.AppendLine($"Improvement: {(improvement >= 0 ? "+" : "")}{improvement:F4}");
            }
            sb.AppendLine($"Champion: {champion.Id} (gen {champion.Generation})");
            sb.AppendLine($"Champion text length: {champion.Text.Length} chars");
            return sb.ToString();
        }
    }
}
