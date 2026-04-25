using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Prompt
{
    /// <summary>
    /// Strategy for combining two parent prompts during crossover.
    /// </summary>
    public enum CrossoverStrategy
    {
        /// <summary>Split at sentence boundaries and interleave.</summary>
        SentenceInterleave,

        /// <summary>Take instructions from one parent and tone/style from the other.</summary>
        InstructionStyleSwap,

        /// <summary>Merge by paragraph, alternating between parents.</summary>
        ParagraphAlternate,

        /// <summary>Combine unique sentences from both parents.</summary>
        UnionMerge,

        /// <summary>Use weighted random selection of sentences from both parents.</summary>
        WeightedRandom
    }

    /// <summary>
    /// Types of mutations that can be applied to prompt offspring.
    /// </summary>
    public enum MutationType
    {
        /// <summary>Rephrase a sentence to be more concise.</summary>
        Condense,

        /// <summary>Rephrase a sentence to be more detailed.</summary>
        Expand,

        /// <summary>Change the tone of a sentence (formal ↔ casual).</summary>
        ToneShift,

        /// <summary>Add emphasis markers (e.g., "Important:", "Note:").</summary>
        AddEmphasis,

        /// <summary>Remove filler words and redundancies.</summary>
        RemoveFiller,

        /// <summary>Reorder sentences for better logical flow.</summary>
        Reorder,

        /// <summary>Inject a constraint or guardrail sentence.</summary>
        AddConstraint,

        /// <summary>Swap synonyms for key terms.</summary>
        SynonymSwap
    }

    /// <summary>
    /// Fitness criteria for evaluating prompt offspring quality.
    /// </summary>
    public enum FitnessCriterion
    {
        /// <summary>Shorter prompts score higher (token efficiency).</summary>
        Brevity,

        /// <summary>More specific instructions score higher.</summary>
        Specificity,

        /// <summary>Better readability scores higher (simpler language).</summary>
        Readability,

        /// <summary>More constraints/guardrails score higher.</summary>
        Safety,

        /// <summary>Balanced combination of all criteria.</summary>
        Balanced
    }

    /// <summary>
    /// An evolved prompt variant with lineage tracking.
    /// </summary>
    public class PromptOffspring
    {
        public PromptOffspring(
            string text,
            int generation,
            string parentA,
            string parentB,
            CrossoverStrategy crossover,
            IReadOnlyList<MutationType> mutations,
            Dictionary<FitnessCriterion, double> fitnessScores)
        {
            Text = text;
            Generation = generation;
            ParentAHash = ComputeHash(parentA);
            ParentBHash = ComputeHash(parentB);
            CrossoverUsed = crossover;
            MutationsApplied = mutations;
            FitnessScores = fitnessScores;
            CreatedAt = DateTimeOffset.UtcNow;
            Id = Guid.NewGuid().ToString("N")[..8];
        }

        [JsonPropertyName("id")]
        public string Id { get; }

        [JsonPropertyName("text")]
        public string Text { get; }

        [JsonPropertyName("generation")]
        public int Generation { get; }

        [JsonPropertyName("parentAHash")]
        public string ParentAHash { get; }

        [JsonPropertyName("parentBHash")]
        public string ParentBHash { get; }

        [JsonPropertyName("crossoverUsed")]
        public CrossoverStrategy CrossoverUsed { get; }

        [JsonPropertyName("mutationsApplied")]
        public IReadOnlyList<MutationType> MutationsApplied { get; }

        [JsonPropertyName("fitnessScores")]
        public Dictionary<FitnessCriterion, double> FitnessScores { get; }

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; }

        [JsonPropertyName("overallFitness")]
        public double OverallFitness => FitnessScores.Count > 0
            ? FitnessScores.Values.Average()
            : 0.0;

        private static string ComputeHash(string input)
        {
            int hash = 17;
            foreach (char c in input)
                hash = hash * 31 + c;
            return Math.Abs(hash).ToString("x8");
        }
    }

    /// <summary>
    /// Configuration for a co-evolution run.
    /// </summary>
    public class CoEvolutionConfig
    {
        /// <summary>Number of offspring to produce per generation.</summary>
        [JsonPropertyName("populationSize")]
        public int PopulationSize { get; set; } = 6;

        /// <summary>Number of generations to evolve.</summary>
        [JsonPropertyName("generations")]
        public int Generations { get; set; } = 3;

        /// <summary>Probability (0.0–1.0) of each mutation type being applied.</summary>
        [JsonPropertyName("mutationRate")]
        public double MutationRate { get; set; } = 0.3;

        /// <summary>Which fitness criteria to optimize for.</summary>
        [JsonPropertyName("fitnessCriteria")]
        public FitnessCriterion FitnessCriteria { get; set; } = FitnessCriterion.Balanced;

        /// <summary>Crossover strategies to use (cycles through them).</summary>
        [JsonPropertyName("strategies")]
        public List<CrossoverStrategy> Strategies { get; set; } = new()
        {
            CrossoverStrategy.SentenceInterleave,
            CrossoverStrategy.InstructionStyleSwap,
            CrossoverStrategy.ParagraphAlternate
        };

        /// <summary>Seed for reproducible evolution. Null for random.</summary>
        [JsonPropertyName("seed")]
        public int? Seed { get; set; }

        /// <summary>If true, keep all generations in the result. Otherwise only the final.</summary>
        [JsonPropertyName("keepLineage")]
        public bool KeepLineage { get; set; } = true;
    }

    /// <summary>
    /// Result of a co-evolution run.
    /// </summary>
    public class CoEvolutionResult
    {
        [JsonPropertyName("parentA")]
        public string ParentA { get; init; } = "";

        [JsonPropertyName("parentB")]
        public string ParentB { get; init; } = "";

        [JsonPropertyName("config")]
        public CoEvolutionConfig Config { get; init; } = new();

        [JsonPropertyName("generations")]
        public List<List<PromptOffspring>> Generations { get; init; } = new();

        [JsonPropertyName("bestOffspring")]
        public PromptOffspring? BestOffspring { get; init; }

        [JsonPropertyName("totalVariantsProduced")]
        public int TotalVariantsProduced { get; init; }

        /// <summary>
        /// Export the evolution results as a formatted summary.
        /// </summary>
        public string ToSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══ Prompt Co-Evolution Report ═══");
            sb.AppendLine();
            sb.AppendLine($"Parent A: \"{StringHelpers.Truncate(ParentA, 60)}\"");
            sb.AppendLine($"Parent B: \"{StringHelpers.Truncate(ParentB, 60)}\"");
            sb.AppendLine($"Generations: {Config.Generations} | Population: {Config.PopulationSize} | Mutation Rate: {Config.MutationRate:P0}");
            sb.AppendLine($"Total Variants: {TotalVariantsProduced}");
            sb.AppendLine();

            for (int g = 0; g < Generations.Count; g++)
            {
                sb.AppendLine($"── Generation {g + 1} ──");
                foreach (var offspring in Generations[g].OrderByDescending(o => o.OverallFitness))
                {
                    sb.AppendLine($"  [{offspring.Id}] Fitness: {offspring.OverallFitness:F2} | {offspring.CrossoverUsed} + {offspring.MutationsApplied.Count} mutations");
                    sb.AppendLine($"    \"{StringHelpers.Truncate(offspring.Text, 80)}\"");
                }
                sb.AppendLine();
            }

            if (BestOffspring != null)
            {
                sb.AppendLine("★ Best Offspring ★");
                sb.AppendLine($"  ID: {BestOffspring.Id} | Gen: {BestOffspring.Generation} | Fitness: {BestOffspring.OverallFitness:F2}");
                sb.AppendLine($"  \"{BestOffspring.Text}\"");
                sb.AppendLine("  Fitness Breakdown:");
                foreach (var (criterion, score) in BestOffspring.FitnessScores)
                    sb.AppendLine($"    {criterion}: {score:F2}");
            }

            return sb.ToString();
        }

        /// <summary>Export as JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

    }

    /// <summary>
    /// Genetic-algorithm-inspired prompt co-evolution engine. Takes two parent prompts
    /// and breeds offspring variants through crossover and mutation, scoring them with
    /// configurable fitness criteria. Useful for exploring the prompt design space and
    /// discovering effective prompt formulations.
    /// </summary>
    /// <example>
    /// <code>
    /// var evolver = new PromptCoEvolver();
    /// var result = evolver.Evolve(
    ///     "You are a helpful assistant. Answer concisely.",
    ///     "You are an expert analyst. Provide detailed, structured responses with examples.",
    ///     new CoEvolutionConfig { Generations = 3, PopulationSize = 4 });
    /// Console.WriteLine(result.ToSummary());
    /// Console.WriteLine($"Best: {result.BestOffspring?.Text}");
    /// </code>
    /// </example>
    public class PromptCoEvolver
    {
        private static readonly string[] FillerWords = {
            "just", "really", "very", "quite", "simply", "basically",
            "actually", "literally", "honestly", "obviously"
        };

        private static readonly string[] EmphasisPrefixes = {
            "Important:", "Note:", "Critical:", "Key point:",
            "Remember:", "Essential:"
        };

        private static readonly string[] ConstraintSentences = {
            "Do not include personal opinions.",
            "Stay within the scope of the question.",
            "If uncertain, say so explicitly.",
            "Cite sources when possible.",
            "Keep responses under 500 words unless asked otherwise.",
            "Use professional language throughout.",
            "Avoid making assumptions about the user's intent.",
            "Prioritize accuracy over speed."
        };

        private static readonly Dictionary<string, string> SynonymMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["helpful"] = "supportive",
            ["detailed"] = "thorough",
            ["concise"] = "succinct",
            ["answer"] = "respond",
            ["provide"] = "deliver",
            ["expert"] = "specialist",
            ["analyze"] = "examine",
            ["explain"] = "clarify",
            ["important"] = "crucial",
            ["ensure"] = "guarantee",
            ["create"] = "generate",
            ["use"] = "employ",
            ["think"] = "consider",
            ["show"] = "demonstrate",
            ["good"] = "effective"
        };

        // Pre-compiled regexes for filler word removal — avoids recompiling
        // 10 Regex objects on every MutateRemoveFiller call (once per offspring).
        private static readonly (Regex pattern, string replacement)[] FillerRegexes =
            FillerWords.Select(f => (new Regex($@"\b{Regex.Escape(f)}\b\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled), "")).ToArray();

        private static readonly Regex MultiSpaceRegex = new(@"\s{2,}", RegexOptions.Compiled);

        // Pre-compiled regexes for synonym swap — avoids recompiling per call.
        private static readonly (Regex pattern, string synonym)[] SynonymRegexes =
            SynonymMap.Select(kv => (new Regex($@"\b{Regex.Escape(kv.Key)}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), kv.Value)).ToArray();

        private Random _rng;

        public PromptCoEvolver()
        {
            _rng = new Random();
        }

        /// <summary>
        /// Evolve two parent prompts into optimized offspring over multiple generations.
        /// </summary>
        /// <param name="parentA">First parent prompt.</param>
        /// <param name="parentB">Second parent prompt.</param>
        /// <param name="config">Evolution configuration. Uses defaults if null.</param>
        /// <returns>Complete evolution results with lineage and best offspring.</returns>
        public CoEvolutionResult Evolve(string parentA, string parentB, CoEvolutionConfig? config = null)
        {
            config ??= new CoEvolutionConfig();
            _rng = config.Seed.HasValue ? new Random(config.Seed.Value) : new Random();

            var allGenerations = new List<List<PromptOffspring>>();
            var currentParents = new List<string> { parentA, parentB };
            int totalVariants = 0;
            PromptOffspring? globalBest = null;

            for (int gen = 0; gen < config.Generations; gen++)
            {
                var generation = new List<PromptOffspring>();

                for (int i = 0; i < config.PopulationSize; i++)
                {
                    // Pick two parents (from originals or top performers of previous gen)
                    string pA = currentParents[_rng.Next(currentParents.Count)];
                    string pB = currentParents[_rng.Next(currentParents.Count)];
                    if (pA == pB && currentParents.Count > 1)
                        pB = currentParents.First(p => p != pA);

                    // Crossover
                    var strategy = config.Strategies[i % config.Strategies.Count];
                    string child = ApplyCrossover(pA, pB, strategy);

                    // Mutation
                    var mutations = new List<MutationType>();
                    foreach (MutationType mt in Enum.GetValues<MutationType>())
                    {
                        if (_rng.NextDouble() < config.MutationRate)
                        {
                            child = ApplyMutation(child, mt);
                            mutations.Add(mt);
                        }
                    }

                    // Fitness evaluation
                    var fitness = EvaluateFitness(child, config.FitnessCriteria);

                    var offspring = new PromptOffspring(
                        child, gen + 1, pA, pB, strategy, mutations, fitness);
                    generation.Add(offspring);

                    if (globalBest == null || offspring.OverallFitness > globalBest.OverallFitness)
                        globalBest = offspring;
                }

                allGenerations.Add(generation);
                totalVariants += generation.Count;

                // Select top performers as parents for next generation
                var topPerformers = generation
                    .OrderByDescending(o => o.OverallFitness)
                    .Take(Math.Max(2, config.PopulationSize / 2))
                    .Select(o => o.Text)
                    .ToList();

                // Always keep originals in the gene pool
                currentParents = new List<string> { parentA, parentB };
                currentParents.AddRange(topPerformers);
                currentParents = currentParents.Distinct().ToList();
            }

            return new CoEvolutionResult
            {
                ParentA = parentA,
                ParentB = parentB,
                Config = config,
                Generations = config.KeepLineage ? allGenerations : new List<List<PromptOffspring>> { allGenerations.Last() },
                BestOffspring = globalBest,
                TotalVariantsProduced = totalVariants
            };
        }

        /// <summary>
        /// Quick-evolve: produce a single generation with default settings.
        /// </summary>
        public List<PromptOffspring> QuickEvolve(string parentA, string parentB, int count = 4)
        {
            var result = Evolve(parentA, parentB, new CoEvolutionConfig
            {
                Generations = 1,
                PopulationSize = count,
                KeepLineage = true
            });
            return result.Generations.FirstOrDefault() ?? new List<PromptOffspring>();
        }

        /// <summary>
        /// Compare two offspring head-to-head across all fitness criteria.
        /// </summary>
        public Dictionary<FitnessCriterion, string> CompareOffspring(PromptOffspring a, PromptOffspring b)
        {
            var result = new Dictionary<FitnessCriterion, string>();
            foreach (var criterion in Enum.GetValues<FitnessCriterion>())
            {
                double scoreA = a.FitnessScores.GetValueOrDefault(criterion, 0);
                double scoreB = b.FitnessScores.GetValueOrDefault(criterion, 0);
                result[criterion] = scoreA > scoreB ? a.Id : scoreB > scoreA ? b.Id : "tie";
            }
            return result;
        }

        #region Crossover Strategies

        private string ApplyCrossover(string parentA, string parentB, CrossoverStrategy strategy)
        {
            return strategy switch
            {
                CrossoverStrategy.SentenceInterleave => SentenceInterleave(parentA, parentB),
                CrossoverStrategy.InstructionStyleSwap => InstructionStyleSwap(parentA, parentB),
                CrossoverStrategy.ParagraphAlternate => ParagraphAlternate(parentA, parentB),
                CrossoverStrategy.UnionMerge => UnionMerge(parentA, parentB),
                CrossoverStrategy.WeightedRandom => WeightedRandom(parentA, parentB),
                _ => parentA
            };
        }

        private string SentenceInterleave(string a, string b)
        {
            var sentA = SplitSentences(a);
            var sentB = SplitSentences(b);
            var result = new List<string>();
            // O(1) duplicate check instead of O(n) List.Contains per sentence
            var seen = new HashSet<string>(StringComparer.Ordinal);

            int maxLen = Math.Max(sentA.Count, sentB.Count);
            for (int i = 0; i < maxLen; i++)
            {
                if (i < sentA.Count && seen.Add(sentA[i]))
                    result.Add(sentA[i]);
                if (i < sentB.Count && seen.Add(sentB[i]))
                    result.Add(sentB[i]);
            }

            return string.Join(" ", result);
        }

        private string InstructionStyleSwap(string a, string b)
        {
            // Extract "instruction" sentences (imperatives) from A
            // and "style" sentences (descriptive) from B
            var sentA = SplitSentences(a);
            var sentB = SplitSentences(b);

            var instructions = sentA.Where(s => IsInstruction(s)).ToList();
            var style = sentB.Where(s => !IsInstruction(s)).ToList();

            if (instructions.Count == 0) instructions = sentA.Take(1).ToList();
            if (style.Count == 0) style = sentB.Take(1).ToList();

            var result = new List<string>();
            result.AddRange(style);
            result.AddRange(instructions);
            return string.Join(" ", result);
        }

        private string ParagraphAlternate(string a, string b)
        {
            var paraA = SplitParagraphs(a);
            var paraB = SplitParagraphs(b);
            var result = new List<string>();

            int maxLen = Math.Max(paraA.Count, paraB.Count);
            for (int i = 0; i < maxLen; i++)
            {
                if (i % 2 == 0 && i < paraA.Count)
                    result.Add(paraA[i]);
                else if (i < paraB.Count)
                    result.Add(paraB[i]);
                else if (i < paraA.Count)
                    result.Add(paraA[i]);
            }

            return string.Join("\n\n", result);
        }

        private string UnionMerge(string a, string b)
        {
            var sentA = SplitSentences(a);
            var sentB = SplitSentences(b);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var s in sentA.Concat(sentB))
            {
                var normalized = s.Trim().ToLowerInvariant();
                if (seen.Add(normalized))
                    result.Add(s);
            }

            return string.Join(" ", result);
        }

        private string WeightedRandom(string a, string b)
        {
            var sentA = SplitSentences(a);
            var sentB = SplitSentences(b);
            var all = sentA.Select(s => (s, 0.5 + _rng.NextDouble() * 0.5))
                .Concat(sentB.Select(s => (s, 0.3 + _rng.NextDouble() * 0.5)))
                .OrderByDescending(x => x.Item2)
                .Select(x => x.s)
                .Distinct()
                .ToList();

            int take = Math.Max(2, (sentA.Count + sentB.Count) / 2);
            return string.Join(" ", all.Take(take));
        }

        #endregion

        #region Mutations

        private string ApplyMutation(string text, MutationType mutation)
        {
            return mutation switch
            {
                MutationType.Condense => MutateCondense(text),
                MutationType.Expand => MutateExpand(text),
                MutationType.ToneShift => MutateToneShift(text),
                MutationType.AddEmphasis => MutateAddEmphasis(text),
                MutationType.RemoveFiller => MutateRemoveFiller(text),
                MutationType.Reorder => MutateReorder(text),
                MutationType.AddConstraint => MutateAddConstraint(text),
                MutationType.SynonymSwap => MutateSynonymSwap(text),
                _ => text
            };
        }

        private string MutateCondense(string text)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count <= 2) return text;

            // Remove the least informative sentence (shortest by word count)
            var shortest = sentences.OrderBy(s => s.Split(' ').Length).First();
            sentences.Remove(shortest);
            return string.Join(" ", sentences);
        }

        private string MutateExpand(string text)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count == 0) return text;

            // Pick a random sentence and add a clarifying follow-up
            int idx = _rng.Next(sentences.Count);
            string[] expansions = {
                "Be thorough in this regard.",
                "Consider edge cases when doing so.",
                "Provide examples where helpful.",
                "Explain your reasoning step by step."
            };
            sentences.Insert(idx + 1, expansions[_rng.Next(expansions.Length)]);
            return string.Join(" ", sentences);
        }

        private string MutateToneShift(string text)
        {
            // Toggle between formal and casual markers
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Please"] = "Make sure to",
                ["Make sure to"] = "Please",
                ["You should"] = "It's recommended to",
                ["It's recommended to"] = "You should",
                ["Don't"] = "Avoid",
                ["Avoid"] = "Don't",
                ["must"] = "should",
                ["shall"] = "should"
            };

            foreach (var (from, to) in replacements)
            {
                if (text.Contains(from, StringComparison.OrdinalIgnoreCase))
                {
                    text = Regex.Replace(text, Regex.Escape(from), to, RegexOptions.IgnoreCase);
                    break; // Only one tone shift per mutation
                }
            }

            return text;
        }

        private string MutateAddEmphasis(string text)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count == 0) return text;

            int idx = _rng.Next(sentences.Count);
            string prefix = EmphasisPrefixes[_rng.Next(EmphasisPrefixes.Length)];

            // Don't double-emphasize
            if (!EmphasisPrefixes.Any(e => sentences[idx].StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                sentences[idx] = $"{prefix} {sentences[idx]}";

            return string.Join(" ", sentences);
        }

        private string MutateRemoveFiller(string text)
        {
            foreach (var (pattern, replacement) in FillerRegexes)
                text = pattern.Replace(text, replacement);
            return MultiSpaceRegex.Replace(text.Trim(), " ");
        }

        private string MutateReorder(string text)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count <= 2) return text;

            // Shuffle middle sentences, keep first and last in place
            var middle = sentences.Skip(1).Take(sentences.Count - 2).ToList();
            for (int i = middle.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (middle[i], middle[j]) = (middle[j], middle[i]);
            }

            var result = new List<string> { sentences[0] };
            result.AddRange(middle);
            result.Add(sentences[^1]);
            return string.Join(" ", result);
        }

        private string MutateAddConstraint(string text)
        {
            string constraint = ConstraintSentences[_rng.Next(ConstraintSentences.Length)];
            if (!text.Contains(constraint, StringComparison.OrdinalIgnoreCase))
                text = text.TrimEnd() + " " + constraint;
            return text;
        }

        private string MutateSynonymSwap(string text)
        {
            int swaps = 0;
            foreach (var (pattern, synonym) in SynonymRegexes)
            {
                if (swaps >= 2) break;
                if (pattern.IsMatch(text))
                {
                    text = pattern.Replace(text, synonym);
                    swaps++;
                }
            }
            return text;
        }

        #endregion

        #region Fitness Evaluation

        private Dictionary<FitnessCriterion, double> EvaluateFitness(string text, FitnessCriterion primary)
        {
            var scores = new Dictionary<FitnessCriterion, double>
            {
                [FitnessCriterion.Brevity] = ScoreBrevity(text),
                [FitnessCriterion.Specificity] = ScoreSpecificity(text),
                [FitnessCriterion.Readability] = ScoreReadability(text),
                [FitnessCriterion.Safety] = ScoreSafety(text),
                [FitnessCriterion.Balanced] = 0.0
            };

            // Balanced is weighted average
            scores[FitnessCriterion.Balanced] =
                scores[FitnessCriterion.Brevity] * 0.2 +
                scores[FitnessCriterion.Specificity] * 0.3 +
                scores[FitnessCriterion.Readability] * 0.25 +
                scores[FitnessCriterion.Safety] * 0.25;

            // Boost the primary criterion
            if (primary != FitnessCriterion.Balanced && scores.ContainsKey(primary))
                scores[primary] = Math.Min(1.0, scores[primary] * 1.2);

            return scores;
        }

        private static double ScoreBrevity(string text)
        {
            int words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            // Optimal range: 20-80 words
            if (words is >= 20 and <= 80) return 1.0;
            if (words < 20) return 0.5 + (words / 40.0);
            return Math.Max(0.1, 1.0 - (words - 80) / 200.0);
        }

        private static double ScoreSpecificity(string text)
        {
            double score = 0.3; // Base score
            string lower = text.ToLowerInvariant();

            // Action verbs boost specificity
            string[] actionVerbs = { "analyze", "list", "compare", "explain", "describe",
                "calculate", "summarize", "evaluate", "identify", "classify" };
            score += actionVerbs.Count(v => lower.Contains(v)) * 0.1;

            // Constraints boost specificity
            if (lower.Contains("do not") || lower.Contains("don't") || lower.Contains("avoid"))
                score += 0.15;
            if (lower.Contains("format") || lower.Contains("structure"))
                score += 0.1;
            if (Regex.IsMatch(lower, @"\d+"))
                score += 0.1; // Numbers suggest specificity

            return Math.Min(1.0, score);
        }

        private static double ScoreReadability(string text)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0.0;

            // Average word length (simpler words = better readability)
            double avgWordLen = words.Average(w => w.Length);
            double wordScore = avgWordLen <= 5 ? 1.0 : Math.Max(0.2, 1.0 - (avgWordLen - 5) / 10.0);

            // Sentence count and length variance
            var sentences = SplitSentencesStatic(text);
            double sentScore = sentences.Count is >= 2 and <= 8 ? 1.0 : 0.6;

            return wordScore * 0.6 + sentScore * 0.4;
        }

        private static double ScoreSafety(string text)
        {
            double score = 0.3;
            string lower = text.ToLowerInvariant();

            string[] safetyIndicators = {
                "do not", "don't", "avoid", "never", "ensure", "important",
                "careful", "note:", "warning", "constraint", "limit",
                "must not", "should not", "refrain"
            };

            score += safetyIndicators.Count(s => lower.Contains(s)) * 0.08;

            // Role definition is a safety indicator
            if (lower.Contains("you are") || lower.Contains("your role"))
                score += 0.1;

            return Math.Min(1.0, score);
        }

        #endregion

        #region Utilities

        private static List<string> SplitSentences(string text) => SplitSentencesStatic(text);

        private static List<string> SplitSentencesStatic(string text) =>
            TextAnalysisHelpers.SplitSentences(text);

        private static List<string> SplitParagraphs(string text)
        {
            return text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList();
        }

        private static bool IsInstruction(string sentence)
        {
            string lower = sentence.TrimStart().ToLowerInvariant();
            string[] imperativeStarts = {
                "do ", "don't", "always", "never", "make sure", "ensure",
                "provide", "include", "use ", "avoid", "keep ", "be ",
                "answer", "respond", "analyze", "list", "explain"
            };
            return imperativeStarts.Any(s => lower.StartsWith(s));
        }

        #endregion
    }
}