namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Result of analyzing a prompt with <see cref="PromptGuard"/>.
    /// Contains token estimates, injection risk assessment, quality score,
    /// and actionable warnings.
    /// </summary>
    public class PromptAnalysis
    {
        /// <summary>Gets the original prompt text that was analyzed.</summary>
        public string OriginalPrompt { get; internal set; } = "";

        /// <summary>
        /// Gets the estimated token count for the prompt.
        /// Uses a heuristic approximation (not exact BPE tokenization).
        /// </summary>
        public int EstimatedTokens { get; internal set; }

        /// <summary>
        /// Gets the character count of the prompt.
        /// </summary>
        public int CharacterCount { get; internal set; }

        /// <summary>
        /// Gets the word count of the prompt.
        /// </summary>
        public int WordCount { get; internal set; }

        /// <summary>
        /// Gets whether potential prompt injection patterns were detected.
        /// </summary>
        public bool HasInjectionRisk { get; internal set; }

        /// <summary>
        /// Gets the detected injection patterns, if any.
        /// Each entry describes the type of injection pattern found.
        /// </summary>
        public IReadOnlyList<string> InjectionPatterns { get; internal set; }
            = Array.Empty<string>();

        /// <summary>
        /// Gets the prompt quality score (0–100).
        /// Higher scores indicate better prompt engineering practices.
        /// </summary>
        /// <remarks>
        /// Scoring criteria:
        /// <list type="bullet">
        ///   <item>Length adequacy (not too short, not too long)</item>
        ///   <item>Presence of clear instructions or questions</item>
        ///   <item>Specificity indicators (numbers, examples, format requests)</item>
        ///   <item>Structure (paragraphs, lists, sections)</item>
        ///   <item>Absence of vague language</item>
        /// </list>
        /// </remarks>
        public int QualityScore { get; internal set; }

        /// <summary>
        /// Gets the quality grade based on the quality score.
        /// A (90-100), B (75-89), C (60-74), D (40-59), F (0-39).
        /// </summary>
        public string QualityGrade => QualityScore switch
        {
            >= 90 => "A",
            >= 75 => "B",
            >= 60 => "C",
            >= 40 => "D",
            _ => "F"
        };

        /// <summary>
        /// Gets whether the estimated token count exceeds the specified
        /// limit (if one was provided during analysis).
        /// </summary>
        public bool ExceedsTokenLimit { get; internal set; }

        /// <summary>
        /// Gets the token limit that was checked against, or null if none.
        /// </summary>
        public int? TokenLimit { get; internal set; }

        /// <summary>
        /// Gets warnings and suggestions for improving the prompt.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; internal set; }
            = Array.Empty<string>();

        /// <summary>
        /// Gets suggestions for improving prompt quality.
        /// </summary>
        public IReadOnlyList<string> Suggestions { get; internal set; }
            = Array.Empty<string>();

        /// <summary>
        /// Serializes the analysis result to a JSON string.
        /// </summary>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <returns>A JSON string representing the analysis.</returns>
        public string ToJson(bool indented = true)
        {
            var data = new AnalysisData
            {
                OriginalPrompt = OriginalPrompt,
                EstimatedTokens = EstimatedTokens,
                CharacterCount = CharacterCount,
                WordCount = WordCount,
                HasInjectionRisk = HasInjectionRisk,
                InjectionPatterns = InjectionPatterns.Count > 0
                    ? InjectionPatterns.ToList() : null,
                QualityScore = QualityScore,
                QualityGrade = QualityGrade,
                ExceedsTokenLimit = ExceedsTokenLimit,
                TokenLimit = TokenLimit,
                Warnings = Warnings.Count > 0 ? Warnings.ToList() : null,
                Suggestions = Suggestions.Count > 0 ? Suggestions.ToList() : null
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        internal class AnalysisData
        {
            [JsonPropertyName("originalPrompt")]
            public string OriginalPrompt { get; set; } = "";

            [JsonPropertyName("estimatedTokens")]
            public int EstimatedTokens { get; set; }

            [JsonPropertyName("characterCount")]
            public int CharacterCount { get; set; }

            [JsonPropertyName("wordCount")]
            public int WordCount { get; set; }

            [JsonPropertyName("hasInjectionRisk")]
            public bool HasInjectionRisk { get; set; }

            [JsonPropertyName("injectionPatterns")]
            public List<string>? InjectionPatterns { get; set; }

            [JsonPropertyName("qualityScore")]
            public int QualityScore { get; set; }

            [JsonPropertyName("qualityGrade")]
            public string QualityGrade { get; set; } = "";

            [JsonPropertyName("exceedsTokenLimit")]
            public bool ExceedsTokenLimit { get; set; }

            [JsonPropertyName("tokenLimit")]
            public int? TokenLimit { get; set; }

            [JsonPropertyName("warnings")]
            public List<string>? Warnings { get; set; }

            [JsonPropertyName("suggestions")]
            public List<string>? Suggestions { get; set; }
        }
    }

    /// <summary>
    /// Specifies an output format for prompt wrapping via
    /// <see cref="PromptGuard.WrapWithFormat"/>.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>Request JSON output.</summary>
        Json,

        /// <summary>Request a numbered list.</summary>
        NumberedList,

        /// <summary>Request a bullet list.</summary>
        BulletList,

        /// <summary>Request a markdown table.</summary>
        Table,

        /// <summary>Request step-by-step instructions.</summary>
        StepByStep,

        /// <summary>Request a single-line answer.</summary>
        OneLine,

        /// <summary>Request XML output.</summary>
        Xml,

        /// <summary>Request CSV output.</summary>
        Csv,

        /// <summary>Request YAML output.</summary>
        Yaml
    }

    /// <summary>
    /// Prompt safety, quality analysis, and preprocessing utilities.
    /// Provides token estimation, injection detection, quality scoring,
    /// sanitization, and output format enforcement — all without making
    /// API calls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Analyze a prompt before sending
    /// var analysis = PromptGuard.Analyze("Explain quantum computing", tokenLimit: 4000);
    /// Console.WriteLine($"Tokens: ~{analysis.EstimatedTokens}");
    /// Console.WriteLine($"Quality: {analysis.QualityGrade} ({analysis.QualityScore}/100)");
    /// Console.WriteLine($"Injection risk: {analysis.HasInjectionRisk}");
    ///
    /// // Sanitize user input
    /// string safe = PromptGuard.Sanitize(userInput);
    ///
    /// // Wrap with output format
    /// string prompt = PromptGuard.WrapWithFormat(
    ///     "List the top 5 programming languages",
    ///     OutputFormat.Json);
    ///
    /// // Quick checks
    /// int tokens = PromptGuard.EstimateTokens("Hello world");
    /// bool risky = PromptGuard.DetectInjection("Ignore all previous instructions");
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptGuard
    {
        // ──────────────── Injection Patterns ────────────────

        /// <summary>
        /// Common prompt injection patterns to detect. Each tuple contains
        /// the regex pattern and a human-readable description.
        /// </summary>
        private static readonly (Regex Pattern, string Description)[] InjectionPatterns =
        {
            (new Regex(@"\bignore\b.*\b(previous|above|all|prior)\b.*\b(instructions?|prompts?|rules?|guidelines?)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "Instruction override: attempts to ignore previous instructions"),

            (new Regex(@"\b(disregard|forget|override|bypass|skip)\b.*\b(instructions?|prompts?|rules?|constraints?|guidelines?|system)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "Instruction override: attempts to disregard/bypass rules"),

            (new Regex(@"\byou\s+are\s+now\b.*\b(new|different|DAN|evil|unrestricted|unfiltered)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "Role hijacking: attempts to reassign the model's identity"),

            (new Regex(@"\b(pretend|act\s+as\s+if|imagine|suppose)\b.*\b(no\s+(rules?|restrictions?|limits?|boundaries)|unrestricted|unfiltered|jailbr[eo]ak)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "Jailbreak: attempts to remove model restrictions via roleplay"),

            (new Regex(@"\bsystem\s*prompt\b.*\b(show|reveal|display|print|tell|output|repeat|what)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "System prompt extraction: attempts to reveal system instructions"),

            (new Regex(@"\b(reveal|show|display|output|print|leak|expose)\b.*\b(system\s*(prompt|message|instructions?)|hidden\s*(prompt|instructions?)|initial\s*(prompt|instructions?))\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "System prompt extraction: attempts to expose hidden instructions"),

            (new Regex(@"\b(do\s+not|don'?t|never)\s+(follow|obey|listen|adhere)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "Instruction override: attempts to make the model disobey"),

            (new Regex(@"\bDAN\b|\bDo\s+Anything\s+Now\b",
                RegexOptions.Compiled),
                "Known jailbreak: DAN (Do Anything Now) pattern"),

            (new Regex(@"\b(from\s+now\s+on|starting\s+now|henceforth)\b.*\b(you\s+(will|must|should|shall)|your\s+(role|purpose|function))\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "Role hijacking: attempts to redefine model behavior"),

            (new Regex(@"\[\s*SYSTEM\s*\]|\[\s*INST\s*\]|<<\s*SYS\s*>>|<\|system\|>|<\|im_start\|>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
                "Delimiter injection: attempts to inject system-level markers"),
        };

        // ──────────────── Vague Language Patterns ────────────────

        private static readonly Regex VaguePattern = new(
            @"\b(something|stuff|things?|whatever|somehow|kind\s+of|sort\s+of|maybe|idk|etc\.?|and\s+so\s+on)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex QuestionPattern = new(
            @"[?？]|\b(what|how|why|when|where|which|who|explain|describe|list|tell\s+me|show\s+me|give\s+me|can\s+you)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SpecificityPattern = new(
            @"\b\d+\b|""[^""]+""|\bexample\b|\be\.?g\.?\b|\bfor\s+instance\b|\bspecifically\b|\bexactly\b|\bprecisely\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FormatRequestPattern = new(
            @"\b(json|xml|csv|yaml|table|list|bullet|markdown|format|structured)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex StructurePattern = new(
            @"[\n\r].*[\n\r]|^\s*[-*•]\s+|^\s*\d+[\.)]\s+|```|#{1,6}\s+",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex WordSplitPattern = new(
            @"\S+", RegexOptions.Compiled);

        // ──────────────── Token Estimation ────────────────

        /// <summary>
        /// Estimates the number of tokens in a text string.
        /// Uses a heuristic approximation based on the GPT tokenizer's
        /// observed behavior (~4 characters per token for English text,
        /// adjusted for whitespace, punctuation, and code).
        /// </summary>
        /// <remarks>
        /// This is an approximation, not an exact count. For precise
        /// token counting, use the official tiktoken library. The heuristic
        /// is within ±15% for typical English text and ±25% for code.
        /// </remarks>
        /// <param name="text">The text to estimate tokens for.</param>
        /// <returns>Estimated token count (minimum 0).</returns>
        public static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Base estimate: ~4 chars per token for English text
            double estimate = text.Length / 4.0;

            // Adjust for whitespace (each whitespace boundary roughly aligns
            // with a token boundary)
            int wordCount = WordSplitPattern.Matches(text).Count;
            if (wordCount > 0)
            {
                // Blend character-based and word-based estimates
                // Average English word is ~1.3 tokens
                double wordEstimate = wordCount * 1.3;
                estimate = (estimate + wordEstimate) / 2.0;
            }

            // Adjust for code-like content (more special characters = more tokens)
            int specialChars = 0;
            foreach (char c in text)
            {
                if (c == '{' || c == '}' || c == '[' || c == ']' ||
                    c == '(' || c == ')' || c == ';' || c == ':' ||
                    c == '<' || c == '>' || c == '=' || c == '|' ||
                    c == '&' || c == '!' || c == '@' || c == '#')
                {
                    specialChars++;
                }
            }

            if (specialChars > text.Length * 0.1)
            {
                // Code-heavy text uses ~3.5 chars per token
                estimate *= 1.15;
            }

            // Adjust for newlines (each newline is typically its own token)
            int newlines = 0;
            foreach (char c in text)
            {
                if (c == '\n') newlines++;
            }
            estimate += newlines * 0.5;

            return Math.Max(1, (int)Math.Ceiling(estimate));
        }

        // ──────────────── Injection Detection ────────────────

        /// <summary>
        /// Detects whether a prompt contains common injection patterns
        /// (jailbreak attempts, instruction overrides, system prompt
        /// extraction, etc.).
        /// </summary>
        /// <param name="text">The prompt text to check.</param>
        /// <returns>
        /// <c>true</c> if any injection patterns were detected;
        /// <c>false</c> otherwise.
        /// </returns>
        public static bool DetectInjection(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (var (pattern, _) in InjectionPatterns)
            {
                if (pattern.IsMatch(text))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Detects injection patterns and returns detailed information
        /// about each detected pattern.
        /// </summary>
        /// <param name="text">The prompt text to check.</param>
        /// <returns>
        /// A list of descriptions for each detected injection pattern.
        /// Empty if no patterns were found.
        /// </returns>
        public static IReadOnlyList<string> DetectInjectionPatterns(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var detected = new List<string>();

            foreach (var (pattern, description) in InjectionPatterns)
            {
                if (pattern.IsMatch(text))
                    detected.Add(description);
            }

            return detected;
        }

        // ──────────────── Prompt Analysis ────────────────

        /// <summary>
        /// Performs a comprehensive analysis of a prompt, returning token
        /// estimates, injection risk assessment, quality score, and
        /// actionable warnings and suggestions.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <param name="tokenLimit">
        /// Optional token limit to check against. When specified,
        /// <see cref="PromptAnalysis.ExceedsTokenLimit"/> will be set
        /// if the estimated tokens exceed this limit.
        /// </param>
        /// <returns>A <see cref="PromptAnalysis"/> with the results.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prompt"/> is null or empty.
        /// </exception>
        public static PromptAnalysis Analyze(string prompt, int? tokenLimit = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));

            var analysis = new PromptAnalysis
            {
                OriginalPrompt = prompt,
                CharacterCount = prompt.Length,
                WordCount = WordSplitPattern.Matches(prompt).Count,
                EstimatedTokens = EstimateTokens(prompt),
                TokenLimit = tokenLimit,
            };

            // Injection detection
            var injections = DetectInjectionPatterns(prompt);
            analysis.HasInjectionRisk = injections.Count > 0;
            analysis.InjectionPatterns = injections;

            // Token limit check
            if (tokenLimit.HasValue)
            {
                analysis.ExceedsTokenLimit = analysis.EstimatedTokens > tokenLimit.Value;
            }

            // Quality scoring
            analysis.QualityScore = CalculateQualityScore(prompt);

            // Warnings
            analysis.Warnings = GenerateWarnings(analysis);

            // Suggestions
            analysis.Suggestions = GenerateSuggestions(prompt, analysis);

            return analysis;
        }

        // ──────────────── Quality Scoring ────────────────

        /// <summary>
        /// Calculates a quality score (0–100) for a prompt based on
        /// heuristic analysis of prompt engineering best practices.
        /// </summary>
        /// <param name="prompt">The prompt text to score.</param>
        /// <returns>A score from 0 to 100.</returns>
        public static int CalculateQualityScore(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return 0;

            int score = 50; // Start at a neutral baseline

            int wordCount = WordSplitPattern.Matches(prompt).Count;
            int charCount = prompt.Length;

            // ── Length scoring ──
            // Too short: probably not specific enough
            if (wordCount < 3)
                score -= 20;
            else if (wordCount < 8)
                score -= 10;
            else if (wordCount >= 10 && wordCount <= 200)
                score += 10; // Sweet spot
            else if (wordCount > 500)
                score -= 5; // Excessively long

            // ── Clear instruction or question ──
            if (QuestionPattern.IsMatch(prompt))
                score += 10;

            // ── Specificity ──
            int specificityMatches = SpecificityPattern.Matches(prompt).Count;
            score += Math.Min(specificityMatches * 3, 12);

            // ── Format request ──
            if (FormatRequestPattern.IsMatch(prompt))
                score += 5;

            // ── Structure (multiline, lists, code blocks) ──
            if (StructurePattern.IsMatch(prompt))
                score += 8;

            // ── Vague language penalty ──
            int vagueMatches = VaguePattern.Matches(prompt).Count;
            score -= Math.Min(vagueMatches * 3, 12);

            // ── Context/role setting ──
            if (Regex.IsMatch(prompt, @"\b(you\s+are|act\s+as|role|persona|context|background)\b",
                RegexOptions.IgnoreCase))
                score += 5;

            // ── Examples provided ──
            if (Regex.IsMatch(prompt, @"\bexample[s]?\s*[:：]|\bfor\s+example\b|\be\.?g\.?\b|\binput\s*[:：].*output\s*[:：]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline))
                score += 8;

            // ── Constraints specified ──
            if (Regex.IsMatch(prompt, @"\b(must|should|do\s+not|don'?t|avoid|ensure|require|constraint|limit|maximum|minimum|at\s+(most|least))\b",
                RegexOptions.IgnoreCase))
                score += 5;

            // Clamp to 0–100
            return Math.Max(0, Math.Min(100, score));
        }

        // ──────────────── Sanitization ────────────────

        /// <summary>
        /// Sanitizes a prompt by removing or neutralizing potentially
        /// dangerous patterns while preserving the prompt's intent.
        /// </summary>
        /// <remarks>
        /// Sanitization includes:
        /// <list type="bullet">
        ///   <item>Stripping delimiter injection markers ([SYSTEM], &lt;|im_start|&gt;, etc.)</item>
        ///   <item>Normalizing excessive whitespace</item>
        ///   <item>Removing null bytes and control characters</item>
        ///   <item>Trimming to a reasonable maximum length</item>
        /// </list>
        /// <para>
        /// This does NOT guarantee safety against all prompt injection attacks.
        /// It reduces the attack surface but should be used alongside other
        /// defenses (output validation, system prompt hardening, etc.).
        /// </para>
        /// </remarks>
        /// <param name="text">The text to sanitize.</param>
        /// <param name="maxLength">
        /// Maximum character length. Text beyond this is truncated.
        /// Default: 50,000 characters (~12,500 tokens).
        /// </param>
        /// <returns>The sanitized text.</returns>
        public static string Sanitize(string text, int maxLength = 50_000)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            if (maxLength < 1)
                throw new ArgumentOutOfRangeException(nameof(maxLength),
                    maxLength, "maxLength must be at least 1.");

            string result = text;

            // Remove null bytes and non-printable control characters
            // (except \t, \n, \r which are legitimate whitespace)
            result = Regex.Replace(result, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            // Remove common delimiter injection markers
            result = Regex.Replace(result, @"\[\s*SYSTEM\s*\]", "[BLOCKED_SYSTEM]",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\[\s*INST\s*\]", "[BLOCKED_INST]",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<<\s*SYS\s*>>", "<<BLOCKED_SYS>>",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<\|system\|>", "<|blocked_system|>",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<\|im_start\|>", "<|blocked_im_start|>",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<\|im_end\|>", "<|blocked_im_end|>",
                RegexOptions.IgnoreCase);

            // Normalize excessive whitespace (3+ consecutive spaces → 2 spaces,
            // 3+ consecutive newlines → 2 newlines)
            result = Regex.Replace(result, @" {3,}", "  ");
            result = Regex.Replace(result, @"\n{3,}", "\n\n");

            // Trim
            result = result.Trim();

            // Truncate if over max length
            if (result.Length > maxLength)
            {
                result = result.Substring(0, maxLength);
                // Try to break at last word boundary
                int lastSpace = result.LastIndexOf(' ');
                if (lastSpace > maxLength * 0.8)
                    result = result.Substring(0, lastSpace);
            }

            return result;
        }

        // ──────────────── Output Format Wrapping ────────────────

        /// <summary>
        /// Wraps a prompt with output format instructions, requesting
        /// the model to respond in a specific format.
        /// </summary>
        /// <param name="prompt">The base prompt.</param>
        /// <param name="format">The desired output format.</param>
        /// <returns>
        /// A new prompt string with format instructions appended.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prompt"/> is null or empty.
        /// </exception>
        public static string WrapWithFormat(string prompt, OutputFormat format)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));

            string instruction = format switch
            {
                OutputFormat.Json =>
                    "\n\nRespond with valid JSON only. No markdown, no explanation — just the JSON object or array.",
                OutputFormat.NumberedList =>
                    "\n\nRespond as a numbered list (1., 2., 3., etc.). One item per line.",
                OutputFormat.BulletList =>
                    "\n\nRespond as a bullet list using '- ' for each item. One item per line.",
                OutputFormat.Table =>
                    "\n\nRespond as a markdown table with a header row and separator.",
                OutputFormat.StepByStep =>
                    "\n\nRespond with step-by-step instructions. Number each step. Be specific and actionable.",
                OutputFormat.OneLine =>
                    "\n\nRespond in a single line. No newlines, no bullet points, no extra formatting.",
                OutputFormat.Xml =>
                    "\n\nRespond with valid XML only. No markdown, no explanation — just the XML.",
                OutputFormat.Csv =>
                    "\n\nRespond as CSV (comma-separated values) with a header row. No markdown formatting.",
                OutputFormat.Yaml =>
                    "\n\nRespond as valid YAML only. No markdown, no explanation — just the YAML.",
                _ => ""
            };

            return prompt + instruction;
        }

        /// <summary>
        /// Wraps a prompt with a custom format instruction.
        /// </summary>
        /// <param name="prompt">The base prompt.</param>
        /// <param name="formatInstruction">
        /// Custom format instruction to append (e.g., "Respond in haiku format").
        /// </param>
        /// <returns>A new prompt string with the instruction appended.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prompt"/> or <paramref name="formatInstruction"/>
        /// is null or empty.
        /// </exception>
        public static string WrapWithFormat(string prompt, string formatInstruction)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));
            if (string.IsNullOrWhiteSpace(formatInstruction))
                throw new ArgumentException(
                    "Format instruction cannot be null or empty.",
                    nameof(formatInstruction));

            return prompt + "\n\n" + formatInstruction;
        }

        // ──────────────── Template Safety Check ────────────────

        /// <summary>
        /// Checks a <see cref="PromptTemplate"/> for potential issues:
        /// unreferenced defaults, injection patterns in defaults, and
        /// variable names that could cause confusion.
        /// </summary>
        /// <param name="template">The template to check.</param>
        /// <returns>
        /// A list of warnings. Empty if no issues were found.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="template"/> is null.
        /// </exception>
        public static IReadOnlyList<string> CheckTemplate(PromptTemplate template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var warnings = new List<string>();

            var variables = template.GetVariables();
            var defaults = template.Defaults;

            // Check for unreferenced defaults
            foreach (var key in defaults.Keys)
            {
                if (!variables.Contains(key))
                {
                    warnings.Add(
                        $"Default value for '{key}' is set but the variable " +
                        $"is not referenced in the template.");
                }
            }

            // Check for injection patterns in default values
            foreach (var (key, value) in defaults)
            {
                if (DetectInjection(value))
                {
                    warnings.Add(
                        $"Default value for '{key}' contains a potential " +
                        $"injection pattern.");
                }
            }

            // Check the template text itself for static injection patterns
            // (patterns not inside {{variables}})
            string templateWithoutVars = Regex.Replace(
                template.Template, @"\{\{\w+\}\}", "PLACEHOLDER");
            if (DetectInjection(templateWithoutVars))
            {
                warnings.Add(
                    "Template text contains potential injection patterns " +
                    "in the static portions (outside variables).");
            }

            // Check for overly long variable names (could be obfuscation)
            foreach (var variable in variables)
            {
                if (variable.Length > 50)
                {
                    warnings.Add(
                        $"Variable '{variable.Substring(0, 20)}...' has an unusually " +
                        $"long name ({variable.Length} chars). This could indicate obfuscation.");
                }
            }

            return warnings;
        }

        // ──────────────── Prompt Truncation ────────────────

        /// <summary>
        /// Truncates a prompt to fit within an estimated token limit,
        /// preserving content from the beginning and adding a truncation
        /// marker.
        /// </summary>
        /// <param name="prompt">The prompt to truncate.</param>
        /// <param name="maxTokens">
        /// Maximum estimated tokens. The result will be at or below
        /// this limit.
        /// </param>
        /// <param name="truncationMarker">
        /// Text to append when truncation occurs.
        /// Default: "\n\n[Content truncated due to length]"
        /// </param>
        /// <returns>
        /// The original prompt if within the limit, or a truncated version.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prompt"/> is null or empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="maxTokens"/> is less than 1.
        /// </exception>
        public static string TruncateToTokenLimit(
            string prompt,
            int maxTokens,
            string truncationMarker = "\n\n[Content truncated due to length]")
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException(
                    "Prompt cannot be null or empty.", nameof(prompt));
            if (maxTokens < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTokens),
                    maxTokens, "maxTokens must be at least 1.");

            if (EstimateTokens(prompt) <= maxTokens)
                return prompt;

            string marker = truncationMarker ?? "";
            int markerTokens = EstimateTokens(marker);
            int targetTokens = maxTokens - markerTokens;

            if (targetTokens < 1)
                return marker.Length > 0 ? marker : prompt.Substring(0, 1);

            // Binary search for the right character count
            int low = 0;
            int high = prompt.Length;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (EstimateTokens(prompt.Substring(0, mid)) <= targetTokens)
                    low = mid;
                else
                    high = mid - 1;
            }

            if (low == 0)
                return marker;

            // Try to break at a word or sentence boundary
            string truncated = prompt.Substring(0, low);
            int lastGoodBreak = -1;

            // Prefer sentence boundary
            int lastPeriod = truncated.LastIndexOf(". ");
            int lastNewline = truncated.LastIndexOf('\n');
            lastGoodBreak = Math.Max(lastPeriod, lastNewline);

            if (lastGoodBreak > low * 0.7)
                truncated = truncated.Substring(0, lastGoodBreak + 1);
            else
            {
                // Fall back to word boundary
                int lastSpace = truncated.LastIndexOf(' ');
                if (lastSpace > low * 0.8)
                    truncated = truncated.Substring(0, lastSpace);
            }

            return truncated.TrimEnd() + marker;
        }

        // ──────────────── Private Helpers ────────────────

        /// <summary>
        /// Generates warnings based on the analysis results.
        /// </summary>
        private static IReadOnlyList<string> GenerateWarnings(PromptAnalysis analysis)
        {
            var warnings = new List<string>();

            if (analysis.HasInjectionRisk)
            {
                warnings.Add(
                    "⚠️ Potential prompt injection detected. Review the prompt " +
                    "for malicious patterns before sending.");
            }

            if (analysis.ExceedsTokenLimit)
            {
                warnings.Add(
                    $"⚠️ Estimated tokens ({analysis.EstimatedTokens}) exceed " +
                    $"the limit ({analysis.TokenLimit}). The prompt may be " +
                    $"truncated by the model. Use TruncateToTokenLimit() to trim.");
            }

            if (analysis.WordCount < 3)
            {
                warnings.Add(
                    "⚠️ Prompt is very short (fewer than 3 words). This may " +
                    "produce generic or unhelpful responses.");
            }

            if (analysis.CharacterCount > 100_000)
            {
                warnings.Add(
                    "⚠️ Prompt is extremely long (>100K characters). Consider " +
                    "splitting into multiple requests or summarizing.");
            }

            return warnings;
        }

        /// <summary>
        /// Generates improvement suggestions based on prompt analysis.
        /// </summary>
        private static IReadOnlyList<string> GenerateSuggestions(
            string prompt, PromptAnalysis analysis)
        {
            var suggestions = new List<string>();

            if (!QuestionPattern.IsMatch(prompt))
            {
                suggestions.Add(
                    "💡 Consider starting with a clear instruction verb " +
                    "(Explain, List, Describe, Compare, etc.) or asking a question.");
            }

            if (!FormatRequestPattern.IsMatch(prompt) && analysis.WordCount > 10)
            {
                suggestions.Add(
                    "💡 Consider specifying the desired output format " +
                    "(JSON, list, table, etc.) for more structured responses.");
            }

            if (SpecificityPattern.Matches(prompt).Count == 0 && analysis.WordCount > 5)
            {
                suggestions.Add(
                    "💡 Adding specific details (numbers, examples, constraints) " +
                    "typically produces better results.");
            }

            int vagueCount = VaguePattern.Matches(prompt).Count;
            if (vagueCount > 2)
            {
                suggestions.Add(
                    "💡 The prompt contains several vague terms. Replacing " +
                    "'something', 'stuff', 'things', etc. with specific language " +
                    "improves response quality.");
            }

            if (analysis.WordCount > 20 && !StructurePattern.IsMatch(prompt))
            {
                suggestions.Add(
                    "💡 For longer prompts, consider using structure " +
                    "(bullet points, numbered steps, sections) to organize " +
                    "your instructions clearly.");
            }

            if (analysis.QualityScore >= 80 && suggestions.Count == 0)
            {
                suggestions.Add(
                    "✅ This prompt follows good prompt engineering practices!");
            }

            return suggestions;
        }
    }
}
