namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>How much detail to add when expanding a prompt.</summary>
    public enum ExpansionLevel
    {
        /// <summary>Add basic structure and clarify abbreviations.</summary>
        Light,
        /// <summary>Add structure, clarify abbreviations, add format/constraint hints.</summary>
        Medium,
        /// <summary>Full expansion with role framing, format specs, constraints, and examples placeholder.</summary>
        Detailed
    }

    /// <summary>Result of expanding a prompt.</summary>
    public class ExpandResult
    {
        /// <summary>The original terse prompt.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>The expanded, more detailed prompt.</summary>
        public string Expanded { get; internal set; } = "";

        /// <summary>Estimated tokens in the original.</summary>
        public int OriginalTokens { get; internal set; }

        /// <summary>Estimated tokens in the expanded prompt.</summary>
        public int ExpandedTokens { get; internal set; }

        /// <summary>Additional tokens added.</summary>
        public int TokensAdded => ExpandedTokens - OriginalTokens;

        /// <summary>Expansion ratio (expanded / original).</summary>
        public double ExpansionRatio => OriginalTokens > 0
            ? Math.Round((double)ExpandedTokens / OriginalTokens, 2)
            : 0;

        /// <summary>Transformations applied during expansion.</summary>
        public IReadOnlyList<string> Transformations { get; internal set; }
            = Array.Empty<string>();
    }

    /// <summary>
    /// Expands terse, abbreviated prompts into detailed, well-structured
    /// instructions. The inverse of <see cref="PromptMinifier"/>: takes
    /// shorthand and produces thorough LLM-ready prompts.
    /// </summary>
    /// <remarks>
    /// <para>Example usage:</para>
    /// <code>
    /// // Quick expand
    /// string detailed = PromptExpander.Expand("summarize this doc");
    /// // → "Please provide a comprehensive summary of the following document.
    /// //    Include the key points, main arguments, and conclusions."
    ///
    /// // With stats
    /// var result = PromptExpander.ExpandWithStats(
    ///     "list pros cons of microservices", ExpansionLevel.Detailed);
    /// Console.WriteLine($"Added {result.TokensAdded} tokens (ratio {result.ExpansionRatio}x)");
    /// Console.WriteLine(result.Expanded);
    /// </code>
    /// </remarks>
    public static class PromptExpander
    {
        // ── Abbreviation expansions ──

        private static readonly (Regex Pattern, string Replacement, string Description)[] AbbreviationRules =
        {
            (Compile(@"\binfo\b"), "information", "Expand 'info' → 'information'"),
            (Compile(@"\bapp\b"), "application", "Expand 'app' → 'application'"),
            (Compile(@"\bapps\b"), "applications", "Expand 'apps' → 'applications'"),
            (Compile(@"\bconfig\b"), "configuration", "Expand 'config' → 'configuration'"),
            (Compile(@"\bdocs\b"), "documentation", "Expand 'docs' → 'documentation'"),
            (Compile(@"\bdoc\b"), "document", "Expand 'doc' → 'document'"),
            (Compile(@"\bimpl\b"), "implementation", "Expand 'impl' → 'implementation'"),
            (Compile(@"\benv\b"), "environment", "Expand 'env' → 'environment'"),
            (Compile(@"\brepo\b"), "repository", "Expand 'repo' → 'repository'"),
            (Compile(@"\brepos\b"), "repositories", "Expand 'repos' → 'repositories'"),
            (Compile(@"\bfunc\b"), "function", "Expand 'func' → 'function'"),
            (Compile(@"\bfuncs\b"), "functions", "Expand 'funcs' → 'functions'"),
            (Compile(@"\be\.g\.\b"), "for example", "Expand 'e.g.' → 'for example'"),
            (Compile(@"\bi\.e\.\b"), "that is", "Expand 'i.e.' → 'that is'"),
            (Compile(@"\bpkg\b"), "package", "Expand 'pkg' → 'package'"),
            (Compile(@"\bpkgs\b"), "packages", "Expand 'pkgs' → 'packages'"),
            (Compile(@"\bdb\b"), "database", "Expand 'db' → 'database'"),
            (Compile(@"\bauth\b"), "authentication", "Expand 'auth' → 'authentication'"),
            (Compile(@"\bsrc\b"), "source", "Expand 'src' → 'source'"),
            (Compile(@"\bparam\b"), "parameter", "Expand 'param' → 'parameter'"),
            (Compile(@"\bparams\b"), "parameters", "Expand 'params' → 'parameters'"),
            (Compile(@"\bargs\b"), "arguments", "Expand 'args' → 'arguments'"),
            (Compile(@"\barg\b"), "argument", "Expand 'arg' → 'argument'"),
            (Compile(@"\bnum\b"), "number", "Expand 'num' → 'number'"),
            (Compile(@"\bstr\b"), "string", "Expand 'str' → 'string'"),
            (Compile(@"\bmsg\b"), "message", "Expand 'msg' → 'message'"),
            (Compile(@"\bmsgs\b"), "messages", "Expand 'msgs' → 'messages'"),
            (Compile(@"\berr\b"), "error", "Expand 'err' → 'error'"),
            (Compile(@"\berrs\b"), "errors", "Expand 'errs' → 'errors'"),
            (Compile(@"\bdevs\b"), "developers", "Expand 'devs' → 'developers'"),
            (Compile(@"\bdev\b"), "developer", "Expand 'dev' → 'developer'"),
        };

        // ── Task verb enrichment patterns ──

        private static readonly (Regex Pattern, string Replacement, string Description)[] TaskEnrichmentRules =
        {
            (Compile(@"^summarize\b", true), "Provide a comprehensive summary of", "Enrich 'summarize'"),
            (Compile(@"^explain\b", true), "Provide a clear and detailed explanation of", "Enrich 'explain'"),
            (Compile(@"^list\b", true), "Provide a comprehensive list of", "Enrich 'list'"),
            (Compile(@"^compare\b", true), "Provide a detailed comparison of", "Enrich 'compare'"),
            (Compile(@"^fix\b", true), "Identify and fix the issues in", "Enrich 'fix'"),
            (Compile(@"^refactor\b", true), "Refactor and improve the structure of", "Enrich 'refactor'"),
            (Compile(@"^review\b", true), "Perform a thorough review of", "Enrich 'review'"),
            (Compile(@"^debug\b", true), "Analyze and debug the following, identifying root causes in", "Enrich 'debug'"),
            (Compile(@"^optimize\b", true), "Analyze and optimize for better performance", "Enrich 'optimize'"),
            (Compile(@"^write\b", true), "Write a well-structured and complete", "Enrich 'write'"),
            (Compile(@"^create\b", true), "Create a well-designed and complete", "Enrich 'create'"),
            (Compile(@"^convert\b", true), "Convert the following, preserving all meaning and structure, from", "Enrich 'convert'"),
            (Compile(@"^translate\b", true), "Translate the following accurately, preserving tone and meaning, to", "Enrich 'translate'"),
            (Compile(@"^analyze\b", true), "Perform a thorough analysis of", "Enrich 'analyze'"),
            (Compile(@"^generate\b", true), "Generate a complete and well-formed", "Enrich 'generate'"),
            (Compile(@"^describe\b", true), "Provide a detailed description of", "Enrich 'describe'"),
            (Compile(@"^design\b", true), "Design a well-architected solution for", "Enrich 'design'"),
            (Compile(@"^test\b", true), "Write comprehensive tests for", "Enrich 'test'"),
        };

        // ── Pros/cons and comparison detection ──

        private static readonly Regex ProsConsPattern = Compile(
            @"\b(pros?\s*(and|&|\/)\s*cons?|advantages?\s*(and|&|\/)\s*disadvantages?)\b");

        private static readonly Regex ListRequestPattern = Compile(
            @"\b(list|enumerate|name|give\s+me|what\s+are)\b");

        private static readonly Regex HowToPattern = Compile(
            @"\b(how\s+to|how\s+do\s+I|how\s+can\s+I|steps\s+to)\b");

        /// <summary>
        /// Expand a terse prompt into a more detailed version.
        /// </summary>
        /// <param name="prompt">The prompt to expand.</param>
        /// <param name="level">How much detail to add (default: Medium).</param>
        /// <returns>The expanded prompt string.</returns>
        public static string Expand(string prompt, ExpansionLevel level = ExpansionLevel.Medium)
        {
            return ExpandWithStats(prompt, level).Expanded;
        }

        /// <summary>
        /// Expand a terse prompt and return detailed statistics.
        /// </summary>
        /// <param name="prompt">The prompt to expand.</param>
        /// <param name="level">How much detail to add (default: Medium).</param>
        /// <returns>An <see cref="ExpandResult"/> with the expanded text and stats.</returns>
        public static ExpandResult ExpandWithStats(
            string prompt, ExpansionLevel level = ExpansionLevel.Medium)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));

            int originalTokens = PromptGuard.EstimateTokens(prompt);
            var transformations = new List<string>();
            string result = prompt.Trim();

            // Step 1: Always expand abbreviations
            result = ApplyRules(result, AbbreviationRules, transformations);

            // Step 2: Enrich task verbs (Light+)
            result = ApplyRules(result, TaskEnrichmentRules, transformations);

            // Step 3: Add structure hints (Medium+)
            if (level >= ExpansionLevel.Medium)
            {
                result = AddStructureHints(result, transformations);
            }

            // Step 4: Full framing (Detailed)
            if (level >= ExpansionLevel.Detailed)
            {
                result = AddDetailedFraming(result, transformations);
            }

            // Ensure proper capitalization and punctuation
            result = EnsureCapitalization(result);
            result = EnsurePunctuation(result);

            int expandedTokens = PromptGuard.EstimateTokens(result);

            return new ExpandResult
            {
                Original = prompt,
                Expanded = result,
                OriginalTokens = originalTokens,
                ExpandedTokens = expandedTokens,
                Transformations = transformations
            };
        }

        /// <summary>
        /// Expand multiple prompts at once, returning results for each.
        /// </summary>
        public static List<ExpandResult> ExpandBatch(
            IEnumerable<string> prompts, ExpansionLevel level = ExpansionLevel.Medium)
        {
            if (prompts == null)
                throw new ArgumentNullException(nameof(prompts));

            return prompts.Select(p => ExpandWithStats(p, level)).ToList();
        }

        /// <summary>
        /// Suggest what expansion level would be most appropriate for a prompt
        /// based on its length and complexity.
        /// </summary>
        public static ExpansionLevel SuggestLevel(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return ExpansionLevel.Detailed;

            int tokens = PromptGuard.EstimateTokens(prompt);
            int wordCount = prompt.Split(
                new[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries).Length;

            // Very short prompts need heavy expansion
            if (wordCount <= 5 || tokens <= 8) return ExpansionLevel.Detailed;
            // Medium-length prompts need moderate expansion
            if (wordCount <= 20 || tokens <= 30) return ExpansionLevel.Medium;
            // Longer prompts just need cleanup
            return ExpansionLevel.Light;
        }

        // ── Private helpers ──

        private static string AddStructureHints(string text, List<string> transformations)
        {
            var sb = new StringBuilder(text);
            bool changed = false;

            // If it looks like a pros/cons request, add structure hint
            if (ProsConsPattern.IsMatch(text))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Format the response with clear sections for each side, ");
                sb.Append("including specific examples where possible.");
                transformations.Add("Added pros/cons structure hint");
                changed = true;
            }
            // If it's a list request, add list formatting hint
            else if (ListRequestPattern.IsMatch(text) && !text.Contains("format"))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Present the results as a well-organized list with brief ");
                sb.Append("descriptions for each item.");
                transformations.Add("Added list formatting hint");
                changed = true;
            }
            // If it's a how-to request, add step structure hint
            else if (HowToPattern.IsMatch(text))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Provide step-by-step instructions, explaining the reasoning ");
                sb.Append("behind each step.");
                transformations.Add("Added step-by-step structure hint");
                changed = true;
            }

            if (!changed)
            {
                // Generic quality hint for medium expansion
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Be thorough and specific in your response.");
                transformations.Add("Added quality hint");
            }

            return sb.ToString();
        }

        private static string AddDetailedFraming(string text, List<string> transformations)
        {
            var sb = new StringBuilder();

            // Add role framing if not already present
            if (!text.Contains("You are") && !text.Contains("Act as") &&
                !text.Contains("Role:") && !text.Contains("you are"))
            {
                sb.AppendLine("You are a knowledgeable and helpful assistant.");
                sb.AppendLine();
                transformations.Add("Added role framing");
            }

            sb.Append(text);

            // Add constraints section if not present
            if (!text.Contains("Constraint") && !text.Contains("constraint") &&
                !text.Contains("Note:") && !text.Contains("Important:"))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("Constraints:");
                sb.AppendLine("- Be accurate and factual");
                sb.AppendLine("- If uncertain about something, say so");
                sb.AppendLine("- Use concrete examples where helpful");
                transformations.Add("Added constraints section");
            }

            return sb.ToString();
        }

        private static string EnsureCapitalization(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (char.IsLower(text[0]))
                return char.ToUpper(text[0]) + text.Substring(1);
            return text;
        }

        private static string EnsurePunctuation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.TrimEnd();
            char last = text[text.Length - 1];
            if (last != '.' && last != '?' && last != '!' && last != ':' && last != '\n')
                return text + ".";
            return text;
        }

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

        private static Regex Compile(string pattern, bool startOfLine = false)
        {
            var options = RegexOptions.IgnoreCase | RegexOptions.Compiled;
            return new Regex(pattern, options, TimeSpan.FromMilliseconds(500));
        }
    }
}
