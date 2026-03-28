namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Normalizes prompts by standardizing whitespace, punctuation, casing,
    /// and structural elements for consistent processing and comparison.
    /// Useful for deduplication, caching, and ensuring prompts are clean
    /// before sending to an LLM API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prompt normalization helps with:
    /// - Reducing cache misses from trivially different prompts
    /// - Ensuring consistent formatting before API calls
    /// - Comparing prompts semantically by removing surface-level differences
    /// - Cleaning user-submitted prompts before storage
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var normalizer = new PromptNormalizer()
    ///     .CollapseWhitespace()
    ///     .TrimLines()
    ///     .NormalizeLineEndings()
    ///     .RemoveTrailingPunctuation()
    ///     .LowercaseDirectives();
    ///
    /// string clean = normalizer.Normalize("  You are   a helpful\r\n\r\n\r\nassistant.  ");
    /// // => "You are a helpful\nassistant."
    ///
    /// // Fingerprint for cache keys:
    /// string hash = normalizer.Fingerprint("  Hello   world  ");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptNormalizer
    {
        private readonly List<Func<string, string>> _rules = new();
        private bool _frozen;

        /// <summary>
        /// Collapses runs of spaces/tabs within each line to a single space.
        /// </summary>
        public PromptNormalizer CollapseWhitespace()
        {
            ThrowIfFrozen();
            _rules.Add(text => Regex.Replace(text, @"[ \t]+", " "));
            return this;
        }

        /// <summary>
        /// Trims leading and trailing whitespace from every line.
        /// </summary>
        public PromptNormalizer TrimLines()
        {
            ThrowIfFrozen();
            _rules.Add(text =>
            {
                var lines = text.Split('\n');
                return string.Join("\n", lines.Select(l => l.Trim()));
            });
            return this;
        }

        /// <summary>
        /// Normalizes all line endings to Unix-style \n.
        /// </summary>
        public PromptNormalizer NormalizeLineEndings()
        {
            ThrowIfFrozen();
            _rules.Add(text => text.Replace("\r\n", "\n").Replace("\r", "\n"));
            return this;
        }

        /// <summary>
        /// Collapses 3+ consecutive blank lines into exactly 2.
        /// </summary>
        public PromptNormalizer CollapseBlankLines(int maxConsecutive = 1)
        {
            ThrowIfFrozen();
            var pattern = @"(\n\s*){" + (maxConsecutive + 1) + @",}";
            var replacement = string.Concat(Enumerable.Repeat("\n", maxConsecutive + 1));
            _rules.Add(text => Regex.Replace(text, pattern, replacement));
            return this;
        }

        /// <summary>
        /// Removes trailing punctuation (periods, exclamation marks) from the last line.
        /// Useful for normalizing prompts that may or may not end with punctuation.
        /// </summary>
        public PromptNormalizer RemoveTrailingPunctuation()
        {
            ThrowIfFrozen();
            _rules.Add(text => text.TrimEnd('.', '!'));
            return this;
        }

        /// <summary>
        /// Lowercases common directive prefixes like "You are", "Act as", "Respond",
        /// making them case-insensitive for comparison purposes.
        /// </summary>
        public PromptNormalizer LowercaseDirectives()
        {
            ThrowIfFrozen();
            _rules.Add(text =>
            {
                var directives = new[] { "You are", "Act as", "Respond as", "Behave as", "Pretend you are" };
                foreach (var d in directives)
                {
                    text = Regex.Replace(text, Regex.Escape(d), d.ToLowerInvariant(), RegexOptions.IgnoreCase);
                }
                return text;
            });
            return this;
        }

        /// <summary>
        /// Strips HTML tags from the prompt text.
        /// </summary>
        public PromptNormalizer StripHtml()
        {
            ThrowIfFrozen();
            _rules.Add(text => Regex.Replace(text, @"<[^>]+>", ""));
            return this;
        }

        /// <summary>
        /// Normalizes Unicode quotes, dashes, and ellipses to their ASCII equivalents.
        /// </summary>
        public PromptNormalizer NormalizeUnicode()
        {
            ThrowIfFrozen();
            _rules.Add(text =>
            {
                text = text.Replace('\u2018', '\'').Replace('\u2019', '\'');
                text = text.Replace('\u201C', '"').Replace('\u201D', '"');
                text = text.Replace('\u2013', '-').Replace('\u2014', '-');
                text = text.Replace("\u2026", "...");
                return text;
            });
            return this;
        }

        /// <summary>
        /// Adds a custom normalization rule.
        /// </summary>
        public PromptNormalizer AddRule(Func<string, string> rule)
        {
            ThrowIfFrozen();
            _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
            return this;
        }

        /// <summary>
        /// Freezes the normalizer so no more rules can be added.
        /// Useful for creating shared singleton normalizers.
        /// </summary>
        public PromptNormalizer Freeze()
        {
            _frozen = true;
            return this;
        }

        /// <summary>
        /// Applies all configured normalization rules to the input text.
        /// </summary>
        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            foreach (var rule in _rules)
            {
                text = rule(text);
            }
            return text.Trim();
        }

        /// <summary>
        /// Normalizes the text and returns a SHA-256 fingerprint suitable for cache keys.
        /// Two prompts that differ only in whitespace/formatting will produce the same fingerprint.
        /// </summary>
        public string Fingerprint(string text)
        {
            var normalized = Normalize(text);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Checks if two prompts are equivalent after normalization.
        /// </summary>
        public bool AreEquivalent(string a, string b)
        {
            return Normalize(a) == Normalize(b);
        }

        /// <summary>
        /// Returns a pre-configured normalizer with sensible defaults:
        /// normalize line endings, collapse whitespace, trim lines, collapse blank lines, normalize unicode.
        /// </summary>
        public static PromptNormalizer Default => new PromptNormalizer()
            .NormalizeLineEndings()
            .NormalizeUnicode()
            .CollapseWhitespace()
            .TrimLines()
            .CollapseBlankLines()
            .Freeze();

        /// <summary>
        /// Returns an aggressive normalizer that also lowercases directives and strips HTML.
        /// </summary>
        public static PromptNormalizer Aggressive => new PromptNormalizer()
            .NormalizeLineEndings()
            .NormalizeUnicode()
            .StripHtml()
            .CollapseWhitespace()
            .TrimLines()
            .CollapseBlankLines()
            .LowercaseDirectives()
            .RemoveTrailingPunctuation()
            .Freeze();

        private void ThrowIfFrozen()
        {
            if (_frozen) throw new InvalidOperationException("This normalizer is frozen and cannot be modified.");
        }
    }
}
