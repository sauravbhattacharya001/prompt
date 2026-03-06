namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ───────────────────────── Enums & DTOs ─────────────────────────

    /// <summary>LLM provider/model family identifier.</summary>
    public enum LlmProvider
    {
        /// <summary>OpenAI GPT family.</summary>
        OpenAI,
        /// <summary>Anthropic Claude family.</summary>
        Anthropic,
        /// <summary>Google Gemini family.</summary>
        Google,
        /// <summary>Meta Llama family.</summary>
        Meta,
        /// <summary>Mistral AI family.</summary>
        Mistral,
        /// <summary>Generic/unknown provider.</summary>
        Generic
    }

    /// <summary>Severity of a migration issue.</summary>
    public enum MigrationSeverity
    {
        /// <summary>Informational, may or may not need changes.</summary>
        Info,
        /// <summary>Should address for best results.</summary>
        Warning,
        /// <summary>Likely to cause problems if not addressed.</summary>
        Error
    }

    /// <summary>Category of migration issue.</summary>
    public enum MigrationCategory
    {
        /// <summary>Provider-specific formatting or syntax.</summary>
        Formatting,
        /// <summary>Token/context window limits.</summary>
        TokenLimit,
        /// <summary>Provider-specific system prompt patterns.</summary>
        SystemPrompt,
        /// <summary>Role or persona conventions.</summary>
        RoleConvention,
        /// <summary>Output format differences.</summary>
        OutputFormat,
        /// <summary>Feature not supported by target.</summary>
        UnsupportedFeature,
        /// <summary>Safety/content filter differences.</summary>
        SafetyFilter,
        /// <summary>API-specific syntax.</summary>
        ApiSyntax,
        /// <summary>Temperature/sampling parameter conventions.</summary>
        ParameterConvention,
        /// <summary>Tool/function calling syntax.</summary>
        ToolCalling
    }

    /// <summary>A single migration issue with source context and fix suggestion.</summary>
    public class MigrationIssue
    {
        [JsonPropertyName("category")]
        public MigrationCategory Category { get; init; }

        [JsonPropertyName("severity")]
        public MigrationSeverity Severity { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        [JsonPropertyName("original")]
        public string Original { get; init; } = "";

        [JsonPropertyName("suggestion")]
        public string Suggestion { get; init; } = "";

        [JsonPropertyName("line")]
        public int Line { get; init; }

        [JsonPropertyName("autoFixable")]
        public bool AutoFixable { get; init; }
    }

    /// <summary>Result of provider detection on a prompt.</summary>
    public class ProviderDetection
    {
        [JsonPropertyName("provider")]
        public LlmProvider Provider { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }

        [JsonPropertyName("signals")]
        public List<string> Signals { get; init; } = new();
    }

    /// <summary>Provider context window and capability information.</summary>
    public class ProviderProfile
    {
        [JsonPropertyName("provider")]
        public LlmProvider Provider { get; init; }

        [JsonPropertyName("maxContextTokens")]
        public int MaxContextTokens { get; init; }

        [JsonPropertyName("supportsSystemPrompt")]
        public bool SupportsSystemPrompt { get; init; }

        [JsonPropertyName("supportsToolCalling")]
        public bool SupportsToolCalling { get; init; }

        [JsonPropertyName("supportsImages")]
        public bool SupportsImages { get; init; }

        [JsonPropertyName("supportsJson")]
        public bool SupportsJson { get; init; }

        [JsonPropertyName("defaultTemperature")]
        public double DefaultTemperature { get; init; }

        [JsonPropertyName("preferredRoleStyle")]
        public string PreferredRoleStyle { get; init; } = "";

        [JsonPropertyName("notableConventions")]
        public List<string> NotableConventions { get; init; } = new();
    }

    /// <summary>Full migration analysis report.</summary>
    public class MigrationReport
    {
        [JsonPropertyName("sourceProvider")]
        public LlmProvider SourceProvider { get; init; }

        [JsonPropertyName("targetProvider")]
        public LlmProvider TargetProvider { get; init; }

        [JsonPropertyName("originalPrompt")]
        public string OriginalPrompt { get; init; } = "";

        [JsonPropertyName("migratedPrompt")]
        public string MigratedPrompt { get; init; } = "";

        [JsonPropertyName("issues")]
        public List<MigrationIssue> Issues { get; init; } = new();

        [JsonPropertyName("compatibilityScore")]
        public int CompatibilityScore { get; init; }

        [JsonPropertyName("grade")]
        public string Grade { get; init; } = "";

        [JsonPropertyName("issueCount")]
        public int IssueCount => Issues.Count;

        [JsonPropertyName("autoFixCount")]
        public int AutoFixCount => Issues.Count(i => i.AutoFixable);

        [JsonPropertyName("errorCount")]
        public int ErrorCount => Issues.Count(i => i.Severity == MigrationSeverity.Error);

        [JsonPropertyName("warningCount")]
        public int WarningCount => Issues.Count(i => i.Severity == MigrationSeverity.Warning);

        [JsonPropertyName("tokenEstimate")]
        public int TokenEstimate { get; init; }

        [JsonPropertyName("sourceProfile")]
        public ProviderProfile? SourceProfile { get; init; }

        [JsonPropertyName("targetProfile")]
        public ProviderProfile? TargetProfile { get; init; }

        /// <summary>Generate a human-readable text report.</summary>
        public string ToTextReport()
        {
            var lines = new List<string>
            {
                $"Prompt Migration Report: {SourceProvider} → {TargetProvider}",
                new string('=', 50),
                $"Compatibility: {CompatibilityScore}/100 (Grade: {Grade})",
                $"Issues: {IssueCount} ({ErrorCount} errors, {WarningCount} warnings)",
                $"Auto-fixable: {AutoFixCount}/{IssueCount}",
                $"Token estimate: ~{TokenEstimate}",
                ""
            };

            if (Issues.Count > 0)
            {
                lines.Add("Issues:");
                lines.Add(new string('-', 40));
                foreach (var issue in Issues.OrderByDescending(i => i.Severity))
                {
                    var sev = issue.Severity == MigrationSeverity.Error ? "ERROR"
                        : issue.Severity == MigrationSeverity.Warning ? "WARN" : "INFO";
                    lines.Add($"  [{sev}] {issue.Category}: {issue.Description}");
                    if (!string.IsNullOrEmpty(issue.Suggestion))
                        lines.Add($"         → {issue.Suggestion}");
                }
            }

            if (MigratedPrompt != OriginalPrompt)
            {
                lines.Add("");
                lines.Add("Migrated Prompt:");
                lines.Add(new string('-', 40));
                lines.Add(MigratedPrompt);
            }

            return string.Join("\n", lines);
        }
    }

    /// <summary>Result of batch migration analysis.</summary>
    public class BatchMigrationReport
    {
        [JsonPropertyName("source")]
        public LlmProvider Source { get; init; }

        [JsonPropertyName("target")]
        public LlmProvider Target { get; init; }

        [JsonPropertyName("reports")]
        public List<MigrationReport> Reports { get; init; } = new();

        [JsonPropertyName("totalPrompts")]
        public int TotalPrompts => Reports.Count;

        [JsonPropertyName("averageScore")]
        public double AverageScore => Reports.Count > 0
            ? Reports.Average(r => r.CompatibilityScore) : 0;

        [JsonPropertyName("totalIssues")]
        public int TotalIssues => Reports.Sum(r => r.IssueCount);

        [JsonPropertyName("totalAutoFixable")]
        public int TotalAutoFixable => Reports.Sum(r => r.AutoFixCount);

        [JsonPropertyName("categoryBreakdown")]
        public Dictionary<string, int> CategoryBreakdown =>
            Reports.SelectMany(r => r.Issues)
                .GroupBy(i => i.Category.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

        [JsonPropertyName("readyCount")]
        public int ReadyCount => Reports.Count(r => r.CompatibilityScore >= 80);

        [JsonPropertyName("needsWorkCount")]
        public int NeedsWorkCount => Reports.Count(r =>
            r.CompatibilityScore >= 50 && r.CompatibilityScore < 80);

        [JsonPropertyName("problematicCount")]
        public int ProblematicCount => Reports.Count(r => r.CompatibilityScore < 50);
    }

    // ───────────────────────── Main Service ─────────────────────────

    /// <summary>
    /// Analyzes prompts for provider-specific patterns and assists migration
    /// between LLM providers. Detects source provider, identifies compatibility
    /// issues, suggests fixes, and auto-migrates where possible.
    /// </summary>
    public class PromptMigrationAssistant
    {
        private static readonly Dictionary<LlmProvider, ProviderProfile> Profiles = new()
        {
            [LlmProvider.OpenAI] = new ProviderProfile
            {
                Provider = LlmProvider.OpenAI,
                MaxContextTokens = 128000,
                SupportsSystemPrompt = true,
                SupportsToolCalling = true,
                SupportsImages = true,
                SupportsJson = true,
                DefaultTemperature = 1.0,
                PreferredRoleStyle = "You are a helpful assistant.",
                NotableConventions = new()
                {
                    "Uses system/user/assistant message roles",
                    "JSON mode via response_format",
                    "Function calling with tools array",
                    "Seed parameter for reproducibility"
                }
            },
            [LlmProvider.Anthropic] = new ProviderProfile
            {
                Provider = LlmProvider.Anthropic,
                MaxContextTokens = 200000,
                SupportsSystemPrompt = true,
                SupportsToolCalling = true,
                SupportsImages = true,
                SupportsJson = true,
                DefaultTemperature = 1.0,
                PreferredRoleStyle = "You are Claude, an AI assistant made by Anthropic.",
                NotableConventions = new()
                {
                    "System prompt is a top-level parameter, not a message",
                    "Prefers Human/Assistant turn markers in few-shot",
                    "XML tags for structured sections",
                    "Thinking blocks for chain-of-thought"
                }
            },
            [LlmProvider.Google] = new ProviderProfile
            {
                Provider = LlmProvider.Google,
                MaxContextTokens = 1000000,
                SupportsSystemPrompt = true,
                SupportsToolCalling = true,
                SupportsImages = true,
                SupportsJson = true,
                DefaultTemperature = 1.0,
                PreferredRoleStyle = "You are a helpful AI assistant powered by Gemini.",
                NotableConventions = new()
                {
                    "Uses system_instruction for system prompts",
                    "Supports grounding with Google Search",
                    "Safety settings per harm category",
                    "Supports code execution tool"
                }
            },
            [LlmProvider.Meta] = new ProviderProfile
            {
                Provider = LlmProvider.Meta,
                MaxContextTokens = 128000,
                SupportsSystemPrompt = true,
                SupportsToolCalling = true,
                SupportsImages = true,
                SupportsJson = false,
                DefaultTemperature = 0.6,
                PreferredRoleStyle = "You are a helpful, respectful, and honest assistant.",
                NotableConventions = new()
                {
                    "Uses [INST] and [/INST] markers",
                    "<<SYS>> tags for system prompts in raw format",
                    "No native JSON mode (must instruct in prompt)",
                    "Safety system prompt prepended by default"
                }
            },
            [LlmProvider.Mistral] = new ProviderProfile
            {
                Provider = LlmProvider.Mistral,
                MaxContextTokens = 128000,
                SupportsSystemPrompt = true,
                SupportsToolCalling = true,
                SupportsImages = true,
                SupportsJson = true,
                DefaultTemperature = 0.7,
                PreferredRoleStyle = "You are a helpful AI assistant.",
                NotableConventions = new()
                {
                    "Uses [INST] and [/INST] markers in raw format",
                    "Safe mode adds safety preamble",
                    "JSON mode via response_format",
                    "Supports function calling"
                }
            },
            [LlmProvider.Generic] = new ProviderProfile
            {
                Provider = LlmProvider.Generic,
                MaxContextTokens = 8000,
                SupportsSystemPrompt = true,
                SupportsToolCalling = false,
                SupportsImages = false,
                SupportsJson = false,
                DefaultTemperature = 0.7,
                PreferredRoleStyle = "You are a helpful assistant.",
                NotableConventions = new() { "Basic chat completion" }
            }
        };

        // Provider detection patterns
        private static readonly (Regex Pattern, LlmProvider Provider, double Weight, string Signal)[] DetectionPatterns =
        {
            // OpenAI signals
            (new Regex(@"\bChatGPT\b", RegexOptions.IgnoreCase), LlmProvider.OpenAI, 0.8, "References ChatGPT"),
            (new Regex(@"\bGPT-?[34]\b", RegexOptions.IgnoreCase), LlmProvider.OpenAI, 0.9, "References GPT model"),
            (new Regex(@"\bOpenAI\b", RegexOptions.IgnoreCase), LlmProvider.OpenAI, 0.7, "References OpenAI"),
            (new Regex(@"response_format.*json", RegexOptions.IgnoreCase), LlmProvider.OpenAI, 0.6, "OpenAI JSON mode syntax"),
            (new Regex(@"\bDALL[·\-]?E\b", RegexOptions.IgnoreCase), LlmProvider.OpenAI, 0.7, "References DALL-E"),
            (new Regex(@"\bseed\s*[:=]\s*\d+", RegexOptions.IgnoreCase), LlmProvider.OpenAI, 0.3, "Seed parameter (OpenAI convention)"),

            // Anthropic signals
            (new Regex(@"\bClaude\b", RegexOptions.IgnoreCase), LlmProvider.Anthropic, 0.9, "References Claude"),
            (new Regex(@"\bAnthropic\b", RegexOptions.IgnoreCase), LlmProvider.Anthropic, 0.8, "References Anthropic"),
            (new Regex(@"\bHuman:\s", RegexOptions.None), LlmProvider.Anthropic, 0.7, "Uses Human: turn marker"),
            (new Regex(@"\bAssistant:\s", RegexOptions.None), LlmProvider.Anthropic, 0.5, "Uses Assistant: turn marker"),
            (new Regex(@"<thinking>", RegexOptions.IgnoreCase), LlmProvider.Anthropic, 0.6, "Uses thinking XML tags"),
            (new Regex(@"</?(?:instructions|context|example|output|document|user_input|response)>", RegexOptions.IgnoreCase), LlmProvider.Anthropic, 0.5, "Uses XML section tags (Anthropic convention)"),

            // Google signals
            (new Regex(@"\bGemini\b", RegexOptions.IgnoreCase), LlmProvider.Google, 0.9, "References Gemini"),
            (new Regex(@"\bGoogle\s+AI\b", RegexOptions.IgnoreCase), LlmProvider.Google, 0.7, "References Google AI"),
            (new Regex(@"\bsystem_instruction\b", RegexOptions.IgnoreCase), LlmProvider.Google, 0.6, "Uses system_instruction"),
            (new Regex(@"\bharm_category\b", RegexOptions.IgnoreCase), LlmProvider.Google, 0.5, "References harm categories"),

            // Meta/Llama signals
            (new Regex(@"\bLlama\s*[23]?\b", RegexOptions.IgnoreCase), LlmProvider.Meta, 0.8, "References Llama model"),
            (new Regex(@"\[INST\]", RegexOptions.None), LlmProvider.Meta, 0.7, "Uses [INST] markers"),
            (new Regex(@"<<SYS>>", RegexOptions.None), LlmProvider.Meta, 0.9, "Uses <<SYS>> system tags"),
            (new Regex(@"\[/INST\]", RegexOptions.None), LlmProvider.Meta, 0.7, "Uses [/INST] markers"),

            // Mistral signals
            (new Regex(@"\bMistral\b", RegexOptions.IgnoreCase), LlmProvider.Mistral, 0.8, "References Mistral"),
            (new Regex(@"\bMixtral\b", RegexOptions.IgnoreCase), LlmProvider.Mistral, 0.7, "References Mixtral"),
        };

        // Migration rules: (source, target, pattern, replacement_fn, category, severity, description)
        private record MigrationRule(
            LlmProvider? Source,
            LlmProvider Target,
            Regex Pattern,
            Func<Match, string> Replace,
            MigrationCategory Category,
            MigrationSeverity Severity,
            string Description
        );

        private static readonly List<MigrationRule> MigrationRules = new()
        {
            // OpenAI → Anthropic: ChatGPT references
            new(LlmProvider.OpenAI, LlmProvider.Anthropic,
                new Regex(@"\bChatGPT\b", RegexOptions.IgnoreCase),
                _ => "Claude", MigrationCategory.RoleConvention, MigrationSeverity.Warning,
                "Replace ChatGPT references with Claude"),

            // OpenAI → Anthropic: GPT model references
            new(LlmProvider.OpenAI, LlmProvider.Anthropic,
                new Regex(@"\bGPT-?[34][^\s]*\b", RegexOptions.IgnoreCase),
                _ => "Claude", MigrationCategory.RoleConvention, MigrationSeverity.Info,
                "Replace GPT model references with Claude"),

            // OpenAI → Google: ChatGPT references
            new(LlmProvider.OpenAI, LlmProvider.Google,
                new Regex(@"\bChatGPT\b", RegexOptions.IgnoreCase),
                _ => "Gemini", MigrationCategory.RoleConvention, MigrationSeverity.Warning,
                "Replace ChatGPT references with Gemini"),

            // Anthropic → OpenAI: Claude references
            new(LlmProvider.Anthropic, LlmProvider.OpenAI,
                new Regex(@"\bClaude\b"), _ => "ChatGPT",
                MigrationCategory.RoleConvention, MigrationSeverity.Warning,
                "Replace Claude references with ChatGPT"),

            // Anthropic → Google: Claude references
            new(LlmProvider.Anthropic, LlmProvider.Google,
                new Regex(@"\bClaude\b"), _ => "Gemini",
                MigrationCategory.RoleConvention, MigrationSeverity.Warning,
                "Replace Claude references with Gemini"),

            // Google → OpenAI: Gemini references
            new(LlmProvider.Google, LlmProvider.OpenAI,
                new Regex(@"\bGemini\b", RegexOptions.IgnoreCase), _ => "ChatGPT",
                MigrationCategory.RoleConvention, MigrationSeverity.Warning,
                "Replace Gemini references with ChatGPT"),

            // Google → Anthropic: Gemini references
            new(LlmProvider.Google, LlmProvider.Anthropic,
                new Regex(@"\bGemini\b", RegexOptions.IgnoreCase), _ => "Claude",
                MigrationCategory.RoleConvention, MigrationSeverity.Warning,
                "Replace Gemini references with Claude"),

            // Meta → OpenAI: [INST] markers
            new(LlmProvider.Meta, LlmProvider.OpenAI,
                new Regex(@"\[/?INST\]"), _ => "",
                MigrationCategory.Formatting, MigrationSeverity.Warning,
                "Remove [INST]/[/INST] markers (not used by OpenAI)"),

            // Meta → Anthropic: [INST] markers
            new(LlmProvider.Meta, LlmProvider.Anthropic,
                new Regex(@"\[/?INST\]"), _ => "",
                MigrationCategory.Formatting, MigrationSeverity.Warning,
                "Remove [INST]/[/INST] markers (not used by Anthropic)"),

            // Meta → Google: [INST] markers
            new(LlmProvider.Meta, LlmProvider.Google,
                new Regex(@"\[/?INST\]"), _ => "",
                MigrationCategory.Formatting, MigrationSeverity.Warning,
                "Remove [INST]/[/INST] markers (not used by Google)"),

            // Meta → *: <<SYS>> markers
            new(LlmProvider.Meta, LlmProvider.OpenAI,
                new Regex(@"<</?SYS>>"), _ => "",
                MigrationCategory.SystemPrompt, MigrationSeverity.Warning,
                "Remove <<SYS>> markers (use system message role instead)"),
            new(LlmProvider.Meta, LlmProvider.Anthropic,
                new Regex(@"<</?SYS>>"), _ => "",
                MigrationCategory.SystemPrompt, MigrationSeverity.Warning,
                "Remove <<SYS>> markers (use system parameter instead)"),
            new(LlmProvider.Meta, LlmProvider.Google,
                new Regex(@"<</?SYS>>"), _ => "",
                MigrationCategory.SystemPrompt, MigrationSeverity.Warning,
                "Remove <<SYS>> markers (use system_instruction instead)"),

            // Anthropic → OpenAI: Human/Assistant markers
            new(LlmProvider.Anthropic, LlmProvider.OpenAI,
                new Regex(@"^Human:\s?", RegexOptions.Multiline), _ => "User: ",
                MigrationCategory.RoleConvention, MigrationSeverity.Info,
                "Replace Human: with User: (OpenAI convention)"),

            // Any → Meta: need [INST] markers
            new(null, LlmProvider.Meta,
                new Regex(@"^(?!\[INST\])(.+)", RegexOptions.None),
                _ => _.Value, // no auto-fix, just flag
                MigrationCategory.Formatting, MigrationSeverity.Info,
                "Consider wrapping instructions in [INST][/INST] markers for Llama"),

            // Anthropic → *: Anthropic-specific references
            new(LlmProvider.Anthropic, LlmProvider.OpenAI,
                new Regex(@"\bAnthropic\b"), _ => "OpenAI",
                MigrationCategory.RoleConvention, MigrationSeverity.Info,
                "Replace Anthropic company references"),
            new(LlmProvider.Anthropic, LlmProvider.Google,
                new Regex(@"\bAnthropic\b"), _ => "Google",
                MigrationCategory.RoleConvention, MigrationSeverity.Info,
                "Replace Anthropic company references"),

            // OpenAI → *: OpenAI-specific references
            new(LlmProvider.OpenAI, LlmProvider.Anthropic,
                new Regex(@"\bOpenAI\b"), _ => "Anthropic",
                MigrationCategory.RoleConvention, MigrationSeverity.Info,
                "Replace OpenAI company references"),
            new(LlmProvider.OpenAI, LlmProvider.Google,
                new Regex(@"\bOpenAI\b"), _ => "Google",
                MigrationCategory.RoleConvention, MigrationSeverity.Info,
                "Replace OpenAI company references"),
        };

        /// <summary>Get the profile for a provider.</summary>
        public ProviderProfile GetProfile(LlmProvider provider)
        {
            return Profiles.TryGetValue(provider, out var profile)
                ? profile : Profiles[LlmProvider.Generic];
        }

        /// <summary>Detect which LLM provider a prompt was likely written for.</summary>
        public ProviderDetection DetectProvider(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var scores = new Dictionary<LlmProvider, double>();
            var signals = new Dictionary<LlmProvider, List<string>>();

            foreach (var provider in Enum.GetValues<LlmProvider>())
            {
                scores[provider] = 0;
                signals[provider] = new List<string>();
            }

            foreach (var (pattern, provider, weight, signal) in DetectionPatterns)
            {
                if (pattern.IsMatch(prompt))
                {
                    scores[provider] += weight;
                    signals[provider].Add(signal);
                }
            }

            // Normalize scores
            var maxScore = scores.Values.Max();
            if (maxScore <= 0)
            {
                return new ProviderDetection
                {
                    Provider = LlmProvider.Generic,
                    Confidence = 0.0,
                    Signals = new List<string> { "No provider-specific patterns detected" }
                };
            }

            var bestProvider = scores
                .OrderByDescending(kv => kv.Value)
                .First();

            var confidence = Math.Min(1.0, bestProvider.Value / 2.0);

            return new ProviderDetection
            {
                Provider = bestProvider.Key,
                Confidence = Math.Round(confidence, 2),
                Signals = signals[bestProvider.Key]
            };
        }

        /// <summary>Analyze compatibility issues when migrating between providers.</summary>
        public MigrationReport Analyze(string prompt, LlmProvider source, LlmProvider target)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            if (source == target)
            {
                return new MigrationReport
                {
                    SourceProvider = source,
                    TargetProvider = target,
                    OriginalPrompt = prompt,
                    MigratedPrompt = prompt,
                    CompatibilityScore = 100,
                    Grade = "A+",
                    TokenEstimate = EstimateTokens(prompt),
                    SourceProfile = GetProfile(source),
                    TargetProfile = GetProfile(target)
                };
            }

            var issues = new List<MigrationIssue>();
            var lines = prompt.Split('\n');

            // Check provider-specific patterns
            issues.AddRange(FindProviderPatterns(prompt, source, target, lines));

            // Check structural compatibility
            issues.AddRange(CheckStructuralCompatibility(prompt, source, target));

            // Check feature compatibility
            issues.AddRange(CheckFeatureCompatibility(prompt, source, target));

            // Check token limits
            issues.AddRange(CheckTokenLimits(prompt, target));

            var score = CalculateCompatibilityScore(issues);

            return new MigrationReport
            {
                SourceProvider = source,
                TargetProvider = target,
                OriginalPrompt = prompt,
                MigratedPrompt = prompt,
                Issues = issues,
                CompatibilityScore = score,
                Grade = ScoreToGrade(score),
                TokenEstimate = EstimateTokens(prompt),
                SourceProfile = GetProfile(source),
                TargetProfile = GetProfile(target)
            };
        }

        /// <summary>Analyze and auto-migrate a prompt to the target provider.</summary>
        public MigrationReport Migrate(string prompt, LlmProvider source, LlmProvider target)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var report = Analyze(prompt, source, target);
            if (source == target) return report;

            var migrated = prompt;
            var appliedFixes = new List<MigrationIssue>();

            // Apply matching migration rules
            foreach (var rule in MigrationRules)
            {
                if (rule.Source != null && rule.Source != source) continue;
                if (rule.Target != target) continue;
                if (!rule.Pattern.IsMatch(migrated)) continue;

                var original = rule.Pattern.Match(migrated).Value;
                var replacement = rule.Replace(rule.Pattern.Match(migrated));
                if (replacement == original) continue;

                migrated = rule.Pattern.Replace(migrated, m => rule.Replace(m));
                appliedFixes.Add(new MigrationIssue
                {
                    Category = rule.Category,
                    Severity = rule.Severity,
                    Description = rule.Description,
                    Original = original,
                    Suggestion = replacement,
                    AutoFixable = true
                });
            }

            // Clean up whitespace artifacts
            migrated = Regex.Replace(migrated, @"\n{3,}", "\n\n");
            migrated = migrated.Trim();

            // Recalculate issues after migration
            var remainingIssues = report.Issues
                .Where(i => !appliedFixes.Any(f =>
                    f.Category == i.Category && f.Description == i.Description))
                .ToList();

            // Mark applied fixes as auto-fixed in the combined issue list
            var allIssues = appliedFixes.Concat(remainingIssues).ToList();
            var newScore = CalculateCompatibilityScore(remainingIssues);

            return new MigrationReport
            {
                SourceProvider = source,
                TargetProvider = target,
                OriginalPrompt = prompt,
                MigratedPrompt = migrated,
                Issues = allIssues,
                CompatibilityScore = newScore,
                Grade = ScoreToGrade(newScore),
                TokenEstimate = EstimateTokens(migrated),
                SourceProfile = GetProfile(source),
                TargetProfile = GetProfile(target)
            };
        }

        /// <summary>Auto-detect source provider and migrate to target.</summary>
        public MigrationReport AutoMigrate(string prompt, LlmProvider target)
        {
            var detection = DetectProvider(prompt);
            return Migrate(prompt, detection.Provider, target);
        }

        /// <summary>Batch-analyze multiple prompts for migration.</summary>
        public BatchMigrationReport BatchAnalyze(
            IEnumerable<string> prompts, LlmProvider source, LlmProvider target)
        {
            if (prompts == null)
                throw new ArgumentNullException(nameof(prompts));

            var reports = prompts
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Analyze(p, source, target))
                .ToList();

            return new BatchMigrationReport
            {
                Source = source,
                Target = target,
                Reports = reports
            };
        }

        /// <summary>Compare migration feasibility across multiple target providers.</summary>
        public Dictionary<LlmProvider, MigrationReport> CompareTargets(
            string prompt, LlmProvider source, IEnumerable<LlmProvider>? targets = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var targetList = targets?.ToList()
                ?? Enum.GetValues<LlmProvider>().Where(p => p != source && p != LlmProvider.Generic).ToList();

            return targetList.ToDictionary(
                t => t,
                t => Analyze(prompt, source, t)
            );
        }

        // ── Private helpers ──

        private List<MigrationIssue> FindProviderPatterns(
            string prompt, LlmProvider source, LlmProvider target, string[] lines)
        {
            var issues = new List<MigrationIssue>();

            foreach (var (pattern, provider, _, signal) in DetectionPatterns)
            {
                if (provider != source) continue;
                var matches = pattern.Matches(prompt);
                foreach (Match match in matches)
                {
                    var lineNum = GetLineNumber(prompt, match.Index);
                    var hasRule = MigrationRules.Any(r =>
                        (r.Source == null || r.Source == source) &&
                        r.Target == target && r.Pattern.IsMatch(match.Value));

                    issues.Add(new MigrationIssue
                    {
                        Category = MigrationCategory.RoleConvention,
                        Severity = MigrationSeverity.Warning,
                        Description = $"Source provider pattern: {signal}",
                        Original = match.Value,
                        Suggestion = $"Review and adapt for {target}",
                        Line = lineNum,
                        AutoFixable = hasRule
                    });
                }
            }

            // Deduplicate by description
            return issues
                .GroupBy(i => i.Description)
                .Select(g => g.First())
                .ToList();
        }

        private List<MigrationIssue> CheckStructuralCompatibility(
            string prompt, LlmProvider source, LlmProvider target)
        {
            var issues = new List<MigrationIssue>();

            // XML tags: Anthropic loves them, others are neutral
            var xmlTagCount = Regex.Matches(prompt, @"</?[a-zA-Z_][a-zA-Z0-9_-]*>").Count;
            if (source == LlmProvider.Anthropic && xmlTagCount > 2 &&
                target != LlmProvider.Anthropic)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.Formatting,
                    Severity = MigrationSeverity.Info,
                    Description = $"Prompt uses {xmlTagCount} XML tags (Anthropic convention)",
                    Suggestion = $"{target} supports XML but may respond better to markdown headers or plain sections",
                    AutoFixable = false
                });
            }

            // [INST] markers: Llama/Mistral
            if ((source == LlmProvider.Meta || source == LlmProvider.Mistral) &&
                Regex.IsMatch(prompt, @"\[INST\]") &&
                target != LlmProvider.Meta && target != LlmProvider.Mistral)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.Formatting,
                    Severity = MigrationSeverity.Warning,
                    Description = "Prompt contains [INST] markers not used by target provider",
                    Suggestion = "Remove [INST]/[/INST] markers and use the API's message role system",
                    AutoFixable = true
                });
            }

            // <<SYS>> markers: Llama
            if (source == LlmProvider.Meta && Regex.IsMatch(prompt, @"<<SYS>>") &&
                target != LlmProvider.Meta)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.SystemPrompt,
                    Severity = MigrationSeverity.Warning,
                    Description = "Prompt contains <<SYS>> markers not used by target provider",
                    Suggestion = "Extract system prompt content and use the API's system message role",
                    AutoFixable = true
                });
            }

            // Human/Assistant markers: Anthropic convention
            if (Regex.IsMatch(prompt, @"^Human:\s", RegexOptions.Multiline) &&
                target != LlmProvider.Anthropic)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.RoleConvention,
                    Severity = MigrationSeverity.Info,
                    Description = "Prompt uses Human:/Assistant: turn markers",
                    Suggestion = "Use the API's message role system instead of inline markers",
                    AutoFixable = true
                });
            }

            // Markdown structure check
            var hasMarkdownHeaders = Regex.IsMatch(prompt, @"^#{1,3}\s", RegexOptions.Multiline);
            if (!hasMarkdownHeaders && prompt.Length > 500 &&
                target == LlmProvider.OpenAI)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.Formatting,
                    Severity = MigrationSeverity.Info,
                    Description = "Long prompt without markdown headers",
                    Suggestion = "OpenAI models respond well to markdown-structured prompts",
                    AutoFixable = false
                });
            }

            return issues;
        }

        private List<MigrationIssue> CheckFeatureCompatibility(
            string prompt, LlmProvider source, LlmProvider target)
        {
            var issues = new List<MigrationIssue>();
            var targetProfile = GetProfile(target);

            // JSON mode references
            if (Regex.IsMatch(prompt, @"response_format.*json|json_object", RegexOptions.IgnoreCase) &&
                !targetProfile.SupportsJson)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.UnsupportedFeature,
                    Severity = MigrationSeverity.Error,
                    Description = "Prompt references JSON response format not supported by target",
                    Suggestion = "Add explicit JSON formatting instructions in the prompt text instead",
                    AutoFixable = false
                });
            }

            // Tool/function calling references
            if (Regex.IsMatch(prompt, @"\bfunction[_\s]?call|tool[_\s]?use|tool_choice", RegexOptions.IgnoreCase) &&
                !targetProfile.SupportsToolCalling)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.ToolCalling,
                    Severity = MigrationSeverity.Error,
                    Description = "Prompt references tool/function calling not supported by target",
                    Suggestion = "Restructure to use text-based tool descriptions and parsing",
                    AutoFixable = false
                });
            }

            // Image references
            if (Regex.IsMatch(prompt, @"\bimage_url|vision|image\s+input", RegexOptions.IgnoreCase) &&
                !targetProfile.SupportsImages)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.UnsupportedFeature,
                    Severity = MigrationSeverity.Error,
                    Description = "Prompt references image/vision capabilities not supported by target",
                    Suggestion = "Remove image references or use a text description instead",
                    AutoFixable = false
                });
            }

            // Thinking blocks: Anthropic → others
            if (source == LlmProvider.Anthropic &&
                Regex.IsMatch(prompt, @"<thinking>|think step by step in <thinking> tags", RegexOptions.IgnoreCase) &&
                target != LlmProvider.Anthropic)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.UnsupportedFeature,
                    Severity = MigrationSeverity.Warning,
                    Description = "Prompt uses Anthropic-style thinking blocks",
                    Suggestion = "Replace with 'Think step by step' or 'Let's work through this' instruction",
                    AutoFixable = false
                });
            }

            // System instruction reference: Google → others
            if (source == LlmProvider.Google &&
                Regex.IsMatch(prompt, @"system_instruction", RegexOptions.IgnoreCase) &&
                target != LlmProvider.Google)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.ApiSyntax,
                    Severity = MigrationSeverity.Warning,
                    Description = "Prompt references Google's system_instruction parameter",
                    Suggestion = "Use the target provider's system message/prompt mechanism",
                    AutoFixable = false
                });
            }

            return issues;
        }

        private List<MigrationIssue> CheckTokenLimits(string prompt, LlmProvider target)
        {
            var issues = new List<MigrationIssue>();
            var tokens = EstimateTokens(prompt);
            var profile = GetProfile(target);

            if (tokens > profile.MaxContextTokens * 0.8)
            {
                issues.Add(new MigrationIssue
                {
                    Category = MigrationCategory.TokenLimit,
                    Severity = tokens > profile.MaxContextTokens
                        ? MigrationSeverity.Error : MigrationSeverity.Warning,
                    Description = $"Prompt uses ~{tokens} tokens ({tokens * 100 / profile.MaxContextTokens}% of {target}'s {profile.MaxContextTokens:N0} limit)",
                    Suggestion = "Consider splitting or shortening the prompt",
                    AutoFixable = false
                });
            }

            return issues;
        }

        private static int CalculateCompatibilityScore(IList<MigrationIssue> issues)
        {
            if (issues.Count == 0) return 100;

            var penalty = 0;
            foreach (var issue in issues)
            {
                penalty += issue.Severity switch
                {
                    MigrationSeverity.Error => 20,
                    MigrationSeverity.Warning => 8,
                    MigrationSeverity.Info => 3,
                    _ => 0
                };
            }

            return Math.Max(0, 100 - penalty);
        }

        private static string ScoreToGrade(int score) => score switch
        {
            >= 95 => "A+",
            >= 90 => "A",
            >= 85 => "A-",
            >= 80 => "B+",
            >= 75 => "B",
            >= 70 => "B-",
            >= 65 => "C+",
            >= 60 => "C",
            >= 55 => "C-",
            >= 50 => "D",
            _ => "F"
        };

        private static int EstimateTokens(string text) =>
            (int)Math.Ceiling(text.Split(
                new[] { ' ', '\n', '\t', '\r' },
                StringSplitOptions.RemoveEmptyEntries).Length * 1.3);

        private static int GetLineNumber(string text, int charIndex)
        {
            if (charIndex <= 0) return 1;
            return text[..Math.Min(charIndex, text.Length)].Count(c => c == '\n') + 1;
        }
    }
}

