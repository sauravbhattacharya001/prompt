namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Classification of a prompt annotation.
    /// </summary>
    public enum AnnotationType
    {
        /// <summary>Free-form comment — stripped before API calls.</summary>
        Comment,

        /// <summary>Structured metadata tag (key-value pair).</summary>
        Metadata,

        /// <summary>Section boundary marker for logical grouping.</summary>
        Section,

        /// <summary>Directive that controls processing behavior.</summary>
        Directive
    }

    /// <summary>
    /// Represents a single annotation found within a prompt.
    /// </summary>
    public sealed record PromptAnnotationEntry
    {
        /// <summary>The annotation type.</summary>
        public AnnotationType Type { get; init; }

        /// <summary>The raw annotation text including delimiters.</summary>
        public string RawText { get; init; } = "";

        /// <summary>The annotation content after delimiter stripping.</summary>
        public string Content { get; init; } = "";

        /// <summary>Metadata key (for <see cref="AnnotationType.Metadata"/> entries).</summary>
        public string? Key { get; init; }

        /// <summary>Metadata value (for <see cref="AnnotationType.Metadata"/> entries).</summary>
        public string? Value { get; init; }

        /// <summary>Zero-based character offset where this annotation starts in the original text.</summary>
        public int StartIndex { get; init; }

        /// <summary>Zero-based character offset where this annotation ends (exclusive).</summary>
        public int EndIndex { get; init; }

        /// <summary>The 1-based line number where the annotation begins.</summary>
        public int Line { get; init; }
    }

    /// <summary>
    /// Result of extracting all annotations from a prompt.
    /// </summary>
    public sealed class AnnotationResult
    {
        /// <summary>The original prompt text.</summary>
        public string OriginalText { get; init; } = "";

        /// <summary>The prompt text with all annotations removed.</summary>
        public string StrippedText { get; init; } = "";

        /// <summary>All annotations found, in order of appearance.</summary>
        public IReadOnlyList<PromptAnnotationEntry> Annotations { get; init; }
            = Array.Empty<PromptAnnotationEntry>();

        /// <summary>
        /// Metadata key-value pairs extracted from <c>@key: value</c> annotations.
        /// Keys are case-insensitive. If a key appears multiple times, values are comma-joined.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>All tags collected from <c>@tag: ...</c> annotations.</summary>
        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

        /// <summary>Section names found in <c>@section: name</c> annotations.</summary>
        public IReadOnlyList<string> Sections { get; init; } = Array.Empty<string>();

        /// <summary>Validation warnings (e.g., unclosed annotations, unknown directives).</summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        /// <summary>True if the prompt has no annotations.</summary>
        public bool IsClean => Annotations.Count == 0;

        /// <summary>Number of tokens saved by stripping annotations (approximate).</summary>
        public int EstimatedTokensSaved { get; init; }
    }

    /// <summary>
    /// Structured prompt annotation system.
    /// <para>
    /// Embeds inline comments and metadata within prompt text using a
    /// <c>{{# ... #}}</c> delimiter syntax. Annotations are stripped
    /// before sending prompts to an LLM, preserving documentation and
    /// metadata without wasting tokens.
    /// </para>
    ///
    /// <para><b>Annotation syntax:</b></para>
    /// <list type="bullet">
    ///   <item><c>{{# This is a comment #}}</c> — free-form comment</item>
    ///   <item><c>{{# @author: Jane Doe #}}</c> — metadata key-value pair</item>
    ///   <item><c>{{# @version: 2.1 #}}</c> — version metadata</item>
    ///   <item><c>{{# @tag: safety, production #}}</c> — tags (comma-separated)</item>
    ///   <item><c>{{# @section: introduction #}}</c> — section boundary marker</item>
    ///   <item><c>{{# @freeze #}}</c> — directive (no value)</item>
    /// </list>
    ///
    /// <para><b>Usage:</b></para>
    /// <code>
    /// var result = PromptAnnotation.Extract("You are {{# @role: system #}} a helpful assistant.");
    /// Console.WriteLine(result.StrippedText);    // "You are  a helpful assistant."
    /// Console.WriteLine(result.Metadata["role"]); // "system"
    ///
    /// // Quick strip without metadata extraction:
    /// string clean = PromptAnnotation.Strip("Hello {{# draft comment #}} world");
    /// // clean == "Hello  world"
    /// </code>
    /// </summary>
    public static class PromptAnnotation
    {
        /// <summary>Opening delimiter for annotations.</summary>
        public const string OpenDelimiter = "{{#";

        /// <summary>Closing delimiter for annotations.</summary>
        public const string CloseDelimiter = "#}}";

        // Regex for balanced annotation blocks (non-greedy).
        private static readonly Regex AnnotationPattern = new(
            @"\{\{#\s*(.*?)\s*#\}\}",
            RegexOptions.Singleline | RegexOptions.Compiled);

        // Metadata pattern: @key: value  or  @key (directive, no value).
        private static readonly Regex MetadataPattern = new(
            @"^@(\w[\w.-]*)\s*(?::\s*(.*))?$",
            RegexOptions.Compiled);

        // Known directive keywords (no value expected).
        private static readonly HashSet<string> KnownDirectives = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "freeze", "lock", "final", "deprecated", "experimental",
            "required", "optional", "sensitive", "redact", "nolog"
        };

        // Approximate tokens per character (GPT-family heuristic).
        private const double TokensPerChar = 0.25;

        /// <summary>
        /// Remove all annotations from a prompt, returning only the clean text.
        /// This is the fastest path when metadata is not needed.
        /// </summary>
        /// <param name="prompt">The annotated prompt text.</param>
        /// <returns>The prompt with all <c>{{# ... #}}</c> blocks removed.</returns>
        public static string Strip(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return prompt ?? "";
            if (!prompt.Contains(OpenDelimiter)) return prompt;
            return AnnotationPattern.Replace(prompt, "");
        }

        /// <summary>
        /// Extract all annotations and metadata from a prompt.
        /// </summary>
        /// <param name="prompt">The annotated prompt text.</param>
        /// <returns>An <see cref="AnnotationResult"/> with stripped text, metadata, tags, and warnings.</returns>
        public static AnnotationResult Extract(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return new AnnotationResult
                {
                    OriginalText = prompt ?? "",
                    StrippedText = prompt ?? ""
                };
            }

            var annotations = new List<PromptAnnotationEntry>();
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tags = new List<string>();
            var sections = new List<string>();
            var warnings = new List<string>();

            foreach (Match match in AnnotationPattern.Matches(prompt))
            {
                var content = match.Groups[1].Value.Trim();
                var line = CountLines(prompt, match.Index);
                var entry = new PromptAnnotationEntry
                {
                    RawText = match.Value,
                    Content = content,
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Line = line
                };

                var metaMatch = MetadataPattern.Match(content);
                if (metaMatch.Success)
                {
                    var key = metaMatch.Groups[1].Value;
                    var value = metaMatch.Groups[2].Success
                        ? metaMatch.Groups[2].Value.Trim()
                        : null;

                    if (string.IsNullOrEmpty(value) && KnownDirectives.Contains(key))
                    {
                        entry = entry with
                        {
                            Type = AnnotationType.Directive,
                            Key = key
                        };
                    }
                    else if (key.Equals("section", StringComparison.OrdinalIgnoreCase))
                    {
                        entry = entry with
                        {
                            Type = AnnotationType.Section,
                            Key = key,
                            Value = value
                        };
                        if (!string.IsNullOrEmpty(value))
                            sections.Add(value);
                    }
                    else if (key.Equals("tag", StringComparison.OrdinalIgnoreCase))
                    {
                        entry = entry with
                        {
                            Type = AnnotationType.Metadata,
                            Key = key,
                            Value = value
                        };
                        if (!string.IsNullOrEmpty(value))
                        {
                            foreach (var t in value.Split(','))
                            {
                                var trimmed = t.Trim();
                                if (trimmed.Length > 0 && !tags.Contains(trimmed))
                                    tags.Add(trimmed);
                            }
                        }
                    }
                    else
                    {
                        entry = entry with
                        {
                            Type = AnnotationType.Metadata,
                            Key = key,
                            Value = value
                        };
                        if (!string.IsNullOrEmpty(value))
                        {
                            if (metadata.TryGetValue(key, out var existing))
                                metadata[key] = existing + ", " + value;
                            else
                                metadata[key] = value;
                        }
                    }
                }
                else
                {
                    entry = entry with { Type = AnnotationType.Comment };
                }

                annotations.Add(entry);
            }

            // Check for unclosed annotations
            var openCount = CountOccurrences(prompt, OpenDelimiter);
            var closeCount = CountOccurrences(prompt, CloseDelimiter);
            if (openCount > closeCount)
                warnings.Add($"Found {openCount - closeCount} unclosed annotation(s) — missing '#}}}}'.");
            if (closeCount > openCount)
                warnings.Add($"Found {closeCount - openCount} stray closing delimiter(s) '#}}}}'.");

            var stripped = AnnotationPattern.Replace(prompt, "");
            var charsSaved = prompt.Length - stripped.Length;

            return new AnnotationResult
            {
                OriginalText = prompt,
                StrippedText = stripped,
                Annotations = annotations,
                Metadata = metadata,
                Tags = tags,
                Sections = sections,
                Warnings = warnings,
                EstimatedTokensSaved = (int)Math.Ceiling(charsSaved * TokensPerChar)
            };
        }

        /// <summary>
        /// Validate annotation syntax without extracting metadata.
        /// Returns a list of warnings/errors. An empty list means the syntax is valid.
        /// </summary>
        public static IReadOnlyList<string> Validate(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
                return Array.Empty<string>();

            var issues = new List<string>();

            var openCount = CountOccurrences(prompt, OpenDelimiter);
            var closeCount = CountOccurrences(prompt, CloseDelimiter);

            if (openCount != closeCount)
            {
                issues.Add(openCount > closeCount
                    ? $"Unclosed annotation: {openCount} opening vs {closeCount} closing delimiters."
                    : $"Stray closing delimiter: {closeCount} closing vs {openCount} opening delimiters.");
            }

            // Check for nested annotations (not supported)
            foreach (Match match in AnnotationPattern.Matches(prompt))
            {
                var inner = match.Groups[1].Value;
                if (inner.Contains(OpenDelimiter))
                {
                    var line = CountLines(prompt, match.Index);
                    issues.Add($"Nested annotation detected at line {line} — nesting is not supported.");
                }
            }

            // Check for empty annotations
            foreach (Match match in AnnotationPattern.Matches(prompt))
            {
                if (string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    var line = CountLines(prompt, match.Index);
                    issues.Add($"Empty annotation at line {line}.");
                }
            }

            return issues;
        }

        /// <summary>
        /// Insert an annotation at a specific position in the prompt.
        /// </summary>
        /// <param name="prompt">The prompt text.</param>
        /// <param name="position">Zero-based character index to insert at.</param>
        /// <param name="content">The annotation content (without delimiters).</param>
        /// <returns>The prompt with the annotation inserted.</returns>
        public static string Insert(string prompt, int position, string content)
        {
            prompt ??= "";
            if (position < 0) position = 0;
            if (position > prompt.Length) position = prompt.Length;
            var annotation = $"{OpenDelimiter} {content} {CloseDelimiter}";
            return prompt.Insert(position, annotation);
        }

        /// <summary>
        /// Add a metadata annotation at the beginning of the prompt.
        /// </summary>
        /// <param name="prompt">The prompt text.</param>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        /// <returns>The prompt with the metadata annotation prepended.</returns>
        public static string AddMetadata(string prompt, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Metadata key cannot be empty.", nameof(key));
            var annotation = $"{OpenDelimiter} @{key}: {value} {CloseDelimiter}\n";
            return annotation + (prompt ?? "");
        }

        /// <summary>
        /// Add a tag annotation at the beginning of the prompt.
        /// </summary>
        public static string AddTag(string prompt, params string[] tagValues)
        {
            if (tagValues == null || tagValues.Length == 0) return prompt ?? "";
            var joined = string.Join(", ", tagValues.Where(t => !string.IsNullOrWhiteSpace(t)));
            if (joined.Length == 0) return prompt ?? "";
            return $"{OpenDelimiter} @tag: {joined} {CloseDelimiter}\n" + (prompt ?? "");
        }

        /// <summary>
        /// Add a section marker at a specific position.
        /// </summary>
        public static string AddSection(string prompt, int position, string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
                throw new ArgumentException("Section name cannot be empty.", nameof(sectionName));
            return Insert(prompt ?? "", position, $"@section: {sectionName}");
        }

        /// <summary>
        /// Get only the annotations of a specific type.
        /// </summary>
        public static IReadOnlyList<PromptAnnotationEntry> GetByType(
            string prompt, AnnotationType type)
        {
            return Extract(prompt).Annotations
                .Where(a => a.Type == type)
                .ToList();
        }

        /// <summary>
        /// Check whether a prompt contains any annotations.
        /// </summary>
        public static bool HasAnnotations(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return false;
            return prompt.Contains(OpenDelimiter) && prompt.Contains(CloseDelimiter)
                && AnnotationPattern.IsMatch(prompt);
        }

        /// <summary>
        /// Get a summary string describing the annotations found.
        /// </summary>
        public static string Summarize(string prompt)
        {
            var result = Extract(prompt);
            if (result.IsClean)
                return "No annotations found.";

            var parts = new List<string>
            {
                $"{result.Annotations.Count} annotation(s)"
            };

            var comments = result.Annotations.Count(a => a.Type == AnnotationType.Comment);
            var metas = result.Annotations.Count(a => a.Type == AnnotationType.Metadata);
            var sections = result.Annotations.Count(a => a.Type == AnnotationType.Section);
            var directives = result.Annotations.Count(a => a.Type == AnnotationType.Directive);

            if (comments > 0) parts.Add($"{comments} comment(s)");
            if (metas > 0) parts.Add($"{metas} metadata");
            if (sections > 0) parts.Add($"{sections} section(s)");
            if (directives > 0) parts.Add($"{directives} directive(s)");
            parts.Add($"~{result.EstimatedTokensSaved} tokens saved");

            return string.Join(", ", parts);
        }

        // ── Helpers ──────────────────────────────────────────────

        private static int CountLines(string text, int upToIndex)
        {
            int line = 1;
            for (int i = 0; i < upToIndex && i < text.Length; i++)
            {
                if (text[i] == '\n') line++;
            }
            return line;
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int idx = 0;
            while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += pattern.Length;
            }
            return count;
        }
    }
}
