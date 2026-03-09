using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Prompt
{
    /// <summary>
    /// Result of sanitizing a prompt through <see cref="PromptSanitizer"/>.
    /// </summary>
    public class SanitizeResult
    {
        /// <summary>Gets the original input text.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>Gets the sanitized output text.</summary>
        public string Sanitized { get; internal set; } = "";

        /// <summary>Gets the list of transformations that were applied.</summary>
        public List<SanitizeAction> Actions { get; internal set; } = new();

        /// <summary>Gets whether any changes were made.</summary>
        public bool WasModified => Actions.Count > 0;

        /// <summary>Gets detected PII types that were redacted (if PII redaction was enabled).</summary>
        public List<string> RedactedPiiTypes { get; internal set; } = new();

        /// <summary>Gets the number of injection patterns that were neutralized.</summary>
        public int InjectionPatternsNeutralized { get; internal set; }
    }

    /// <summary>
    /// Describes a single sanitization action that was applied.
    /// </summary>
    public class SanitizeAction
    {
        /// <summary>Gets the type of action.</summary>
        public string Type { get; internal set; } = "";

        /// <summary>Gets a human-readable description of what was done.</summary>
        public string Description { get; internal set; } = "";
    }

    /// <summary>
    /// Configuration options for <see cref="PromptSanitizer"/>.
    /// </summary>
    public class SanitizeOptions
    {
        /// <summary>Normalize excessive whitespace (collapse runs, trim lines). Default: true.</summary>
        public bool NormalizeWhitespace { get; set; } = true;

        /// <summary>Remove or neutralize common prompt injection patterns. Default: true.</summary>
        public bool NeutralizeInjections { get; set; } = true;

        /// <summary>Redact detected PII (emails, phone numbers, SSNs, credit cards). Default: false.</summary>
        public bool RedactPii { get; set; }

        /// <summary>The placeholder used for redacted PII. Default: "[REDACTED]".</summary>
        public string PiiPlaceholder { get; set; } = "[REDACTED]";

        /// <summary>Strip invisible/zero-width Unicode characters. Default: true.</summary>
        public bool StripInvisibleChars { get; set; } = true;

        /// <summary>Escape special tokens (e.g., &lt;|endoftext|&gt;). Default: true.</summary>
        public bool EscapeSpecialTokens { get; set; } = true;

        /// <summary>Maximum allowed length in characters. 0 = no limit. Default: 0.</summary>
        public int MaxLength { get; set; }

        /// <summary>Remove duplicate blank lines. Default: true.</summary>
        public bool CollapseBlankLines { get; set; } = true;

        /// <summary>Trim leading/trailing whitespace from the entire prompt. Default: true.</summary>
        public bool TrimEnds { get; set; } = true;
    }

    /// <summary>
    /// Cleans and normalizes prompt text before sending to an LLM.
    /// Handles whitespace normalization, PII redaction, injection neutralization,
    /// invisible character stripping, and special token escaping.
    /// </summary>
    /// <example>
    /// <code>
    /// var sanitizer = new PromptSanitizer();
    /// var result = sanitizer.Sanitize("  Hello   world!  \n\n\n  Tell me more.  ");
    /// // result.Sanitized == "Hello world!\n\nTell me more."
    ///
    /// // With PII redaction:
    /// var opts = new SanitizeOptions { RedactPii = true };
    /// var r2 = sanitizer.Sanitize("Email me at john@example.com", opts);
    /// // r2.Sanitized == "Email me at [REDACTED]"
    /// </code>
    /// </example>
    public class PromptSanitizer
    {
        private static readonly Regex MultipleSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        private static readonly Regex MultipleBlankLines = new(@"(\r?\n){3,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        private static readonly Regex TrailingLineSpaces = new(@"[ \t]+(?=\r?\n|$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        private static readonly Regex LeadingLineSpaces = new(@"(?<=\r?\n)[ \t]+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        // PII patterns
        private static readonly Regex EmailPattern = new(
            @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        private static readonly Regex PhonePattern = new(
            @"(?<!\d)(\+?1[-.\s]?)?(\(?\d{3}\)?[-.\s]?)?\d{3}[-.\s]?\d{4}(?!\d)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        private static readonly Regex SsnPattern = new(
            @"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        private static readonly Regex CreditCardPattern = new(
            @"\b(?:\d[ -]*?){13,16}\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        private static readonly Regex IpAddressPattern = new(
            @"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        // Injection patterns — phrases commonly used to override system prompts
        private static readonly string[] InjectionPhrases = new[]
        {
            "ignore previous instructions",
            "ignore all previous",
            "disregard above",
            "disregard previous",
            "forget your instructions",
            "forget all previous",
            "ignore your system prompt",
            "override your instructions",
            "new instructions:",
            "system prompt override",
            "you are now",
            "pretend you are",
            "act as if you have no restrictions",
            "jailbreak",
            "DAN mode",
            "developer mode enabled",
        };

        // Special tokens used by various LLM tokenizers
        private static readonly string[] SpecialTokens = new[]
        {
            "<|endoftext|>",
            "<|im_start|>",
            "<|im_end|>",
            "<|system|>",
            "<|user|>",
            "<|assistant|>",
            "<|pad|>",
            "<s>",
            "</s>",
            "[INST]",
            "[/INST]",
            "<<SYS>>",
            "<</SYS>>",
        };

        // Zero-width and invisible Unicode characters
        private static readonly Regex InvisibleChars = new(
            @"[\u200B\u200C\u200D\u200E\u200F\u202A-\u202E\u2060\u2061\u2062\u2063\u2064\uFEFF\u00AD\u034F\u061C\u180E]",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Sanitize a prompt using default options.
        /// </summary>
        /// <param name="prompt">The prompt text to sanitize.</param>
        /// <returns>A <see cref="SanitizeResult"/> with the cleaned text and action log.</returns>
        public SanitizeResult Sanitize(string prompt)
            => Sanitize(prompt, new SanitizeOptions());

        /// <summary>
        /// Sanitize a prompt using the specified options.
        /// </summary>
        /// <param name="prompt">The prompt text to sanitize.</param>
        /// <param name="options">Configuration for which sanitization steps to apply.</param>
        /// <returns>A <see cref="SanitizeResult"/> with the cleaned text and action log.</returns>
        /// <exception cref="ArgumentNullException">Thrown when prompt or options is null.</exception>
        public SanitizeResult Sanitize(string prompt, SanitizeOptions options)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var result = new SanitizeResult { Original = prompt };
            var text = prompt;

            if (options.StripInvisibleChars)
                text = StripInvisible(text, result);

            if (options.EscapeSpecialTokens)
                text = EscapeTokens(text, result);

            if (options.NeutralizeInjections)
                text = NeutralizeInjectionPatterns(text, result);

            if (options.RedactPii)
                text = RedactPiiPatterns(text, options.PiiPlaceholder, result);

            if (options.NormalizeWhitespace)
                text = NormalizeWs(text, result);

            if (options.CollapseBlankLines)
                text = CollapseBlankLn(text, result);

            if (options.TrimEnds)
            {
                var trimmed = text.Trim();
                if (trimmed != text)
                {
                    result.Actions.Add(new SanitizeAction
                    {
                        Type = "trim",
                        Description = "Trimmed leading/trailing whitespace"
                    });
                    text = trimmed;
                }
            }

            if (options.MaxLength > 0 && text.Length > options.MaxLength)
            {
                text = text[..options.MaxLength];
                result.Actions.Add(new SanitizeAction
                {
                    Type = "truncate",
                    Description = $"Truncated to {options.MaxLength} characters"
                });
            }

            result.Sanitized = text;
            return result;
        }

        /// <summary>
        /// Quick sanitize that returns just the cleaned string.
        /// </summary>
        public string Clean(string prompt)
            => Sanitize(prompt).Sanitized;

        /// <summary>
        /// Quick sanitize with options that returns just the cleaned string.
        /// </summary>
        public string Clean(string prompt, SanitizeOptions options)
            => Sanitize(prompt, options).Sanitized;

        /// <summary>
        /// Check if a prompt contains potential injection patterns without modifying it.
        /// </summary>
        /// <param name="prompt">The prompt to check.</param>
        /// <returns>A list of detected injection phrases.</returns>
        public List<string> DetectInjections(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return new List<string>();
            var lower = prompt.ToLowerInvariant();
            return InjectionPhrases
                .Where(p => lower.Contains(p, StringComparison.Ordinal))
                .ToList();
        }

        /// <summary>
        /// Check if a prompt contains detectable PII patterns.
        /// </summary>
        /// <param name="prompt">The prompt to check.</param>
        /// <returns>A list of PII type names found (e.g., "email", "phone").</returns>
        public List<string> DetectPii(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return new List<string>();
            var types = new List<string>();
            if (EmailPattern.IsMatch(prompt)) types.Add("email");
            if (SsnPattern.IsMatch(prompt)) types.Add("ssn");
            if (CreditCardPattern.IsMatch(prompt)) types.Add("credit_card");
            if (PhonePattern.IsMatch(prompt)) types.Add("phone");
            if (IpAddressPattern.IsMatch(prompt)) types.Add("ip_address");
            return types;
        }

        private string StripInvisible(string text, SanitizeResult result)
        {
            var cleaned = InvisibleChars.Replace(text, "");
            if (cleaned != text)
            {
                var count = text.Length - cleaned.Length;
                result.Actions.Add(new SanitizeAction
                {
                    Type = "strip_invisible",
                    Description = $"Removed {count} invisible/zero-width character(s)"
                });
            }
            return cleaned;
        }

        private string EscapeTokens(string text, SanitizeResult result)
        {
            var sb = new StringBuilder(text);
            var escaped = 0;
            foreach (var token in SpecialTokens)
            {
                if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    // Escape by inserting a zero-width space inside the token
                    var replacement = token.Insert(1, "\u200B");
                    // Since we strip invisible chars first, use a visible escape instead
                    replacement = token.Replace("<", "＜").Replace(">", "＞")
                                       .Replace("[", "［").Replace("]", "］");
                    sb.Replace(token, replacement);
                    escaped++;
                }
            }
            if (escaped > 0)
            {
                result.Actions.Add(new SanitizeAction
                {
                    Type = "escape_tokens",
                    Description = $"Escaped {escaped} special token type(s)"
                });
            }
            return sb.ToString();
        }

        private string NeutralizeInjectionPatterns(string text, SanitizeResult result)
        {
            var lower = text.ToLowerInvariant();
            var count = 0;
            var sb = new StringBuilder(text);

            foreach (var phrase in InjectionPhrases)
            {
                var idx = lower.IndexOf(phrase, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    // Wrap the matched phrase in brackets to neutralize it
                    var original = text.Substring(idx, phrase.Length);
                    sb.Replace(original, $"[blocked: {original}]", idx, phrase.Length);

                    // Recalculate for the new length
                    text = sb.ToString();
                    lower = text.ToLowerInvariant();
                    count++;
                    idx = lower.IndexOf(phrase, idx + $"[blocked: {original}]".Length, StringComparison.Ordinal);
                }
            }

            if (count > 0)
            {
                result.InjectionPatternsNeutralized = count;
                result.Actions.Add(new SanitizeAction
                {
                    Type = "neutralize_injection",
                    Description = $"Neutralized {count} injection pattern(s)"
                });
            }
            return text;
        }

        private string RedactPiiPatterns(string text, string placeholder, SanitizeResult result)
        {
            var types = new List<string>();

            if (SsnPattern.IsMatch(text))
            {
                text = SsnPattern.Replace(text, placeholder);
                types.Add("ssn");
            }
            if (CreditCardPattern.IsMatch(text))
            {
                text = CreditCardPattern.Replace(text, placeholder);
                types.Add("credit_card");
            }
            if (EmailPattern.IsMatch(text))
            {
                text = EmailPattern.Replace(text, placeholder);
                types.Add("email");
            }
            if (PhonePattern.IsMatch(text))
            {
                text = PhonePattern.Replace(text, placeholder);
                types.Add("phone");
            }
            if (IpAddressPattern.IsMatch(text))
            {
                text = IpAddressPattern.Replace(text, placeholder);
                types.Add("ip_address");
            }

            if (types.Count > 0)
            {
                result.RedactedPiiTypes = types;
                result.Actions.Add(new SanitizeAction
                {
                    Type = "redact_pii",
                    Description = $"Redacted PII: {string.Join(", ", types)}"
                });
            }
            return text;
        }

        private string NormalizeWs(string text, SanitizeResult result)
        {
            var cleaned = TrailingLineSpaces.Replace(text, "");
            cleaned = LeadingLineSpaces.Replace(cleaned, "");
            cleaned = MultipleSpaces.Replace(cleaned, " ");
            if (cleaned != text)
            {
                result.Actions.Add(new SanitizeAction
                {
                    Type = "normalize_whitespace",
                    Description = "Normalized excessive whitespace"
                });
            }
            return cleaned;
        }

        private string CollapseBlankLn(string text, SanitizeResult result)
        {
            var cleaned = MultipleBlankLines.Replace(text, "\n\n");
            if (cleaned != text)
            {
                result.Actions.Add(new SanitizeAction
                {
                    Type = "collapse_blank_lines",
                    Description = "Collapsed excessive blank lines"
                });
            }
            return cleaned;
        }
    }
}
