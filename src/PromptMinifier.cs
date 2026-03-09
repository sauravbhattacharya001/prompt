namespace Prompt
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Result of minifying a prompt with <see cref="PromptMinifier"/>.
    /// </summary>
    public class MinifyResult
    {
        /// <summary>Gets the original prompt text.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>Gets the minified prompt text.</summary>
        public string Minified { get; internal set; } = "";

        /// <summary>Gets the estimated token count of the original prompt.</summary>
        public int OriginalTokens { get; internal set; }

        /// <summary>Gets the estimated token count of the minified prompt.</summary>
        public int MinifiedTokens { get; internal set; }

        /// <summary>Gets the number of tokens saved.</summary>
        public int TokensSaved => OriginalTokens - MinifiedTokens;

        /// <summary>Gets the percentage of tokens saved (0–100).</summary>
        public double SavingsPercent => OriginalTokens > 0
            ? Math.Round((double)TokensSaved / OriginalTokens * 100, 1)
            : 0;

        /// <summary>Gets the list of transformations applied.</summary>
        public IReadOnlyList<string> Transformations { get; internal set; }
            = Array.Empty<string>();
    }

    /// <summary>
    /// Minification level controlling how aggressively prompts are compressed.
    /// </summary>
    public enum MinifyLevel
    {
        /// <summary>
        /// Light: remove filler words, normalize whitespace, compress
        /// obvious redundancy. Safe for all prompts.
        /// </summary>
        Light,

        /// <summary>
        /// Medium: all Light transforms plus abbreviate common phrases,
        /// remove politeness markers, condense instructions.
        /// </summary>
        Medium,

        /// <summary>
        /// Aggressive: all Medium transforms plus telegraph-style
        /// compression, remove articles/prepositions where safe,
        /// shorten common words.
        /// </summary>
        Aggressive
    }

    /// <summary>
    /// Compresses prompts to reduce token usage while preserving semantic
    /// meaning. Useful for staying within context windows or reducing API
    /// costs without changing what you're asking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Quick minify
    /// string compact = PromptMinifier.Minify(
    ///     "Please help me to write a detailed summary of the following text");
    /// // → "Write detailed summary of following text"
    ///
    /// // With stats
    /// var result = PromptMinifier.MinifyWithStats(longPrompt, MinifyLevel.Medium);
    /// Console.WriteLine($"Saved {result.TokensSaved} tokens ({result.SavingsPercent}%)");
    /// Console.WriteLine(result.Minified);
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptMinifier
    {
        // ── Pre-compiled cleanup patterns ──────────────────

        private static readonly Regex MultiSpaceCleanup = new(
            @" {2,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex LeadingSpaceCleanup = new(
            @"(?m)^ +", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        // ──────────────── Filler Patterns (Light) ────────────────

        private static readonly (Regex Pattern, string Replacement, string Description)[] LightRules =
        {
            // Filler phrases
            (new Regex(@"\b(please\s+)?(help\s+me\s+to|help\s+me)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove 'help me to'"),

            (new Regex(@"\b(I\s+would\s+like\s+(you\s+to|to)|I\s+want\s+you\s+to|I\s+need\s+you\s+to)\s+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove 'I would like you to'"),

            (new Regex(@"\b(could\s+you\s+(please\s+)?|can\s+you\s+(please\s+)?|would\s+you\s+(please\s+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove 'could you please'"),

            (new Regex(@"\bplease\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove 'please'"),

            (new Regex(@"\b(basically|essentially|actually|obviously|clearly|simply|just|really|very|quite|rather)\s+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove hedge/filler words"),

            // Redundant phrases
            (new Regex(@"\bin\s+order\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "to", "Simplify 'in order to' → 'to'"),

            (new Regex(@"\bdue\s+to\s+the\s+fact\s+that\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "because", "Simplify 'due to the fact that' → 'because'"),

            (new Regex(@"\bat\s+this\s+point\s+in\s+time\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "now", "Simplify 'at this point in time' → 'now'"),

            (new Regex(@"\bin\s+the\s+event\s+that\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "if", "Simplify 'in the event that' → 'if'"),

            (new Regex(@"\bfor\s+the\s+purpose\s+of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "for", "Simplify 'for the purpose of' → 'for'"),

            (new Regex(@"\bwith\s+regard\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "about", "Simplify 'with regard to' → 'about'"),

            (new Regex(@"\bin\s+terms\s+of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "regarding", "Simplify 'in terms of' → 'regarding'"),

            (new Regex(@"\bhas\s+the\s+ability\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "can", "Simplify 'has the ability to' → 'can'"),

            (new Regex(@"\bit\s+is\s+important\s+to\s+note\s+that\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "Note:", "Simplify 'it is important to note that' → 'Note:'"),

            // Whitespace normalization
            (new Regex(@"[ \t]{2,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                " ", "Normalize multiple spaces"),

            (new Regex(@"\n{3,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "\n\n", "Normalize multiple blank lines"),
        };

        // ──────────────── Medium Patterns ────────────────

        private static readonly (Regex Pattern, string Replacement, string Description)[] MediumRules =
        {
            // Politeness
            (new Regex(@"\b(thank\s+you|thanks)\s*(in\s+advance|for\s+your\s+help|so\s+much)?[.!]?\s*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove thank-you phrases"),

            (new Regex(@"\bI\s+appreciate\s+(your|any)\s+\w+[.!]?\s*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove appreciation phrases"),

            // Verbose instruction patterns
            (new Regex(@"\bmake\s+sure\s+(to|that)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "ensure ", "Condense 'make sure to' → 'ensure'"),

            (new Regex(@"\btake\s+into\s+(account|consideration)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "consider", "Condense 'take into account' → 'consider'"),

            (new Regex(@"\bprovide\s+(me\s+with|an?\s+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "give ", "Condense 'provide me with' → 'give'"),

            (new Regex(@"\bin\s+a\s+way\s+that\s+is\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "that is", "Condense 'in a way that is'"),

            (new Regex(@"\b(it\s+is|it's)\s+(important|necessary|essential|crucial|critical)\s+(to|that)\s+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "Must ", "Condense 'it is important to' → 'Must'"),

            (new Regex(@"\bas\s+much\s+as\s+possible\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "maximally", "Condense 'as much as possible' → 'maximally'"),

            (new Regex(@"\ba\s+large\s+number\s+of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "many", "Condense 'a large number of' → 'many'"),

            (new Regex(@"\ba\s+small\s+number\s+of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "few", "Condense 'a small number of' → 'few'"),
        };

        // ──────────────── Aggressive Patterns ────────────────

        private static readonly (Regex Pattern, string Replacement, string Description)[] AggressiveRules =
        {
            // Remove articles where meaning is preserved
            (new Regex(@"\b(the|a|an)\s+(?=\w)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "", "Remove articles"),

            // Shorten common words
            (new Regex(@"\binformation\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "info", "Shorten 'information' → 'info'"),

            (new Regex(@"\bapplication\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "app", "Shorten 'application' → 'app'"),

            (new Regex(@"\bconfiguration\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "config", "Shorten 'configuration' → 'config'"),

            (new Regex(@"\bdocumentation\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "docs", "Shorten 'documentation' → 'docs'"),

            (new Regex(@"\bimplementation\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "impl", "Shorten 'implementation' → 'impl'"),

            (new Regex(@"\benvironment\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "env", "Shorten 'environment' → 'env'"),

            (new Regex(@"\brepository\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "repo", "Shorten 'repository' → 'repo'"),

            (new Regex(@"\bfunction(ality)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "func", "Shorten 'functionality' → 'func'"),

            (new Regex(@"\bfor\s+example\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "e.g.", "Shorten 'for example' → 'e.g.'"),

            (new Regex(@"\bthat\s+is\s+to\s+say\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "i.e.", "Shorten 'that is to say' → 'i.e.'"),

            // Remove filler prepositions in lists
            (new Regex(@"\b(and|or)\s+also\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
                "and", "Remove redundant 'also' after conjunctions"),
        };

        /// <summary>
        /// Minifies a prompt, returning the compressed text.
        /// </summary>
        /// <param name="prompt">The prompt to minify.</param>
        /// <param name="level">
        /// How aggressively to compress. Default: <see cref="MinifyLevel.Medium"/>.
        /// </param>
        /// <returns>The minified prompt string.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prompt"/> is null or empty.
        /// </exception>
        public static string Minify(string prompt, MinifyLevel level = MinifyLevel.Medium)
        {
            return MinifyWithStats(prompt, level).Minified;
        }

        /// <summary>
        /// Minifies a prompt and returns detailed statistics about the
        /// compression including token savings and transformations applied.
        /// </summary>
        /// <param name="prompt">The prompt to minify.</param>
        /// <param name="level">
        /// How aggressively to compress. Default: <see cref="MinifyLevel.Medium"/>.
        /// </param>
        /// <returns>
        /// A <see cref="MinifyResult"/> with the minified text, token counts,
        /// savings, and list of transformations applied.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prompt"/> is null or empty.
        /// </exception>
        public static MinifyResult MinifyWithStats(
            string prompt, MinifyLevel level = MinifyLevel.Medium)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));

            int originalTokens = PromptGuard.EstimateTokens(prompt);
            var transformations = new List<string>();
            string result = prompt;

            // Always apply Light rules
            result = ApplyRules(result, LightRules, transformations);

            // Medium and above
            if (level >= MinifyLevel.Medium)
                result = ApplyRules(result, MediumRules, transformations);

            // Aggressive
            if (level >= MinifyLevel.Aggressive)
                result = ApplyRules(result, AggressiveRules, transformations);

            // Final cleanup
            result = result.Trim();
            // Fix double spaces left by removals
            result = MultiSpaceCleanup.Replace(result, " ");
            // Fix leading spaces on lines
            result = LeadingSpaceCleanup.Replace(result, "");
            // Capitalize first letter if it got lowercased
            if (result.Length > 0 && char.IsLower(result[0]))
                result = char.ToUpper(result[0]) + result.Substring(1);

            int minifiedTokens = PromptGuard.EstimateTokens(result);

            return new MinifyResult
            {
                Original = prompt,
                Minified = result,
                OriginalTokens = originalTokens,
                MinifiedTokens = minifiedTokens,
                Transformations = transformations
            };
        }

        /// <summary>
        /// Applies a set of regex rules to the text, tracking which
        /// transformations actually matched.
        /// </summary>
        private static string ApplyRules(
            string text,
            (Regex Pattern, string Replacement, string Description)[] rules,
            List<string> transformations)
        {
            foreach (var (pattern, replacement, description) in rules)
            {
                string before = text;
                text = pattern.Replace(text, replacement);
                if (text != before)
                    transformations.Add(description);
            }
            return text;
        }
    }
}
