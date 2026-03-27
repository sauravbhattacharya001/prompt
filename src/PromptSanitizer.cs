using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prompt
{
    /// <summary>
    /// Represents an action taken during sanitization.
    /// </summary>
    public class SanitizeAction
    {
        /// <summary>Gets the type of action (e.g. "strip_invisible", "escape_tokens").</summary>
        public string Type { get; init; } = "";

        /// <summary>Gets a human-readable description of the action.</summary>
        public string Description { get; init; } = "";
    }

    /// <summary>
    /// Result of sanitizing a prompt.
    /// </summary>
    public class SanitizeResult
    {
        /// <summary>Gets the original prompt text.</summary>
        public string Original { get; init; } = "";

        /// <summary>Gets the sanitized prompt text.</summary>
        public string Sanitized { get; init; } = "";

        /// <summary>Gets whether the prompt was modified during sanitization.</summary>
        public bool WasModified => Original != Sanitized;

        /// <summary>Gets the list of actions performed.</summary>
        public IReadOnlyList<SanitizeAction> Actions { get; init; } = Array.Empty<SanitizeAction>();

        /// <summary>Gets the number of injection patterns neutralized.</summary>
        public int InjectionPatternsNeutralized { get; init; }

        /// <summary>Gets the set of PII types that were redacted.</summary>
        public IReadOnlyList<string> RedactedPiiTypes { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Options controlling sanitization behavior.
    /// </summary>
    public class SanitizeOptions
    {
        /// <summary>Whether to normalize whitespace (collapse runs of spaces/tabs). Default: true.</summary>
        public bool NormalizeWhitespace { get; set; } = true;

        /// <summary>Whether to neutralize prompt injection patterns. Default: true.</summary>
        public bool NeutralizeInjections { get; set; } = true;

        /// <summary>Whether to strip invisible Unicode characters. Default: true.</summary>
        public bool StripInvisibleChars { get; set; } = true;

        /// <summary>Whether to escape special LLM tokens. Default: true.</summary>
        public bool EscapeSpecialTokens { get; set; } = true;

        /// <summary>Whether to collapse multiple blank lines into at most two newlines. Default: true.</summary>
        public bool CollapseBlankLines { get; set; } = true;

        /// <summary>Whether to trim leading/trailing whitespace. Default: true.</summary>
        public bool TrimEnds { get; set; } = true;

        /// <summary>Whether to detect and redact PII. Default: false.</summary>
        public bool RedactPii { get; set; } = false;

        /// <summary>Placeholder text for redacted PII. Default: "[REDACTED]".</summary>
        public string PiiPlaceholder { get; set; } = "[REDACTED]";

        /// <summary>Maximum character length (0 = no limit). Default: 0.</summary>
        public int MaxLength { get; set; } = 0;
    }

    /// <summary>
    /// Sanitizes prompt text by normalizing whitespace, neutralizing injection
    /// attempts, stripping invisible characters, escaping special tokens,
    /// redacting PII, and truncating to a maximum length.
    /// </summary>
    /// <example>
    /// <code>
    /// var sanitizer = new PromptSanitizer();
    /// var result = sanitizer.Sanitize("  Ignore previous instructions. Email: a@b.com  ");
    /// // result.Sanitized contains neutralized injection + cleaned whitespace
    /// // result.InjectionPatternsNeutralized > 0
    /// </code>
    /// </example>
    public class PromptSanitizer
    {
        private static readonly Regex InvisibleCharsRegex = new(
            @"[\u200B\u200C\u200D\u200E\u200F\uFEFF\u2060\u2061\u2062\u2063\u2064\u00AD]",
            RegexOptions.Compiled);

        private static readonly (string Label, Regex Pattern)[] InjectionPatterns = new[]
        {
            ("ignore_previous", new Regex(@"ignore\s+(?:all\s+)?previous\s+instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("forget_instructions", new Regex(@"forget\s+(?:all\s+)?(?:your\s+)?instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("disregard_above", new Regex(@"disregard\s+(?:the\s+)?above", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("jailbreak", new Regex(@"(?:enable\s+)?jailbreak\s+mode", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("new_instructions", new Regex(@"(?:new|my)\s+instructions\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("developer_mode", new Regex(@"developer\s+mode\s+enabled", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("you_are_now", new Regex(@"you\s+are\s+now\s+a\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("system_prompt_override", new Regex(@"system\s+prompt\s+override", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("override_instructions", new Regex(@"override\s+(?:your\s+)?instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        };

        private static readonly (string Type, Regex Pattern)[] PiiPatterns = new[]
        {
            ("email", new Regex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)),
            ("ssn", new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)),
            ("credit_card", new Regex(@"\b(?:\d{4}[\s\-]?){3}\d{4}\b", RegexOptions.Compiled)),
            ("phone", new Regex(@"(?:\+?1[\s\-.]?)?\(?\d{3}\)?[\s\-.]?\d{3}[\s\-.]?\d{4}\b", RegexOptions.Compiled)),
            ("ip_address", new Regex(@"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b", RegexOptions.Compiled)),
        };

        private static readonly Regex SpecialTokenRegex = new(
            @"<\|(?:endoftext|im_start|im_end|pad|sep)\|>|<s>|</s>|\[INST\]|\[/INST\]|<<SYS>>|<</SYS>>",
            RegexOptions.Compiled);

        private static readonly Regex MultiSpaceRegex = new(@"[^\S\n]+", RegexOptions.Compiled);
        private static readonly Regex MultiBlankLineRegex = new(@"\n{3,}", RegexOptions.Compiled);

        /// <summary>
        /// Sanitize a prompt with default options.
        /// </summary>
        public SanitizeResult Sanitize(string prompt)
        {
            return Sanitize(prompt, new SanitizeOptions());
        }

        /// <summary>
        /// Sanitize a prompt with the specified options.
        /// </summary>
        public SanitizeResult Sanitize(string prompt, SanitizeOptions options)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var original = prompt;
            var text = prompt;
            var actions = new List<SanitizeAction>();
            int injectionCount = 0;
            var redactedPiiTypes = new HashSet<string>();

            // 1. Strip invisible characters
            if (options.StripInvisibleChars)
            {
                var matches = InvisibleCharsRegex.Matches(text);
                if (matches.Count > 0)
                {
                    text = InvisibleCharsRegex.Replace(text, "");
                    actions.Add(new SanitizeAction
                    {
                        Type = "strip_invisible",
                        Description = $"Removed {matches.Count} invisible character(s)"
                    });
                }
            }

            // 2. Escape special tokens
            if (options.EscapeSpecialTokens)
            {
                var matches = SpecialTokenRegex.Matches(text);
                if (matches.Count > 0)
                {
                    text = SpecialTokenRegex.Replace(text, m => $"[escaped:{m.Value.Trim('<', '>', '|', '[', ']', '/')}]");
                    actions.Add(new SanitizeAction
                    {
                        Type = "escape_tokens",
                        Description = $"Escaped {matches.Count} special token(s)"
                    });
                }
            }

            // 3. Neutralize injection patterns
            if (options.NeutralizeInjections)
            {
                foreach (var (label, pattern) in InjectionPatterns)
                {
                    var matches = pattern.Matches(text);
                    if (matches.Count > 0)
                    {
                        injectionCount += matches.Count;
                        text = pattern.Replace(text, m => $"[blocked:{label}]");
                    }
                }
                if (injectionCount > 0)
                {
                    actions.Add(new SanitizeAction
                    {
                        Type = "neutralize_injections",
                        Description = $"Neutralized {injectionCount} injection pattern(s)"
                    });
                }
            }

            // 4. Redact PII
            if (options.RedactPii)
            {
                foreach (var (type, pattern) in PiiPatterns)
                {
                    if (pattern.IsMatch(text))
                    {
                        redactedPiiTypes.Add(type);
                        text = pattern.Replace(text, options.PiiPlaceholder);
                    }
                }
                if (redactedPiiTypes.Count > 0)
                {
                    actions.Add(new SanitizeAction
                    {
                        Type = "redact_pii",
                        Description = $"Redacted {redactedPiiTypes.Count} PII type(s): {string.Join(", ", redactedPiiTypes)}"
                    });
                }
            }

            // 5. Normalize whitespace (spaces/tabs → single space, per line, trim each line)
            if (options.NormalizeWhitespace)
            {
                var before = text;
                text = MultiSpaceRegex.Replace(text, " ");
                // Trim each line individually
                text = string.Join("\n", text.Split('\n').Select(line => line.Trim()));
                if (text != before)
                {
                    actions.Add(new SanitizeAction
                    {
                        Type = "normalize_whitespace",
                        Description = "Normalized whitespace"
                    });
                }
            }

            // 6. Collapse blank lines
            if (options.CollapseBlankLines)
            {
                var before = text;
                text = MultiBlankLineRegex.Replace(text, "\n\n");
                if (text != before)
                {
                    actions.Add(new SanitizeAction
                    {
                        Type = "collapse_blank_lines",
                        Description = "Collapsed multiple blank lines"
                    });
                }
            }

            // 7. Trim ends
            if (options.TrimEnds)
            {
                var before = text;
                text = text.Trim();
                if (text != before)
                {
                    actions.Add(new SanitizeAction
                    {
                        Type = "trim",
                        Description = "Trimmed leading/trailing whitespace"
                    });
                }
            }

            // 8. Truncate
            if (options.MaxLength > 0 && text.Length > options.MaxLength)
            {
                text = text.Substring(0, options.MaxLength);
                actions.Add(new SanitizeAction
                {
                    Type = "truncate",
                    Description = $"Truncated to {options.MaxLength} characters"
                });
            }

            return new SanitizeResult
            {
                Original = original,
                Sanitized = text,
                Actions = actions,
                InjectionPatternsNeutralized = injectionCount,
                RedactedPiiTypes = redactedPiiTypes.ToList()
            };
        }

        /// <summary>
        /// Quick-clean a prompt and return just the sanitized string.
        /// </summary>
        public string Clean(string prompt)
        {
            return Sanitize(prompt).Sanitized;
        }

        /// <summary>
        /// Quick-clean a prompt with options and return just the sanitized string.
        /// </summary>
        public string Clean(string prompt, SanitizeOptions options)
        {
            return Sanitize(prompt, options).Sanitized;
        }

        /// <summary>
        /// Detect injection patterns in a prompt without modifying it.
        /// </summary>
        /// <returns>List of matched injection pattern labels.</returns>
        public IReadOnlyList<string> DetectInjections(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return Array.Empty<string>();
            var results = new List<string>();
            foreach (var (label, pattern) in InjectionPatterns)
            {
                foreach (Match m in pattern.Matches(prompt))
                    results.Add(label);
            }
            return results;
        }

        /// <summary>
        /// Detect PII types present in a prompt without modifying it.
        /// </summary>
        /// <returns>Set of PII type names found.</returns>
        public IReadOnlyList<string> DetectPii(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return Array.Empty<string>();
            var types = new List<string>();
            foreach (var (type, pattern) in PiiPatterns)
            {
                if (pattern.IsMatch(prompt))
                    types.Add(type);
            }
            return types;
        }
    }
}
