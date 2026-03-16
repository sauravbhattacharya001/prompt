namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Severity level for compatibility findings.
    /// </summary>
    public enum CompatibilitySeverity
    {
        /// <summary>Informational note — works everywhere but has nuances.</summary>
        Info,
        /// <summary>Warning — may behave differently across providers.</summary>
        Warning,
        /// <summary>Error — uses provider-specific features that won't work elsewhere.</summary>
        Error
    }

    /// <summary>
    /// A single compatibility finding for a prompt.
    /// </summary>
    public class CompatibilityFinding
    {
        /// <summary>Gets the finding identifier (e.g., "OPENAI_FUNCTION_CALL").</summary>
        public string RuleId { get; internal set; } = "";

        /// <summary>Gets the severity level.</summary>
        public CompatibilitySeverity Severity { get; internal set; }

        /// <summary>Gets the provider this finding is specific to.</summary>
        public string Provider { get; internal set; } = "";

        /// <summary>Gets a human-readable description of the issue.</summary>
        public string Message { get; internal set; } = "";

        /// <summary>Gets a suggested portable alternative.</summary>
        public string Suggestion { get; internal set; } = "";

        /// <summary>Gets the matched text that triggered the finding.</summary>
        public string MatchedText { get; internal set; } = "";

        /// <summary>Gets the category of the finding.</summary>
        public string Category { get; internal set; } = "";
    }

    /// <summary>
    /// Provider compatibility status for a prompt.
    /// </summary>
    public class ProviderCompatibility
    {
        /// <summary>Gets the provider name.</summary>
        public string Provider { get; internal set; } = "";

        /// <summary>Gets whether the prompt is fully compatible.</summary>
        public bool IsCompatible { get; internal set; }

        /// <summary>Gets the compatibility score (0–100).</summary>
        public int Score { get; internal set; }

        /// <summary>Gets findings specific to this provider.</summary>
        public List<CompatibilityFinding> Findings { get; internal set; } = new();
    }

    /// <summary>
    /// Full compatibility analysis result.
    /// </summary>
    public class CompatibilityReport
    {
        /// <summary>Gets the overall portability score (0–100).</summary>
        public int PortabilityScore { get; internal set; }

        /// <summary>Gets the portability grade (A–F).</summary>
        public string Grade { get; internal set; } = "";

        /// <summary>Gets all findings.</summary>
        public List<CompatibilityFinding> Findings { get; internal set; } = new();

        /// <summary>Gets per-provider compatibility summaries.</summary>
        public List<ProviderCompatibility> Providers { get; internal set; } = new();

        /// <summary>Gets the list of providers the prompt is fully compatible with.</summary>
        public List<string> CompatibleProviders { get; internal set; } = new();

        /// <summary>Gets the analyzed prompt text.</summary>
        public string PromptText { get; internal set; } = "";

        /// <summary>Gets the total number of errors.</summary>
        public int ErrorCount => Findings.Count(f => f.Severity == CompatibilitySeverity.Error);

        /// <summary>Gets the total number of warnings.</summary>
        public int WarningCount => Findings.Count(f => f.Severity == CompatibilitySeverity.Warning);

        /// <summary>Generates a text summary of the report.</summary>
        public string ToSummary()
        {
            var lines = new List<string>
            {
                $"Portability: {PortabilityScore}/100 (Grade {Grade})",
                $"Findings: {ErrorCount} errors, {WarningCount} warnings, {Findings.Count - ErrorCount - WarningCount} info",
                $"Compatible with: {(CompatibleProviders.Count > 0 ? string.Join(", ", CompatibleProviders) : "none fully")}",
                ""
            };

            foreach (var f in Findings.OrderByDescending(f => f.Severity))
            {
                var icon = f.Severity == CompatibilitySeverity.Error ? "✗" :
                           f.Severity == CompatibilitySeverity.Warning ? "⚠" : "ℹ";
                lines.Add($"  {icon} [{f.RuleId}] {f.Message}");
                if (!string.IsNullOrEmpty(f.Suggestion))
                    lines.Add($"    → {f.Suggestion}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Analyzes prompts for cross-provider compatibility across major LLM providers
    /// (OpenAI, Anthropic, Google, Meta, Mistral, Cohere). Detects provider-specific
    /// syntax, role conventions, token patterns, and formatting assumptions.
    /// Suggests portable alternatives for flagged issues.
    /// </summary>
    public class PromptCompatibilityChecker
    {
        private static readonly string[] AllProviders = { "OpenAI", "Anthropic", "Google", "Meta", "Mistral", "Cohere" };

        private readonly List<CompatibilityRule> _rules;

        /// <summary>
        /// Initializes a new instance with the default rule set.
        /// </summary>
        public PromptCompatibilityChecker()
        {
            _rules = BuildDefaultRules();
        }

        /// <summary>
        /// Analyze a prompt for cross-provider compatibility.
        /// </summary>
        /// <param name="promptText">The prompt text to analyze.</param>
        /// <returns>A detailed compatibility report.</returns>
        public CompatibilityReport Analyze(string promptText)
        {
            if (string.IsNullOrEmpty(promptText))
                return new CompatibilityReport
                {
                    PortabilityScore = 100,
                    Grade = "A",
                    PromptText = promptText ?? "",
                    CompatibleProviders = new List<string>(AllProviders)
                };

            var findings = new List<CompatibilityFinding>();

            foreach (var rule in _rules)
            {
                var matches = rule.Detect(promptText);
                findings.AddRange(matches);
            }

            // Deduplicate by RuleId
            findings = findings
                .GroupBy(f => f.RuleId + "|" + f.MatchedText)
                .Select(g => g.First())
                .ToList();

            // Build per-provider summaries
            var providers = AllProviders.Select(p =>
            {
                var providerFindings = findings.Where(f =>
                    string.IsNullOrEmpty(f.Provider) || f.Provider == p ||
                    f.Provider == "non-" + p).ToList();

                var errors = providerFindings.Count(f => f.Severity == CompatibilitySeverity.Error);
                var warnings = providerFindings.Count(f => f.Severity == CompatibilitySeverity.Warning);

                var score = Math.Max(0, 100 - errors * 25 - warnings * 10);

                return new ProviderCompatibility
                {
                    Provider = p,
                    IsCompatible = errors == 0,
                    Score = score,
                    Findings = providerFindings
                };
            }).ToList();

            var compatible = providers.Where(p => p.IsCompatible).Select(p => p.Provider).ToList();

            // Overall portability = average of provider scores
            var portability = providers.Count > 0 ? (int)providers.Average(p => p.Score) : 100;

            return new CompatibilityReport
            {
                PortabilityScore = portability,
                Grade = ScoreToGrade(portability),
                Findings = findings,
                Providers = providers,
                CompatibleProviders = compatible,
                PromptText = promptText
            };
        }

        /// <summary>
        /// Check if a prompt is portable (no errors for any provider).
        /// </summary>
        public bool IsPortable(string promptText) => Analyze(promptText).ErrorCount == 0;

        /// <summary>
        /// Get providers a prompt is compatible with.
        /// </summary>
        public List<string> GetCompatibleProviders(string promptText) => Analyze(promptText).CompatibleProviders;

        private static string ScoreToGrade(int score) => score switch
        {
            >= 95 => "A",
            >= 85 => "B",
            >= 70 => "C",
            >= 55 => "D",
            _ => "F"
        };

        private static List<CompatibilityRule> BuildDefaultRules()
        {
            return new List<CompatibilityRule>
            {
                // OpenAI-specific patterns
                new RegexRule("OPENAI_FUNCTION_SYNTAX",
                    @"\bfunctions?\s*[:=]\s*\[",
                    CompatibilitySeverity.Error, "OpenAI", "Function Calling",
                    "Uses OpenAI-specific function calling syntax.",
                    "Use a provider-agnostic tool description format or abstract behind a tool layer."),

                new RegexRule("OPENAI_GPT_REFERENCE",
                    @"\b(gpt-[34][\.o]?\w*|text-davinci|chatgpt)\b",
                    CompatibilitySeverity.Warning, "OpenAI", "Model Reference",
                    "References OpenAI-specific model names.",
                    "Use generic capability descriptions instead of model names (e.g., 'a capable language model')."),

                new RegexRule("OPENAI_SYSTEM_PREFIX",
                    @"(?i)\[system\]|\bsystem\s*message\s*:",
                    CompatibilitySeverity.Warning, "OpenAI", "Role Format",
                    "Uses OpenAI-style system message markers.",
                    "Most providers support system roles, but syntax varies. Use your API's native role field."),

                new RegexRule("OPENAI_RESPONSE_FORMAT",
                    @"(?i)\bresponse_format\b.*\bjson_object\b",
                    CompatibilitySeverity.Error, "OpenAI", "API Feature",
                    "References OpenAI's response_format JSON mode.",
                    "Request JSON output in the prompt text itself: 'Respond with valid JSON only.'"),

                // Anthropic-specific patterns
                new RegexRule("ANTHROPIC_XML_TAGS",
                    @"<(thinking|artifact|result|answer|scratchpad|inner_monologue)>",
                    CompatibilitySeverity.Warning, "Anthropic", "XML Convention",
                    "Uses XML tags commonly associated with Anthropic/Claude prompting conventions.",
                    "XML tags work in most models but may not be specially handled. Consider using markdown headers instead."),

                new RegexRule("ANTHROPIC_HUMAN_ASSISTANT",
                    @"(?m)^\s*\\n\\n(Human|Assistant):",
                    CompatibilitySeverity.Error, "Anthropic", "Role Format",
                    "Uses Anthropic's legacy Human:/Assistant: turn format.",
                    "Use the API's native message roles instead of text-based turn markers."),

                new RegexRule("ANTHROPIC_CLAUDE_REF",
                    @"\b(claude|claude-[123]\w*|anthropic)\b",
                    CompatibilitySeverity.Warning, "Anthropic", "Model Reference",
                    "References Anthropic-specific model or company names.",
                    "Use generic descriptions instead of provider-specific model names."),

                // Google-specific patterns
                new RegexRule("GOOGLE_GEMINI_REF",
                    @"\b(gemini|palm|bard|google\s+ai)\b",
                    CompatibilitySeverity.Warning, "Google", "Model Reference",
                    "References Google-specific model or product names.",
                    "Use generic model capability descriptions."),

                new RegexRule("GOOGLE_SAFETY_SETTINGS",
                    @"(?i)\bsafety_settings\b|\bharm_category\b",
                    CompatibilitySeverity.Error, "Google", "API Feature",
                    "References Google's safety settings API parameters.",
                    "Handle safety configuration at the API layer, not in prompt text."),

                // General cross-provider issues
                new RegexRule("TOKEN_LIMIT_ASSUMPTION",
                    @"(?i)\b(4096|8192|16384|32768|128k|200k)\s*(token|context|window)\b",
                    CompatibilitySeverity.Warning, "", "Token Limits",
                    "Assumes specific token/context limits that vary across providers.",
                    "Avoid hardcoding token limits. Use dynamic context window detection."),

                new RegexRule("MARKDOWN_IMAGE",
                    @"!\[.*?\]\(.*?\)",
                    CompatibilitySeverity.Info, "", "Multimodal",
                    "Contains markdown image syntax. Image handling varies across providers.",
                    "Use the provider's native multimodal API for image inputs."),

                new RegexRule("JSON_MODE_REQUEST",
                    @"(?i)\b(respond|reply|output|return)\s+(only\s+)?(in|with|as)\s+(valid\s+)?json\b",
                    CompatibilitySeverity.Info, "", "Output Format",
                    "Requests JSON output. Reliability of structured output varies by model.",
                    "Consider adding a JSON schema example to improve output consistency across providers."),

                new RegexRule("SYSTEM_PROMPT_IN_TEXT",
                    @"(?i)^(you are|act as|pretend to be|your role is)\b",
                    CompatibilitySeverity.Info, "", "Prompt Style",
                    "Uses in-text system prompt pattern. All providers support this but behavior varies.",
                    "For best results, use the API's dedicated system/instruction field."),

                new RegexRule("TOOL_USE_GENERIC",
                    @"(?i)\btool_use\b|\btool_calls?\b|\bfunction_call\b",
                    CompatibilitySeverity.Warning, "", "Tool Use",
                    "References tool/function calling. Implementations differ significantly across providers.",
                    "Abstract tool calling behind a provider-agnostic interface."),

                new RegexRule("LONG_PROMPT",
                    new LongPromptDetector(),
                    CompatibilitySeverity.Warning, "", "Length",
                    "Prompt exceeds 4000 tokens (estimated). Shorter-context models may truncate.",
                    "Consider splitting into smaller prompts or using summarization for context."),

                new RegexRule("MULTI_LANGUAGE_MIX",
                    new MultiLanguageDetector(),
                    CompatibilitySeverity.Info, "", "Multilingual",
                    "Prompt mixes multiple scripts/languages. Some models handle multilingual content better.",
                    "Test with target models. Consider separating instructions (English) from content (other language)."),

                new RegexRule("META_LLAMA_REF",
                    @"\b(llama|llama[-\s]?[23]\w*|meta\s+ai)\b",
                    CompatibilitySeverity.Warning, "Meta", "Model Reference",
                    "References Meta-specific model names.",
                    "Use generic capability descriptions instead of model names."),

                new RegexRule("MISTRAL_REF",
                    @"\b(mistral|mixtral|mistral[-\s]?\w+)\b",
                    CompatibilitySeverity.Warning, "Mistral", "Model Reference",
                    "References Mistral-specific model names.",
                    "Use generic capability descriptions instead of model names."),

                new RegexRule("COHERE_COMMAND_REF",
                    @"\b(command[-\s]?r?\+?|cohere)\b",
                    CompatibilitySeverity.Warning, "Cohere", "Model Reference",
                    "References Cohere-specific model names.",
                    "Use generic capability descriptions instead of model names."),

                new RegexRule("SPECIAL_TOKENS",
                    @"<\|?(im_start|im_end|endoftext|sep|pad|eos|bos)\|?>",
                    CompatibilitySeverity.Error, "", "Special Tokens",
                    "Contains model-specific special tokens that will be treated as plain text by other models.",
                    "Remove special tokens. Use the API's native message structure instead."),

                new RegexRule("CHAT_ML_FORMAT",
                    @"<\|im_start\|>(system|user|assistant)",
                    CompatibilitySeverity.Error, "", "Chat Format",
                    "Uses ChatML format markers. This is a low-level format not portable across providers.",
                    "Use the API's native chat/message format instead of raw ChatML."),
            };
        }

        private class CompatibilityRule
        {
            public virtual List<CompatibilityFinding> Detect(string text) => new();
        }

        private class RegexRule : CompatibilityRule
        {
            private readonly string _ruleId;
            private readonly Regex? _regex;
            private readonly ICustomDetector? _detector;
            private readonly CompatibilitySeverity _severity;
            private readonly string _provider;
            private readonly string _category;
            private readonly string _message;
            private readonly string _suggestion;

            public RegexRule(string ruleId, string pattern,
                CompatibilitySeverity severity, string provider, string category,
                string message, string suggestion)
            {
                _ruleId = ruleId;
                _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                _severity = severity;
                _provider = provider;
                _category = category;
                _message = message;
                _suggestion = suggestion;
            }

            public RegexRule(string ruleId, ICustomDetector detector,
                CompatibilitySeverity severity, string provider, string category,
                string message, string suggestion)
            {
                _ruleId = ruleId;
                _detector = detector;
                _severity = severity;
                _provider = provider;
                _category = category;
                _message = message;
                _suggestion = suggestion;
            }

            public override List<CompatibilityFinding> Detect(string text)
            {
                var findings = new List<CompatibilityFinding>();

                if (_detector != null)
                {
                    if (_detector.IsMatch(text))
                    {
                        findings.Add(new CompatibilityFinding
                        {
                            RuleId = _ruleId,
                            Severity = _severity,
                            Provider = _provider,
                            Category = _category,
                            Message = _message,
                            Suggestion = _suggestion,
                            MatchedText = _detector.GetMatchedText(text)
                        });
                    }
                    return findings;
                }

                if (_regex == null) return findings;

                var match = _regex.Match(text);
                if (match.Success)
                {
                    findings.Add(new CompatibilityFinding
                    {
                        RuleId = _ruleId,
                        Severity = _severity,
                        Provider = _provider,
                        Category = _category,
                        Message = _message,
                        Suggestion = _suggestion,
                        MatchedText = match.Value
                    });
                }

                return findings;
            }
        }

        private interface ICustomDetector
        {
            bool IsMatch(string text);
            string GetMatchedText(string text);
        }

        private class LongPromptDetector : ICustomDetector
        {
            public bool IsMatch(string text) => EstimateTokens(text) > 4000;
            public string GetMatchedText(string text) => $"~{EstimateTokens(text)} estimated tokens";
            private static int EstimateTokens(string text) => (int)(text.Length / 3.5);
        }

        private class MultiLanguageDetector : ICustomDetector
        {
            private static readonly Regex CjkPattern = new(@"[\u4e00-\u9fff\u3040-\u309f\u30a0-\u30ff\uac00-\ud7af]", RegexOptions.Compiled);
            private static readonly Regex CyrillicPattern = new(@"[\u0400-\u04ff]", RegexOptions.Compiled);
            private static readonly Regex ArabicPattern = new(@"[\u0600-\u06ff]", RegexOptions.Compiled);
            private static readonly Regex LatinPattern = new(@"[a-zA-Z]{3,}", RegexOptions.Compiled);

            public bool IsMatch(string text)
            {
                int scripts = 0;
                if (LatinPattern.IsMatch(text)) scripts++;
                if (CjkPattern.IsMatch(text)) scripts++;
                if (CyrillicPattern.IsMatch(text)) scripts++;
                if (ArabicPattern.IsMatch(text)) scripts++;
                return scripts >= 2;
            }

            public string GetMatchedText(string text) => "Multiple scripts detected";
        }
    }
}
