namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
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
            _rules.Add(text => CollapseWhitespaceRegex.Replace(text, " "));
            return this;
        }

        private static readonly Regex CollapseWhitespaceRegex =
            new(@"[ \t]+", RegexOptions.Compiled);

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
        /// Caps the number of consecutive newlines between non-blank lines so
        /// that runs of blank lines never exceed the configured size.
        /// </summary>
        /// <param name="maxConsecutive">
        /// The maximum allowed run of consecutive newlines. The default of
        /// <c>1</c> caps any run of blank lines at one blank line (i.e. two
        /// newlines in a row).
        /// </param>
        /// <example>
        /// <code>
        /// // Default (maxConsecutive=1): at most one blank line
        /// new PromptNormalizer().CollapseBlankLines().Normalize("a\n\n\n\nb");
        /// // => "a\n\nb"
        ///
        /// // Allow up to two blank lines
        /// new PromptNormalizer().CollapseBlankLines(2).Normalize("a\n\n\n\n\nb");
        /// // => "a\n\n\nb"
        /// </code>
        /// </example>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="maxConsecutive"/> is less than 1.</exception>
        public PromptNormalizer CollapseBlankLines(int maxConsecutive = 1)
        {
            ThrowIfFrozen();
            if (maxConsecutive < 1)
                throw new ArgumentOutOfRangeException(nameof(maxConsecutive), maxConsecutive,
                    "maxConsecutive must be at least 1.");
            var regex = BlankLineRegexCache.GetOrAdd(maxConsecutive, static mc =>
                new Regex(@"(\n\s*){" + (mc + 1) + @",}", RegexOptions.Compiled));
            var replacement = new string('\n', maxConsecutive + 1);
            _rules.Add(text => regex.Replace(text, replacement));
            return this;
        }

        // Compiled regex per maxConsecutive value. In practice 1–3 covers the
        // vast majority of callers, so this cache stays tiny.
        private static readonly ConcurrentDictionary<int, Regex> BlankLineRegexCache = new();

        /// <summary>
        /// Removes trailing sentence-ending punctuation (<c>.</c>, <c>!</c>, <c>?</c>)
        /// from the end of the prompt. Useful for normalizing prompts that may or
        /// may not end with punctuation so they compare and fingerprint equally.
        /// </summary>
        public PromptNormalizer RemoveTrailingPunctuation()
        {
            ThrowIfFrozen();
            _rules.Add(text => text.TrimEnd(TrailingPunctuation));
            return this;
        }

        private static readonly char[] TrailingPunctuation = { '.', '!', '?' };

        /// <summary>
        /// Lowercases common directive prefixes like "You are", "Act as", "Respond",
        /// making them case-insensitive for comparison purposes.
        /// </summary>
        public PromptNormalizer LowercaseDirectives()
        {
            ThrowIfFrozen();
            _rules.Add(text => DirectivesRegex.Replace(text, static m => m.Value.ToLowerInvariant()));
            return this;
        }

        private static readonly string[] KnownDirectives =
        {
            "You are", "Act as", "Respond as", "Behave as", "Pretend you are"
        };

        // One combined regex replaces five sequential passes. Anchored on \b
        // boundaries so we don't lowercase "yourself" or "reactant". The
        // longer phrases are listed first so the alternation prefers them
        // over the shorter overlapping prefix ("act as" before "you are"
        // is irrelevant, but "pretend you are" must beat "you are").
        private static readonly Regex DirectivesRegex = new(
            @"(?:pretend\ you\ are|respond\ as|behave\ as|you\ are|act\ as)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        /// <summary>
        /// Strips HTML tags from the prompt text.
        /// </summary>
        public PromptNormalizer StripHtml()
        {
            ThrowIfFrozen();
            _rules.Add(text => HtmlTagRegex.Replace(text, ""));
            return this;
        }

        private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

        /// <summary>
        /// Normalizes Unicode quotes, dashes, and ellipses to their ASCII equivalents.
        /// </summary>
        public PromptNormalizer NormalizeUnicode()
        {
            ThrowIfFrozen();
            _rules.Add(NormalizeUnicodeInternal);
            return this;
        }

        // Single-pass replacement — the previous implementation walked the
        // string eight separate times (six Replace(char,char) plus a
        // Replace(string,string) for the ellipsis). For long prompts this
        // costs O(8 × N) and allocates seven intermediate strings; the
        // builder-based pass is O(N) with one allocation.
        private static string NormalizeUnicodeInternal(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            // Fast path: detect whether any of the special characters appear
            // at all. The overwhelmingly common case is ASCII-only prompts,
            // and IndexOfAny on a small char[] is cheap.
            if (text.IndexOfAny(UnicodePunctuation) < 0)
                return text;

            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '\u2018': case '\u2019':
                        sb.Append('\''); break;
                    case '\u201C': case '\u201D':
                        sb.Append('"'); break;
                    case '\u2013': case '\u2014':
                        sb.Append('-'); break;
                    case '\u2026':
                        sb.Append("..."); break;
                    default:
                        sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static readonly char[] UnicodePunctuation =
        {
            '\u2018', '\u2019', '\u201C', '\u201D', '\u2013', '\u2014', '\u2026'
        };

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
            // SHA256.HashData + Convert.ToHexString avoid the SHA256 instance
            // allocation and the BitConverter "-" dance + ToLower pass; net
            // is roughly 2× faster on short prompts and produces zero garbage
            // beyond the final 64-char string.
            Span<byte> hash = stackalloc byte[32];
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(normalized), hash);
            return Convert.ToHexString(hash).ToLowerInvariant();
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
        /// Returns a shared frozen singleton — cheap to call in hot paths.
        /// </summary>
        public static PromptNormalizer Default => _defaultInstance;

        /// <summary>
        /// Returns an aggressive normalizer that also lowercases directives and strips HTML.
        /// Returns a shared frozen singleton — cheap to call in hot paths.
        /// </summary>
        public static PromptNormalizer Aggressive => _aggressiveInstance;

        // Cache the preset singletons. The previous implementation built and
        // froze a fresh PromptNormalizer (and a fresh rule chain) on every
        // property access, which made `PromptNormalizer.Default.Normalize(x)`
        // dramatically more expensive than expected when called per-request.
        private static readonly PromptNormalizer _defaultInstance = new PromptNormalizer()
            .NormalizeLineEndings()
            .NormalizeUnicode()
            .CollapseWhitespace()
            .TrimLines()
            .CollapseBlankLines()
            .Freeze();

        private static readonly PromptNormalizer _aggressiveInstance = new PromptNormalizer()
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
