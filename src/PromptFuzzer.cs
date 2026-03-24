namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The type of mutation to apply to a prompt.
    /// </summary>
    public enum FuzzMutation
    {
        /// <summary>Introduce random character-level typos.</summary>
        Typo,
        /// <summary>Randomly change the case of words.</summary>
        CaseFlip,
        /// <summary>Drop random words from the prompt.</summary>
        WordDrop,
        /// <summary>Shuffle the order of words in sentences.</summary>
        WordShuffle,
        /// <summary>Truncate the prompt at a random point.</summary>
        Truncate,
        /// <summary>Duplicate random words.</summary>
        WordDuplicate,
        /// <summary>Insert random whitespace or punctuation noise.</summary>
        Noise,
        /// <summary>Swap adjacent words.</summary>
        AdjacentSwap
    }

    /// <summary>
    /// Configuration for a fuzzing run.
    /// </summary>
    public class FuzzOptions
    {
        /// <summary>Number of variants to generate. Default: 10.</summary>
        public int Count { get; set; } = 10;

        /// <summary>
        /// Mutation intensity from 0.0 (no change) to 1.0 (maximum chaos).
        /// Default: 0.3.
        /// </summary>
        public double Intensity { get; set; } = 0.3;

        /// <summary>
        /// Specific mutations to apply. If empty, all mutation types are used.
        /// </summary>
        public List<FuzzMutation> Mutations { get; set; } = new();

        /// <summary>
        /// Random seed for reproducible fuzzing. Null for random.
        /// </summary>
        public int? Seed { get; set; }
    }

    /// <summary>
    /// A single fuzzed variant of a prompt.
    /// </summary>
    public class FuzzVariant
    {
        /// <summary>The mutated prompt text.</summary>
        public string Text { get; internal set; } = "";

        /// <summary>Which mutations were applied.</summary>
        public IReadOnlyList<FuzzMutation> AppliedMutations { get; internal set; }
            = Array.Empty<FuzzMutation>();

        /// <summary>
        /// Edit distance (Levenshtein) from the original prompt.
        /// </summary>
        public int EditDistance { get; internal set; }

        /// <summary>
        /// Similarity to original as a percentage (0–100).
        /// </summary>
        public double SimilarityPercent { get; internal set; }
    }

    /// <summary>
    /// Result of a fuzzing session.
    /// </summary>
    public class FuzzResult
    {
        /// <summary>The original prompt.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>Generated variants.</summary>
        public IReadOnlyList<FuzzVariant> Variants { get; internal set; }
            = Array.Empty<FuzzVariant>();

        /// <summary>Average similarity across all variants (0–100).</summary>
        public double AverageSimilarity { get; internal set; }

        /// <summary>The variant most different from the original.</summary>
        public FuzzVariant? MostDivergent { get; internal set; }
    }

    /// <summary>
    /// Generates random perturbations of prompts to test robustness.
    /// Useful for evaluating whether an LLM produces consistent results
    /// when prompts contain typos, missing words, or formatting changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Quick fuzz — 10 variants at moderate intensity
    /// var variants = PromptFuzzer.Fuzz("Summarize the following article in 3 bullet points.");
    /// foreach (var v in variants.Variants)
    ///     Console.WriteLine($"[{v.SimilarityPercent:F0}%] {v.Text}");
    ///
    /// // Targeted fuzz — only typos and word drops, reproducible
    /// var result = PromptFuzzer.Fuzz("Translate to French:", new FuzzOptions
    /// {
    ///     Count = 5,
    ///     Intensity = 0.2,
    ///     Mutations = { FuzzMutation.Typo, FuzzMutation.WordDrop },
    ///     Seed = 42
    /// });
    /// Console.WriteLine($"Avg similarity: {result.AverageSimilarity:F1}%");
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptFuzzer
    {
        private static readonly char[] TypoChars =
            "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

        private static readonly Regex WordPattern = new(
            @"\S+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly string[] NoiseChars =
            { "  ", "\t", ".", ",", ";", "!", "?", "-", "_", "~" };

        /// <summary>
        /// Generates fuzzed variants of a prompt using default options.
        /// </summary>
        /// <param name="prompt">The prompt to fuzz.</param>
        /// <returns>A <see cref="FuzzResult"/> containing all variants.</returns>
        public static FuzzResult Fuzz(string prompt)
            => Fuzz(prompt, new FuzzOptions());

        /// <summary>
        /// Generates fuzzed variants of a prompt with the specified options.
        /// </summary>
        /// <param name="prompt">The prompt to fuzz.</param>
        /// <param name="options">Fuzzing configuration.</param>
        /// <returns>A <see cref="FuzzResult"/> containing all variants and stats.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prompt"/> is null or empty.
        /// </exception>
        public static FuzzResult Fuzz(string prompt, FuzzOptions options)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));

            var rng = options.Seed.HasValue
                ? new Random(options.Seed.Value)
                : new Random();

            var mutations = options.Mutations.Count > 0
                ? options.Mutations
                : Enum.GetValues<FuzzMutation>().ToList();

            double intensity = Math.Clamp(options.Intensity, 0.0, 1.0);
            var variants = new List<FuzzVariant>();

            for (int i = 0; i < options.Count; i++)
            {
                string mutated = prompt;
                var applied = new List<FuzzMutation>();

                // Pick 1-3 mutations per variant
                int mutationCount = rng.Next(1, Math.Min(4, mutations.Count + 1));
                var chosen = mutations.OrderBy(_ => rng.Next())
                    .Take(mutationCount).ToList();

                foreach (var mutation in chosen)
                {
                    string before = mutated;
                    mutated = ApplyMutation(mutated, mutation, intensity, rng);
                    if (mutated != before)
                        applied.Add(mutation);
                }

                int editDist = LevenshteinDistance(prompt, mutated);
                int maxLen = Math.Max(prompt.Length, mutated.Length);
                double similarity = maxLen > 0
                    ? Math.Round((1.0 - (double)editDist / maxLen) * 100, 1)
                    : 100.0;

                variants.Add(new FuzzVariant
                {
                    Text = mutated,
                    AppliedMutations = applied,
                    EditDistance = editDist,
                    SimilarityPercent = similarity
                });
            }

            double avgSim = variants.Count > 0
                ? Math.Round(variants.Average(v => v.SimilarityPercent), 1)
                : 100.0;

            var mostDivergent = variants
                .OrderBy(v => v.SimilarityPercent)
                .FirstOrDefault();

            return new FuzzResult
            {
                Original = prompt,
                Variants = variants,
                AverageSimilarity = avgSim,
                MostDivergent = mostDivergent
            };
        }

        /// <summary>
        /// Generates a single fuzzed variant with a specific mutation type.
        /// Useful for targeted testing.
        /// </summary>
        public static string FuzzSingle(
            string prompt, FuzzMutation mutation,
            double intensity = 0.3, int? seed = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            return ApplyMutation(prompt, mutation,
                Math.Clamp(intensity, 0.0, 1.0), rng);
        }

        // ── Mutation implementations ───────────────────

        private static string ApplyMutation(
            string text, FuzzMutation mutation, double intensity, Random rng)
        {
            return mutation switch
            {
                FuzzMutation.Typo => ApplyTypos(text, intensity, rng),
                FuzzMutation.CaseFlip => ApplyCaseFlip(text, intensity, rng),
                FuzzMutation.WordDrop => ApplyWordDrop(text, intensity, rng),
                FuzzMutation.WordShuffle => ApplyWordShuffle(text, intensity, rng),
                FuzzMutation.Truncate => ApplyTruncate(text, intensity, rng),
                FuzzMutation.WordDuplicate => ApplyWordDuplicate(text, intensity, rng),
                FuzzMutation.Noise => ApplyNoise(text, intensity, rng),
                FuzzMutation.AdjacentSwap => ApplyAdjacentSwap(text, intensity, rng),
                _ => text
            };
        }

        private static string ApplyTypos(string text, double intensity, Random rng)
        {
            var chars = text.ToCharArray();
            int mutations = Math.Max(1, (int)(chars.Length * intensity * 0.1));
            for (int i = 0; i < mutations; i++)
            {
                int pos = rng.Next(chars.Length);
                if (char.IsLetter(chars[pos]))
                {
                    int op = rng.Next(3);
                    if (op == 0) // substitute
                        chars[pos] = TypoChars[rng.Next(TypoChars.Length)];
                    else if (op == 1 && chars.Length > 1) // delete
                    {
                        var list = chars.ToList();
                        list.RemoveAt(pos);
                        return ApplyTypos(new string(list.ToArray()), 0, rng);
                    }
                    else // duplicate
                        return new string(chars).Insert(pos, chars[pos].ToString());
                }
            }
            return new string(chars);
        }

        private static string ApplyCaseFlip(string text, double intensity, Random rng)
        {
            var words = WordPattern.Matches(text);
            int flips = Math.Max(1, (int)(words.Count * intensity * 0.4));
            var sb = new StringBuilder(text);

            var indices = Enumerable.Range(0, words.Count)
                .OrderBy(_ => rng.Next()).Take(flips);

            foreach (int idx in indices)
            {
                var m = words[idx];
                int op = rng.Next(3);
                string replacement = op switch
                {
                    0 => m.Value.ToUpper(),
                    1 => m.Value.ToLower(),
                    _ => InvertCase(m.Value)
                };
                // Apply in-place (offsets stay valid if same length)
                for (int i = 0; i < m.Length; i++)
                    sb[m.Index + i] = replacement[i];
            }
            return sb.ToString();
        }

        private static string InvertCase(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char c in word)
                sb.Append(char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c));
            return sb.ToString();
        }

        private static string ApplyWordDrop(string text, double intensity, Random rng)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1) return text;

            int drops = Math.Max(1, (int)(words.Length * intensity * 0.3));
            var dropSet = new HashSet<int>(
                Enumerable.Range(0, words.Length)
                    .OrderBy(_ => rng.Next())
                    .Take(Math.Min(drops, words.Length - 1)));

            return string.Join(' ',
                words.Where((_, i) => !dropSet.Contains(i)));
        }

        private static string ApplyWordShuffle(string text, double intensity, Random rng)
        {
            // Split into sentences, shuffle words within each
            var sentences = text.Split(new[] { ". ", "! ", "? " },
                StringSplitOptions.None);
            var result = new List<string>();

            foreach (var sentence in sentences)
            {
                var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int shuffles = Math.Max(1, (int)(words.Length * intensity * 0.3));
                for (int i = 0; i < shuffles; i++)
                {
                    int a = rng.Next(words.Length);
                    int b = rng.Next(words.Length);
                    (words[a], words[b]) = (words[b], words[a]);
                }
                result.Add(string.Join(' ', words));
            }
            return string.Join(". ", result);
        }

        private static string ApplyTruncate(string text, double intensity, Random rng)
        {
            // Keep between 50% and (100% - intensity*40%) of the text
            double keepRatio = 1.0 - (intensity * (0.2 + rng.NextDouble() * 0.2));
            int keepLen = Math.Max(1, (int)(text.Length * keepRatio));
            return text.Substring(0, keepLen);
        }

        private static string ApplyWordDuplicate(string text, double intensity, Random rng)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (words.Count == 0) return text;

            int dups = Math.Max(1, (int)(words.Count * intensity * 0.2));
            for (int i = 0; i < dups; i++)
            {
                int pos = rng.Next(words.Count);
                words.Insert(pos + 1, words[pos]);
            }
            return string.Join(' ', words);
        }

        private static string ApplyNoise(string text, double intensity, Random rng)
        {
            var sb = new StringBuilder(text);
            int insertions = Math.Max(1, (int)(text.Length * intensity * 0.05));
            for (int i = 0; i < insertions; i++)
            {
                int pos = rng.Next(sb.Length);
                string noise = NoiseChars[rng.Next(NoiseChars.Length)];
                sb.Insert(pos, noise);
            }
            return sb.ToString();
        }

        private static string ApplyAdjacentSwap(string text, double intensity, Random rng)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1) return text;

            int swaps = Math.Max(1, (int)(words.Length * intensity * 0.2));
            for (int i = 0; i < swaps; i++)
            {
                int pos = rng.Next(words.Length - 1);
                (words[pos], words[pos + 1]) = (words[pos + 1], words[pos]);
            }
            return string.Join(' ', words);
        }

        // ── Levenshtein distance ───────────────────

        private static int LevenshteinDistance(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            // Use two-row optimization for memory efficiency
            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];

            for (int j = 0; j <= b.Length; j++)
                prev[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[b.Length];
        }
    }
}
