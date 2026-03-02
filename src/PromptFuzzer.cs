namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Specifies which fuzzing strategies to apply when generating prompt variations.
    /// Strategies can be combined using bitwise OR.
    /// </summary>
    [Flags]
    public enum FuzzStrategy
    {
        /// <summary>No fuzzing applied.</summary>
        None = 0,

        /// <summary>Replace words with common synonyms.</summary>
        SynonymSwap = 1,

        /// <summary>Introduce realistic typos (transposition, duplication, omission).</summary>
        TypoInjection = 2,

        /// <summary>Randomize letter casing (upper, lower, title).</summary>
        CaseChange = 4,

        /// <summary>Remove words from the prompt.</summary>
        WordDrop = 8,

        /// <summary>Swap the order of adjacent words.</summary>
        WordShuffle = 16,

        /// <summary>Add filler words or whitespace noise.</summary>
        NoiseInjection = 32,

        /// <summary>Truncate the prompt at various points.</summary>
        Truncation = 64,

        /// <summary>Apply all strategies.</summary>
        All = SynonymSwap | TypoInjection | CaseChange | WordDrop | WordShuffle | NoiseInjection | Truncation
    }

    /// <summary>
    /// Represents a single fuzzed variant of a prompt, including the
    /// mutation applied and a description of the change.
    /// </summary>
    public class FuzzedPrompt
    {
        /// <summary>Gets the fuzzed prompt text.</summary>
        [JsonPropertyName("text")]
        public string Text { get; internal set; } = "";

        /// <summary>Gets the strategy that produced this variant.</summary>
        [JsonPropertyName("strategy")]
        public string Strategy { get; internal set; } = "";

        /// <summary>Gets a human-readable description of what changed.</summary>
        [JsonPropertyName("description")]
        public string Description { get; internal set; } = "";

        /// <summary>Gets the similarity to the original (0.0–1.0).</summary>
        [JsonPropertyName("similarity")]
        public double Similarity { get; internal set; }
    }

    /// <summary>
    /// Result of a fuzzing operation containing the original prompt
    /// and all generated variants.
    /// </summary>
    public class FuzzResult
    {
        /// <summary>Gets the original prompt text.</summary>
        [JsonPropertyName("original")]
        public string Original { get; internal set; } = "";

        /// <summary>Gets the generated fuzzed variants.</summary>
        [JsonPropertyName("variants")]
        public IReadOnlyList<FuzzedPrompt> Variants { get; internal set; }
            = Array.Empty<FuzzedPrompt>();

        /// <summary>Gets the strategies that were applied.</summary>
        [JsonPropertyName("strategiesApplied")]
        public IReadOnlyList<string> StrategiesApplied { get; internal set; }
            = Array.Empty<string>();

        /// <summary>
        /// Serializes the fuzz result to JSON.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            return JsonSerializer.Serialize(this, SerializationGuards.WriteOptions(indented));
        }
    }

    /// <summary>
    /// Generates prompt variations for robustness testing. Applies controlled
    /// mutations (synonym swaps, typos, case changes, word drops, shuffles,
    /// noise, truncation) to test whether a prompt produces consistent results
    /// across minor perturbations.
    /// </summary>
    /// <remarks>
    /// <para>Example usage:</para>
    /// <code>
    /// // Generate 5 variants using all strategies
    /// var result = PromptFuzzer.Fuzz("Explain quantum computing in simple terms", count: 5);
    /// foreach (var v in result.Variants)
    ///     Console.WriteLine($"[{v.Strategy}] {v.Text}");
    ///
    /// // Use specific strategies only
    /// var typos = PromptFuzzer.Fuzz("List the top 10 languages",
    ///     strategies: FuzzStrategy.TypoInjection | FuzzStrategy.CaseChange);
    ///
    /// // Fuzz a template's rendered output
    /// var template = new PromptTemplate("Summarize {{topic}} for {{audience}}");
    /// string rendered = template.Render(new() { ["topic"] = "AI safety", ["audience"] = "beginners" });
    /// var fuzzed = PromptFuzzer.Fuzz(rendered, count: 3);
    /// </code>
    /// </remarks>
    public static class PromptFuzzer
    {
        private static readonly Random Rng = new();

        private static readonly Regex WordBoundary = new(@"\b(\w+)\b", RegexOptions.Compiled);

        // Common synonym pairs for fuzzing
        private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["explain"] = new[] { "describe", "clarify", "elaborate on", "detail" },
            ["list"] = new[] { "enumerate", "name", "provide", "give" },
            ["describe"] = new[] { "explain", "outline", "detail", "depict" },
            ["create"] = new[] { "generate", "produce", "make", "build" },
            ["show"] = new[] { "display", "present", "demonstrate", "reveal" },
            ["find"] = new[] { "locate", "identify", "discover", "search for" },
            ["use"] = new[] { "utilize", "employ", "apply", "leverage" },
            ["help"] = new[] { "assist", "aid", "support", "guide" },
            ["write"] = new[] { "compose", "draft", "author", "produce" },
            ["make"] = new[] { "create", "build", "construct", "develop" },
            ["good"] = new[] { "excellent", "great", "effective", "quality" },
            ["bad"] = new[] { "poor", "ineffective", "subpar", "inadequate" },
            ["big"] = new[] { "large", "substantial", "significant", "major" },
            ["small"] = new[] { "minor", "tiny", "compact", "brief" },
            ["important"] = new[] { "critical", "crucial", "essential", "key" },
            ["simple"] = new[] { "basic", "straightforward", "easy", "plain" },
            ["complex"] = new[] { "complicated", "intricate", "sophisticated", "involved" },
            ["provide"] = new[] { "give", "supply", "offer", "furnish" },
            ["analyze"] = new[] { "examine", "evaluate", "assess", "review" },
            ["compare"] = new[] { "contrast", "differentiate", "distinguish", "weigh" },
            ["summarize"] = new[] { "recap", "condense", "outline", "brief" },
            ["include"] = new[] { "contain", "incorporate", "encompass", "cover" },
        };

        private static readonly string[] FillerWords = { "basically", "actually", "really", "just", "please", "kindly" };

        /// <summary>
        /// Generates fuzzed variants of a prompt for robustness testing.
        /// </summary>
        /// <param name="prompt">The original prompt to fuzz.</param>
        /// <param name="count">Number of variants to generate (1–50). Default: 5.</param>
        /// <param name="strategies">Which fuzzing strategies to apply. Default: All.</param>
        /// <param name="seed">Optional random seed for reproducible results.</param>
        /// <returns>A <see cref="FuzzResult"/> containing the original and all variants.</returns>
        /// <exception cref="ArgumentException">Thrown when prompt is null/empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when count is out of range.</exception>
        public static FuzzResult Fuzz(string prompt, int count = 5,
            FuzzStrategy strategies = FuzzStrategy.All, int? seed = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            if (count < 1 || count > 50)
                throw new ArgumentOutOfRangeException(nameof(count), count,
                    "Count must be between 1 and 50.");

            var rng = seed.HasValue ? new Random(seed.Value) : Rng;

            // Collect active strategies
            var activeStrategies = new List<FuzzStrategy>();
            foreach (FuzzStrategy s in Enum.GetValues(typeof(FuzzStrategy)))
            {
                if (s != FuzzStrategy.None && s != FuzzStrategy.All && strategies.HasFlag(s))
                    activeStrategies.Add(s);
            }

            if (activeStrategies.Count == 0)
            {
                return new FuzzResult
                {
                    Original = prompt,
                    Variants = Array.Empty<FuzzedPrompt>(),
                    StrategiesApplied = Array.Empty<string>()
                };
            }

            var variants = new List<FuzzedPrompt>();
            var usedTexts = new HashSet<string> { prompt };

            int attempts = 0;
            int maxAttempts = count * 5;

            while (variants.Count < count && attempts < maxAttempts)
            {
                attempts++;
                var strategy = activeStrategies[rng.Next(activeStrategies.Count)];
                var fuzzed = ApplyStrategy(prompt, strategy, rng);

                if (fuzzed != null && !usedTexts.Contains(fuzzed.Text))
                {
                    fuzzed.Similarity = CalculateSimilarity(prompt, fuzzed.Text);
                    variants.Add(fuzzed);
                    usedTexts.Add(fuzzed.Text);
                }
            }

            return new FuzzResult
            {
                Original = prompt,
                Variants = variants,
                StrategiesApplied = activeStrategies.Select(s => s.ToString()).ToList()
            };
        }

        /// <summary>
        /// Generates a single fuzzed variant using a specific strategy.
        /// </summary>
        /// <param name="prompt">The prompt to fuzz.</param>
        /// <param name="strategy">The specific strategy to apply (must be a single strategy, not a combination).</param>
        /// <param name="seed">Optional random seed.</param>
        /// <returns>A single <see cref="FuzzedPrompt"/>, or null if the strategy couldn't be applied.</returns>
        public static FuzzedPrompt? FuzzOne(string prompt, FuzzStrategy strategy, int? seed = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var rng = seed.HasValue ? new Random(seed.Value) : Rng;
            var result = ApplyStrategy(prompt, strategy, rng);
            if (result != null)
                result.Similarity = CalculateSimilarity(prompt, result.Text);
            return result;
        }

        /// <summary>
        /// Returns all available strategy names.
        /// </summary>
        public static IReadOnlyList<string> GetStrategyNames()
        {
            return Enum.GetValues(typeof(FuzzStrategy))
                .Cast<FuzzStrategy>()
                .Where(s => s != FuzzStrategy.None && s != FuzzStrategy.All)
                .Select(s => s.ToString())
                .ToList();
        }

        // ──────────────── Strategy Implementations ────────────────

        private static FuzzedPrompt? ApplyStrategy(string prompt, FuzzStrategy strategy, Random rng)
        {
            return strategy switch
            {
                FuzzStrategy.SynonymSwap => ApplySynonymSwap(prompt, rng),
                FuzzStrategy.TypoInjection => ApplyTypoInjection(prompt, rng),
                FuzzStrategy.CaseChange => ApplyCaseChange(prompt, rng),
                FuzzStrategy.WordDrop => ApplyWordDrop(prompt, rng),
                FuzzStrategy.WordShuffle => ApplyWordShuffle(prompt, rng),
                FuzzStrategy.NoiseInjection => ApplyNoiseInjection(prompt, rng),
                FuzzStrategy.Truncation => ApplyTruncation(prompt, rng),
                _ => null
            };
        }

        private static FuzzedPrompt? ApplySynonymSwap(string prompt, Random rng)
        {
            var words = WordBoundary.Matches(prompt);
            var swappable = new List<Match>();

            foreach (Match m in words)
            {
                if (Synonyms.ContainsKey(m.Value))
                    swappable.Add(m);
            }

            if (swappable.Count == 0)
                return null;

            var target = swappable[rng.Next(swappable.Count)];
            var synonyms = Synonyms[target.Value];
            var replacement = synonyms[rng.Next(synonyms.Length)];

            // Preserve original casing
            if (char.IsUpper(target.Value[0]) && char.IsLower(replacement[0]))
                replacement = char.ToUpper(replacement[0]) + replacement.Substring(1);

            string result = prompt.Substring(0, target.Index) + replacement +
                prompt.Substring(target.Index + target.Length);

            return new FuzzedPrompt
            {
                Text = result,
                Strategy = "SynonymSwap",
                Description = $"Replaced '{target.Value}' with '{replacement}'"
            };
        }

        private static FuzzedPrompt? ApplyTypoInjection(string prompt, Random rng)
        {
            var words = WordBoundary.Matches(prompt);
            var eligible = words.Cast<Match>().Where(m => m.Value.Length >= 3).ToList();

            if (eligible.Count == 0)
                return null;

            var target = eligible[rng.Next(eligible.Count)];
            string word = target.Value;
            string typo;
            string desc;

            int typoType = rng.Next(3);
            switch (typoType)
            {
                case 0: // Transposition
                    int pos = rng.Next(word.Length - 1);
                    char[] chars = word.ToCharArray();
                    (chars[pos], chars[pos + 1]) = (chars[pos + 1], chars[pos]);
                    typo = new string(chars);
                    desc = $"Transposed letters in '{word}' → '{typo}'";
                    break;
                case 1: // Character duplication
                    int dupPos = rng.Next(word.Length);
                    typo = word.Insert(dupPos, word[dupPos].ToString());
                    desc = $"Duplicated letter in '{word}' → '{typo}'";
                    break;
                default: // Character omission
                    int omitPos = rng.Next(1, word.Length); // skip first char
                    typo = word.Remove(omitPos, 1);
                    desc = $"Omitted letter in '{word}' → '{typo}'";
                    break;
            }

            string result = prompt.Substring(0, target.Index) + typo +
                prompt.Substring(target.Index + target.Length);

            return new FuzzedPrompt
            {
                Text = result,
                Strategy = "TypoInjection",
                Description = desc
            };
        }

        private static FuzzedPrompt? ApplyCaseChange(string prompt, Random rng)
        {
            int mode = rng.Next(3);
            string result;
            string desc;

            switch (mode)
            {
                case 0:
                    result = prompt.ToUpperInvariant();
                    desc = "Converted entire prompt to UPPERCASE";
                    break;
                case 1:
                    result = prompt.ToLowerInvariant();
                    desc = "Converted entire prompt to lowercase";
                    break;
                default: // Random word casing
                    result = WordBoundary.Replace(prompt, m =>
                        rng.Next(2) == 0 ? m.Value.ToUpperInvariant() : m.Value.ToLowerInvariant());
                    desc = "Applied random casing to individual words";
                    break;
            }

            if (result == prompt)
                return null;

            return new FuzzedPrompt
            {
                Text = result,
                Strategy = "CaseChange",
                Description = desc
            };
        }

        private static FuzzedPrompt? ApplyWordDrop(string prompt, Random rng)
        {
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 3)
                return null;

            int dropIndex = rng.Next(words.Length);
            string dropped = words[dropIndex];
            var remaining = words.Where((_, i) => i != dropIndex).ToArray();
            string result = string.Join(' ', remaining);

            return new FuzzedPrompt
            {
                Text = result,
                Strategy = "WordDrop",
                Description = $"Dropped word '{dropped}' at position {dropIndex}"
            };
        }

        private static FuzzedPrompt? ApplyWordShuffle(string prompt, Random rng)
        {
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 3)
                return null;

            int i = rng.Next(words.Length - 1);
            (words[i], words[i + 1]) = (words[i + 1], words[i]);
            string result = string.Join(' ', words);

            if (result == prompt)
                return null;

            return new FuzzedPrompt
            {
                Text = result,
                Strategy = "WordShuffle",
                Description = $"Swapped words at positions {i} and {i + 1} ('{words[i + 1]}' ↔ '{words[i]}')"
            };
        }

        private static FuzzedPrompt? ApplyNoiseInjection(string prompt, Random rng)
        {
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (words.Count < 2)
                return null;

            string filler = FillerWords[rng.Next(FillerWords.Length)];
            int insertPos = rng.Next(1, words.Count);
            words.Insert(insertPos, filler);
            string result = string.Join(' ', words);

            return new FuzzedPrompt
            {
                Text = result,
                Strategy = "NoiseInjection",
                Description = $"Inserted filler word '{filler}' at position {insertPos}"
            };
        }

        private static FuzzedPrompt? ApplyTruncation(string prompt, Random rng)
        {
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 4)
                return null;

            // Keep 50–80% of the prompt
            int keepCount = (int)(words.Length * (0.5 + rng.NextDouble() * 0.3));
            keepCount = Math.Max(2, Math.Min(keepCount, words.Length - 1));

            string result = string.Join(' ', words.Take(keepCount));

            return new FuzzedPrompt
            {
                Text = result,
                Strategy = "Truncation",
                Description = $"Truncated to {keepCount}/{words.Length} words ({(keepCount * 100 / words.Length)}%)"
            };
        }

        // ──────────────── Similarity ────────────────

        /// <summary>
        /// Calculates a simple character-level similarity ratio (0.0–1.0)
        /// between two strings using the Sørensen–Dice coefficient on bigrams.
        /// </summary>
        internal static double CalculateSimilarity(string a, string b)
        {
            if (a == b) return 1.0;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

            var bigramsA = GetBigrams(a.ToLowerInvariant());
            var bigramsB = GetBigrams(b.ToLowerInvariant());

            if (bigramsA.Count == 0 && bigramsB.Count == 0) return 1.0;
            if (bigramsA.Count == 0 || bigramsB.Count == 0) return 0.0;

            int intersection = 0;
            var copy = new Dictionary<string, int>(bigramsB);
            foreach (var bigram in bigramsA)
            {
                if (copy.TryGetValue(bigram.Key, out int count) && count > 0)
                {
                    intersection += Math.Min(bigram.Value, count);
                    copy[bigram.Key] = count - Math.Min(bigram.Value, count);
                }
            }

            return (2.0 * intersection) / (bigramsA.Values.Sum() + bigramsB.Values.Sum());
        }

        private static Dictionary<string, int> GetBigrams(string text)
        {
            var bigrams = new Dictionary<string, int>();
            for (int i = 0; i < text.Length - 1; i++)
            {
                string bigram = text.Substring(i, 2);
                bigrams.TryGetValue(bigram, out int count);
                bigrams[bigram] = count + 1;
            }
            return bigrams;
        }
    }
}
