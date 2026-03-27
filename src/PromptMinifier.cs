namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Controls how aggressively the minifier compresses prompt text.
    /// </summary>
    public enum MinifyLevel
    {
        /// <summary>Light: collapse repeated blank lines, trim trailing whitespace.</summary>
        Light,
        /// <summary>Medium: Light + strip markdown comments, normalise whitespace runs.</summary>
        Medium,
        /// <summary>Aggressive: Medium + remove decorative punctuation, shorten separators, collapse lists.</summary>
        Aggressive
    }

    /// <summary>
    /// Configuration options for the prompt minifier.
    /// </summary>
    public class MinifyOptions
    {
        /// <summary>Compression level.</summary>
        [JsonPropertyName("level")]
        public MinifyLevel Level { get; init; } = MinifyLevel.Medium;

        /// <summary>Strip HTML-style comments (&lt;!-- … --&gt;).</summary>
        [JsonPropertyName("stripHtmlComments")]
        public bool StripHtmlComments { get; init; } = true;

        /// <summary>Strip lines that are only markdown horizontal rules (---, ***, ___).</summary>
        [JsonPropertyName("stripHorizontalRules")]
        public bool StripHorizontalRules { get; init; } = true;

        /// <summary>Collapse runs of 3+ blank lines down to a single blank line.</summary>
        [JsonPropertyName("collapseBlankLines")]
        public bool CollapseBlankLines { get; init; } = true;

        /// <summary>Normalise bullet markers to a single dash (-).</summary>
        [JsonPropertyName("normaliseBullets")]
        public bool NormaliseBullets { get; init; } = false;

        /// <summary>Remove trailing whitespace on every line.</summary>
        [JsonPropertyName("trimTrailing")]
        public bool TrimTrailing { get; init; } = true;

        /// <summary>Remove leading blank lines from the prompt.</summary>
        [JsonPropertyName("trimLeadingBlanks")]
        public bool TrimLeadingBlanks { get; init; } = true;

        /// <summary>Remove trailing blank lines from the prompt.</summary>
        [JsonPropertyName("trimTrailingBlanks")]
        public bool TrimTrailingBlanks { get; init; } = true;

        /// <summary>Preserve code fences (``` blocks) from minification.</summary>
        [JsonPropertyName("preserveCodeBlocks")]
        public bool PreserveCodeBlocks { get; init; } = true;

        /// <summary>Maximum consecutive blank lines allowed (0 = remove all).</summary>
        [JsonPropertyName("maxConsecutiveBlanks")]
        public int MaxConsecutiveBlanks { get; init; } = 1;
    }

    /// <summary>
    /// Result of a minification operation, including before/after metrics.
    /// </summary>
    public class MinifyResult
    {
        /// <summary>The minified prompt text.</summary>
        [JsonPropertyName("text")]
        public string Text { get; init; } = "";

        /// <summary>Original character count.</summary>
        [JsonPropertyName("originalChars")]
        public int OriginalChars { get; init; }

        /// <summary>Minified character count.</summary>
        [JsonPropertyName("minifiedChars")]
        public int MinifiedChars { get; init; }

        /// <summary>Original line count.</summary>
        [JsonPropertyName("originalLines")]
        public int OriginalLines { get; init; }

        /// <summary>Minified line count.</summary>
        [JsonPropertyName("minifiedLines")]
        public int MinifiedLines { get; init; }

        /// <summary>Percentage of characters saved (0-100).</summary>
        [JsonPropertyName("savingsPercent")]
        public double SavingsPercent { get; init; }

        /// <summary>Estimated token reduction (rough: chars / 4).</summary>
        [JsonPropertyName("estimatedTokensSaved")]
        public int EstimatedTokensSaved { get; init; }
    }

    /// <summary>
    /// Minifies prompt text by removing unnecessary whitespace, comments, and decorative
    /// formatting to reduce token usage while preserving semantic meaning.
    /// </summary>
    /// <example>
    /// <code>
    /// var minifier = new PromptMinifier();
    /// var result = minifier.Minify(longPrompt);
    /// Console.WriteLine($"Saved {result.SavingsPercent:F1}% ({result.EstimatedTokensSaved} tokens)");
    /// Console.WriteLine(result.Text);
    ///
    /// // With aggressive settings:
    /// var result2 = minifier.Minify(longPrompt, new MinifyOptions { Level = MinifyLevel.Aggressive });
    /// </code>
    /// </example>
    public class PromptMinifier
    {
        private static readonly Regex HtmlCommentPattern = new(@"<!--[\s\S]*?-->", RegexOptions.Compiled);
        private static readonly Regex HorizontalRulePattern = new(@"^[\s]*([-*_])\s*\1\s*\1[\s\-\*_]*$", RegexOptions.Compiled);
        private static readonly Regex MultipleBlankLines = new(@"(\r?\n){3,}", RegexOptions.Compiled);
        private static readonly Regex TrailingWhitespace = new(@"[ \t]+$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex BulletPattern = new(@"^(\s*)[*+]\s", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex CodeFencePattern = new(@"^```", RegexOptions.Compiled);
        private static readonly Regex DecorativePunctuation = new(@"([!?.])\1{2,}", RegexOptions.Compiled);
        private static readonly Regex MultipleSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled);
        private static readonly Regex EmptyHeading = new(@"^(#{1,6})\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Minifies the given prompt text with default options (Medium level).
        /// </summary>
        public MinifyResult Minify(string prompt) => Minify(prompt, new MinifyOptions());

        /// <summary>
        /// Minifies the given prompt text with the specified options.
        /// </summary>
        public MinifyResult Minify(string prompt, MinifyOptions options)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return new MinifyResult
                {
                    Text = prompt ?? "",
                    OriginalChars = 0,
                    MinifiedChars = 0,
                    OriginalLines = 0,
                    MinifiedLines = 0,
                    SavingsPercent = 0,
                    EstimatedTokensSaved = 0
                };
            }

            int originalChars = prompt.Length;
            int originalLines = prompt.Split('\n').Length;

            string result = prompt;

            if (options.PreserveCodeBlocks)
            {
                result = MinifyWithCodeBlockPreservation(result, options);
            }
            else
            {
                result = ApplyMinification(result, options);
            }

            int minifiedChars = result.Length;
            int minifiedLines = result.Split('\n').Length;
            int charsSaved = originalChars - minifiedChars;

            return new MinifyResult
            {
                Text = result,
                OriginalChars = originalChars,
                MinifiedChars = minifiedChars,
                OriginalLines = originalLines,
                MinifiedLines = minifiedLines,
                SavingsPercent = originalChars > 0 ? Math.Round((double)charsSaved / originalChars * 100, 1) : 0,
                EstimatedTokensSaved = charsSaved / 4
            };
        }

        /// <summary>
        /// Quick convenience method: minify and return just the text.
        /// </summary>
        public string MinifyText(string prompt, MinifyLevel level = MinifyLevel.Medium)
        {
            return Minify(prompt, new MinifyOptions { Level = level }).Text;
        }

        /// <summary>
        /// Estimates how many tokens would be saved by minification without performing it.
        /// </summary>
        public int EstimateSavings(string prompt, MinifyLevel level = MinifyLevel.Medium)
        {
            var result = Minify(prompt, new MinifyOptions { Level = level });
            return result.EstimatedTokensSaved;
        }

        /// <summary>
        /// Returns a JSON report of the minification analysis.
        /// </summary>
        public string Report(string prompt, MinifyOptions? options = null)
        {
            var result = Minify(prompt, options ?? new MinifyOptions());
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }

        private string MinifyWithCodeBlockPreservation(string text, MinifyOptions options)
        {
            var lines = text.Split('\n');
            var segments = new List<(string content, bool isCode)>();
            var current = new List<string>();
            bool inCode = false;

            foreach (var line in lines)
            {
                if (CodeFencePattern.IsMatch(line.TrimEnd()))
                {
                    if (inCode)
                    {
                        // End of code block - save code as-is
                        current.Add(line);
                        segments.Add((string.Join("\n", current), true));
                        current = new List<string>();
                        inCode = false;
                    }
                    else
                    {
                        // Start of code block - minify what we have so far
                        if (current.Count > 0)
                        {
                            segments.Add((string.Join("\n", current), false));
                            current = new List<string>();
                        }
                        current.Add(line);
                        inCode = true;
                    }
                }
                else
                {
                    current.Add(line);
                }
            }

            if (current.Count > 0)
            {
                segments.Add((string.Join("\n", current), inCode));
            }

            var processed = segments.Select(s => s.isCode ? s.content : ApplyMinification(s.content, options));
            return string.Join("\n", processed);
        }

        private string ApplyMinification(string text, MinifyOptions options)
        {
            var result = text;

            // Strip HTML comments
            if (options.StripHtmlComments)
            {
                result = HtmlCommentPattern.Replace(result, "");
            }

            // Strip horizontal rules
            if (options.StripHorizontalRules)
            {
                result = string.Join("\n",
                    result.Split('\n').Where(l => !HorizontalRulePattern.IsMatch(l)));
            }

            // Trim trailing whitespace per line
            if (options.TrimTrailing)
            {
                result = TrailingWhitespace.Replace(result, "");
            }

            // Normalise bullets
            if (options.NormaliseBullets || options.Level >= MinifyLevel.Aggressive)
            {
                result = BulletPattern.Replace(result, "$1- ");
            }

            // Medium+: remove empty headings
            if (options.Level >= MinifyLevel.Medium)
            {
                result = EmptyHeading.Replace(result, "");
            }

            // Aggressive: collapse decorative punctuation
            if (options.Level >= MinifyLevel.Aggressive)
            {
                result = DecorativePunctuation.Replace(result, "$1");
                // Collapse multiple inline spaces to one
                result = MultipleSpaces.Replace(result, " ");
            }

            // Collapse blank lines
            if (options.CollapseBlankLines)
            {
                int max = Math.Max(0, options.MaxConsecutiveBlanks);
                string replacement = string.Concat(Enumerable.Repeat("\n", max + 1));
                // Keep collapsing until stable
                string pattern = @"(\r?\n){" + (max + 2) + ",}";
                result = Regex.Replace(result, pattern, replacement);
            }

            // Trim leading/trailing blank lines
            if (options.TrimLeadingBlanks)
            {
                result = result.TrimStart('\r', '\n');
            }
            if (options.TrimTrailingBlanks)
            {
                result = result.TrimEnd('\r', '\n', ' ', '\t');
            }

            return result;
        }
    }
}
