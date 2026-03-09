namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Deep structural analysis of prompts — detects anti-patterns, identifies
    /// components, measures clarity, and suggests specific improvements.
    /// Unlike <see cref="PromptGuard"/> (security-focused) this class focuses
    /// on prompt effectiveness and engineering quality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var report = PromptDebugger.Analyze("Tell me about dogs");
    /// Console.WriteLine($"Clarity: {report.ClarityScore}/100");
    /// foreach (var issue in report.Issues)
    ///     Console.WriteLine($"[{issue.Severity}] {issue.Message}");
    /// foreach (var fix in report.SuggestedFixes)
    ///     Console.WriteLine($"Fix: {fix}");
    ///
    /// // With conversation context
    /// var messages = new[] {
    ///     new DebugChatMessage("system", "You are a helpful assistant."),
    ///     new DebugChatMessage("user", "Summarize this article.")
    /// };
    /// var report2 = PromptDebugger.AnalyzeConversation(messages);
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptDebugger
    {
        // ── Anti-pattern detectors ───────────────────────────────

        /// <summary>Regex timeout for all pattern matching (ReDoS protection).</summary>
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

        private static readonly (Regex Pattern, string Id, string Message, IssueSeverity Severity, string Fix)[] AntiPatterns =
        {
            (new Regex(@"\b(do\s+everything|handle\s+all|cover\s+every|be\s+comprehensive\s+about\s+everything)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP001", "Overly broad scope — asking the model to do 'everything' produces shallow results",
                IssueSeverity.Warning,
                "Break the task into specific sub-tasks or focus on one aspect"),

            (new Regex(@"\b(make\s+it\s+(good|nice|better|great|perfect))\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP002", "Vague quality instruction — 'make it good' gives the model no actionable criteria",
                IssueSeverity.Warning,
                "Specify what 'good' means: e.g., 'use active voice, keep sentences under 20 words'"),

            (new Regex(@"\b(don'?t|do\s+not|never|avoid)\b[^.]{0,50}\b(but\s+also|however|yet)\b[^.]{0,50}\b(do|include|make\s+sure|ensure)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP003", "Contradictory instructions — negation followed by counter-instruction creates ambiguity",
                IssueSeverity.Error,
                "Separate conflicting constraints or rephrase as positive instructions"),

            (new Regex(@"(?:^|\.\s*)((?:also|and\s+also|additionally|furthermore|moreover|plus|on\s+top\s+of\s+that)\b[^.]*\.[\s]*){3,}",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline, RegexTimeout),
                "AP004", "Instruction stacking — too many additive clauses can dilute focus",
                IssueSeverity.Info,
                "Prioritize constraints: list the 3 most important requirements first"),

            (new Regex(@"\b(obviously|clearly|simply|just|easily|basically)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP005", "Filler/hedge words waste tokens without adding information",
                IssueSeverity.Info,
                "Remove filler words to save tokens and improve clarity"),

            (new Regex(@"\b(as\s+an?\s+(AI|assistant|language\s+model|LLM|chatbot|bot))\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP006", "Unnecessary meta-reference — the model knows what it is",
                IssueSeverity.Info,
                "Remove 'as an AI' phrasing unless role-setting is intentional"),

            (new Regex(@"\b(think\s+step\s+by\s+step|let'?s\s+think|chain\s+of\s+thought)\b.*\b(be\s+(brief|concise|short)|one\s+(sentence|line|word))\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP007", "Conflicting format: requesting step-by-step AND brevity",
                IssueSeverity.Error,
                "Choose either detailed reasoning (step-by-step) or concise output, not both"),

            (new Regex(@"(\b(please|kindly|if\s+you\s+(could|would|don'?t\s+mind))\b[^.]*){3,}",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP008", "Excessive politeness — multiple please/kindly phrases waste tokens",
                IssueSeverity.Info,
                "One polite opener is fine; remove redundant courtesy phrases"),

            (new Regex(@"\b(I\s+want|I\s+need|I'?d\s+like|can\s+you|could\s+you|would\s+you)\b.*\b(I\s+want|I\s+need|I'?d\s+like|can\s+you|could\s+you|would\s+you)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "AP009", "Repeated request framing — multiple 'I want/can you' suggests unclear task",
                IssueSeverity.Warning,
                "State the task once directly: 'Summarize X' instead of 'Can you summarize X? I want you to...'"),

            (new Regex(@"(.{15,})\1",
                RegexOptions.Compiled, RegexTimeout),
                "AP010", "Duplicated text detected — repeated content wastes tokens",
                IssueSeverity.Warning,
                "Remove the duplicated section"),
        };

        // ── Component markers ────────────────────────────────────

        private static readonly (Regex Pattern, string Component)[] ComponentDetectors =
        {
            (new Regex(@"^\s*(you\s+are|act\s+as|role\s*:|persona\s*:|your\s+role)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline, RegexTimeout),
                "Persona/Role"),

            (new Regex(@"\b(context|background|given\s+that|assuming|here\s+is\s+(the\s+)?context)\s*:",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "Context"),

            (new Regex(@"^\s*(task|instruction|objective|goal|your\s+task)\s*:",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline, RegexTimeout),
                "Task"),

            (new Regex(@"\b(constraint|rule|requirement|guideline|you\s+must|you\s+should|always|never)\s*:?\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "Constraints"),

            (new Regex(@"\b(example|for\s+instance|e\.g\.|such\s+as|input\s*:|output\s*:|sample\s*:)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "Examples"),

            (new Regex(@"\b(format|output\s+format|respond\s+(in|as|with)|return\s+(as|in)|use\s+(JSON|XML|markdown|YAML|CSV|table|bullet|numbered))\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "Output Format"),

            (new Regex(@"\b(step\s+by\s+step|chain\s+of\s+thought|think\s+(carefully|through)|reason\s+about|let'?s\s+think)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "Reasoning Strategy"),

            (new Regex(@"\b(tone|style|voice|formal|informal|casual|professional|friendly|academic)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
                "Tone/Style"),
        };

        // ── Regex timeout guard ──────────────────────────────────

        // (Uses RegexTimeout defined at class top — 500ms)

        // ── Pre-compiled regex patterns for hot-path methods ────

        private static readonly Regex ImperativePattern = new(
            @"^\s*(list|explain|describe|summarize|write|create|generate|analyze|compare|translate|convert|extract|find|show|tell|give|provide)\b",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex InstructionWordsPattern = new(
            @"\b(must|should|always|never|ensure|make\s+sure|required|important|critical|do\s+not|don'?t)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex CapsPattern = new(
            @"\b[A-Z]{4,}\b",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Safe regex match with timeout protection to prevent ReDoS.
        /// </summary>
        private static bool SafeMatch(Regex pattern, string input)
        {
            try
            {
                return pattern.IsMatch(input);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        private static MatchCollection SafeMatches(Regex pattern, string input)
        {
            try
            {
                return pattern.Matches(input);
            }
            catch (RegexMatchTimeoutException)
            {
                return Regex.Matches("", "x", RegexOptions.None, TimeSpan.FromMilliseconds(500)); // empty matches
            }
        }

        // ── Analysis ─────────────────────────────────────────────

        /// <summary>
        /// Analyzes a prompt for structural issues, anti-patterns, component
        /// coverage, and clarity. Returns a detailed diagnostic report.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>A <see cref="DebugReport"/> with findings and suggestions.</returns>
        public static DebugReport Analyze(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new DebugReport
                {
                    PromptText = prompt ?? "",
                    ClarityScore = 0,
                    Issues = { new DebugIssue
                    {
                        Id = "EMPTY",
                        Severity = IssueSeverity.Error,
                        Message = "Prompt is empty or whitespace-only",
                        SuggestedFix = "Provide a clear instruction"
                    }},
                    SuggestedFixes = { "Provide a prompt with a clear task statement" }
                };
            }

            var report = new DebugReport { PromptText = prompt };

            // 1. Detect components
            DetectComponents(prompt, report);

            // 2. Detect anti-patterns
            DetectAntiPatterns(prompt, report);

            // 3. Structural analysis
            AnalyzeStructure(prompt, report);

            // 4. Calculate clarity score
            report.ClarityScore = CalculateClarityScore(report);

            // 5. Generate improvement suggestions
            GenerateSuggestions(report);

            return report;
        }

        /// <summary>
        /// Analyzes a multi-turn conversation for role consistency,
        /// context coherence, and per-message issues.
        /// </summary>
        /// <param name="messages">Conversation messages with role and content.</param>
        /// <returns>A <see cref="DebugReport"/> covering the full conversation.</returns>
        public static DebugReport AnalyzeConversation(IEnumerable<DebugChatMessage> messages)
        {
            var msgList = messages?.ToList() ?? new List<DebugChatMessage>();
            if (msgList.Count == 0)
            {
                return new DebugReport
                {
                    ClarityScore = 0,
                    Issues = { new DebugIssue
                    {
                        Id = "NO_MSGS",
                        Severity = IssueSeverity.Error,
                        Message = "No messages in conversation",
                        SuggestedFix = "Add at least one user message"
                    }}
                };
            }

            // Analyze the full conversation as concatenated text
            var fullText = string.Join("\n\n", msgList.Select(m => m.Content ?? ""));
            var report = Analyze(fullText);
            report.MessageCount = msgList.Count;

            // Check for system message
            var hasSystem = msgList.Any(m =>
                string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase));
            if (!hasSystem)
            {
                report.Issues.Add(new DebugIssue
                {
                    Id = "CONV001",
                    Severity = IssueSeverity.Info,
                    Message = "No system message — consider adding one for consistent behavior",
                    SuggestedFix = "Add a system message defining the assistant's role and constraints"
                });
            }

            // Check for role consistency
            var roles = msgList.Select(m => m.Role?.ToLowerInvariant() ?? "").Distinct().ToList();
            var validRoles = new HashSet<string> { "system", "user", "assistant" };
            var invalidRoles = roles.Where(r => !string.IsNullOrEmpty(r) && !validRoles.Contains(r)).ToList();
            if (invalidRoles.Count > 0)
            {
                report.Issues.Add(new DebugIssue
                {
                    Id = "CONV002",
                    Severity = IssueSeverity.Warning,
                    Message = $"Non-standard role(s) detected: {string.Join(", ", invalidRoles)}",
                    SuggestedFix = "Use standard roles: 'system', 'user', 'assistant'"
                });
            }

            // Check for empty messages
            var emptyMsgs = msgList.Where(m => string.IsNullOrWhiteSpace(m.Content)).ToList();
            if (emptyMsgs.Count > 0)
            {
                report.Issues.Add(new DebugIssue
                {
                    Id = "CONV003",
                    Severity = IssueSeverity.Warning,
                    Message = $"{emptyMsgs.Count} empty message(s) in conversation",
                    SuggestedFix = "Remove empty messages — they waste tokens"
                });
            }

            // Check conversation flow (user→assistant alternation)
            var nonSystemMsgs = msgList
                .Where(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();
            for (int i = 1; i < nonSystemMsgs.Count; i++)
            {
                if (string.Equals(nonSystemMsgs[i].Role, nonSystemMsgs[i - 1].Role,
                    StringComparison.OrdinalIgnoreCase))
                {
                    report.Issues.Add(new DebugIssue
                    {
                        Id = "CONV004",
                        Severity = IssueSeverity.Info,
                        Message = $"Consecutive '{nonSystemMsgs[i].Role}' messages at positions {i} and {i + 1} — consider merging",
                        SuggestedFix = "Merge consecutive same-role messages to save tokens and improve coherence"
                    });
                    break; // Only report once
                }
            }

            return report;
        }

        /// <summary>
        /// Compares two prompt variants and reports differences in structure,
        /// clarity, and detected issues.
        /// </summary>
        /// <param name="promptA">First prompt variant.</param>
        /// <param name="promptB">Second prompt variant.</param>
        /// <returns>A comparison report highlighting differences.</returns>
        public static PromptComparison Compare(string promptA, string promptB)
        {
            var reportA = Analyze(promptA ?? "");
            var reportB = Analyze(promptB ?? "");

            var onlyInA = reportA.Components.Except(reportB.Components).ToList();
            var onlyInB = reportB.Components.Except(reportA.Components).ToList();

            var issuesOnlyInA = reportA.Issues
                .Where(i => !reportB.Issues.Any(j => j.Id == i.Id))
                .Select(i => i.Message).ToList();
            var issuesOnlyInB = reportB.Issues
                .Where(i => !reportA.Issues.Any(j => j.Id == i.Id))
                .Select(i => i.Message).ToList();

            string recommendation;
            if (reportA.ClarityScore > reportB.ClarityScore + 10)
                recommendation = "Prompt A is significantly clearer — prefer it";
            else if (reportB.ClarityScore > reportA.ClarityScore + 10)
                recommendation = "Prompt B is significantly clearer — prefer it";
            else if (reportA.Issues.Count(i => i.Severity >= IssueSeverity.Warning)
                     < reportB.Issues.Count(i => i.Severity >= IssueSeverity.Warning))
                recommendation = "Prompt A has fewer warnings — slight edge";
            else if (reportB.Issues.Count(i => i.Severity >= IssueSeverity.Warning)
                     < reportA.Issues.Count(i => i.Severity >= IssueSeverity.Warning))
                recommendation = "Prompt B has fewer warnings — slight edge";
            else
                recommendation = "Both prompts are similar in quality — choose based on intent";

            var tokenDiff = reportA.TokenEstimate - reportB.TokenEstimate;

            return new PromptComparison
            {
                ReportA = reportA,
                ReportB = reportB,
                ComponentsOnlyInA = onlyInA,
                ComponentsOnlyInB = onlyInB,
                IssuesOnlyInA = issuesOnlyInA,
                IssuesOnlyInB = issuesOnlyInB,
                ClarityDifference = reportA.ClarityScore - reportB.ClarityScore,
                TokenDifference = tokenDiff,
                Recommendation = recommendation
            };
        }

        // ── Internal analysis methods ────────────────────────────

        private static void DetectComponents(string prompt, DebugReport report)
        {
            foreach (var (pattern, component) in ComponentDetectors)
            {
                if (SafeMatch(pattern, prompt) && !report.Components.Contains(component))
                {
                    report.Components.Add(component);
                }
            }
        }

        private static void DetectAntiPatterns(string prompt, DebugReport report)
        {
            foreach (var (pattern, id, message, severity, fix) in AntiPatterns)
            {
                if (SafeMatch(pattern, prompt))
                {
                    report.Issues.Add(new DebugIssue
                    {
                        Id = id,
                        Severity = severity,
                        Message = message,
                        SuggestedFix = fix
                    });
                }
            }
        }

        private static void AnalyzeStructure(string prompt, DebugReport report)
        {
            var words = prompt.Split(new[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);
            var sentences = Regex.Split(prompt, @"(?<=[.!?])\s+", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            var lines = prompt.Split('\n');

            report.WordCount = words.Length;
            report.SentenceCount = sentences.Length;
            report.LineCount = lines.Length;
            report.TokenEstimate = PromptGuard.EstimateTokens(prompt);

            // Average sentence length
            if (sentences.Length > 0)
            {
                var avgWords = words.Length / (double)sentences.Length;
                report.AverageSentenceLength = Math.Round(avgWords, 1);

                if (avgWords > 35)
                {
                    report.Issues.Add(new DebugIssue
                    {
                        Id = "STRUCT001",
                        Severity = IssueSeverity.Warning,
                        Message = $"Average sentence length is {avgWords:F0} words — long sentences reduce clarity",
                        SuggestedFix = "Break sentences longer than 25 words into shorter ones"
                    });
                }
            }

            // Check for extremely short prompts
            if (words.Length < 5 && words.Length > 0)
            {
                report.Issues.Add(new DebugIssue
                {
                    Id = "STRUCT002",
                    Severity = IssueSeverity.Warning,
                    Message = "Very short prompt — may produce generic or unexpected results",
                    SuggestedFix = "Add context, constraints, or output format to guide the response"
                });
            }

            // Check for missing question mark or imperative
            var hasQuestion = prompt.Contains('?');
            var hasImperative = SafeMatches(ImperativePattern, prompt).Count > 0;
            if (!hasQuestion && !hasImperative && words.Length > 3)
            {
                report.Issues.Add(new DebugIssue
                {
                    Id = "STRUCT003",
                    Severity = IssueSeverity.Info,
                    Message = "No clear question or imperative verb detected — the model may not know what action to take",
                    SuggestedFix = "Start with an action verb (Explain, List, Summarize) or end with a question"
                });
            }

            // Check for template placeholders that weren't filled
            var unfilled = Regex.Matches(prompt, @"\{\{[^}]+\}\}", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            if (unfilled.Count > 0)
            {
                var names = unfilled.Cast<Match>().Select(m => m.Value).Distinct().ToList();
                report.Issues.Add(new DebugIssue
                {
                    Id = "STRUCT004",
                    Severity = IssueSeverity.Error,
                    Message = $"Unfilled template placeholder(s): {string.Join(", ", names)}",
                    SuggestedFix = "Fill all {{placeholders}} before sending to the model"
                });
            }

            var instructionCount = SafeMatches(InstructionWordsPattern, prompt).Count;
            if (words.Length > 20)
            {
                report.InstructionDensity = Math.Round((double)instructionCount / words.Length * 100, 1);
                if (report.InstructionDensity > 15)
                {
                    report.Issues.Add(new DebugIssue
                    {
                        Id = "STRUCT005",
                        Severity = IssueSeverity.Warning,
                        Message = $"High instruction density ({report.InstructionDensity:F0}%) — too many constraints can confuse the model",
                        SuggestedFix = "Prioritize the 3-5 most important constraints and remove or soften the rest"
                    });
                }
            }

            var capsMatches = SafeMatches(CapsPattern, prompt);
            if (capsMatches.Count > 3)
            {
                report.Issues.Add(new DebugIssue
                {
                    Id = "STRUCT006",
                    Severity = IssueSeverity.Info,
                    Message = $"Multiple ALL-CAPS words ({capsMatches.Count}) — excessive emphasis can reduce readability",
                    SuggestedFix = "Use caps sparingly for critical keywords only"
                });
            }
        }

        private static int CalculateClarityScore(DebugReport report)
        {
            int score = 70; // Base score

            // Component bonuses (good structure)
            if (report.Components.Contains("Task")) score += 10;
            if (report.Components.Contains("Output Format")) score += 8;
            if (report.Components.Contains("Persona/Role")) score += 5;
            if (report.Components.Contains("Context")) score += 5;
            if (report.Components.Contains("Examples")) score += 7;
            if (report.Components.Contains("Constraints")) score += 3;

            // Issue penalties
            foreach (var issue in report.Issues)
            {
                switch (issue.Severity)
                {
                    case IssueSeverity.Error: score -= 15; break;
                    case IssueSeverity.Warning: score -= 8; break;
                    case IssueSeverity.Info: score -= 2; break;
                }
            }

            // Length penalties
            if (report.WordCount < 5) score -= 20;
            if (report.WordCount > 1000) score -= 5;
            if (report.AverageSentenceLength > 40) score -= 10;

            return Math.Max(0, Math.Min(100, score));
        }

        private static void GenerateSuggestions(DebugReport report)
        {
            // Missing component suggestions
            var ideal = new[]
            {
                ("Task", "Add a clear task statement (e.g., 'Summarize the following article')"),
                ("Output Format", "Specify an output format (e.g., 'Respond in JSON' or 'Use bullet points')"),
                ("Examples", "Add 1-2 examples of expected input→output to guide the model"),
                ("Context", "Provide relevant context to reduce ambiguity"),
            };

            foreach (var (component, suggestion) in ideal)
            {
                if (!report.Components.Contains(component))
                {
                    report.SuggestedFixes.Add(suggestion);
                }
            }

            // Issue-specific fixes
            foreach (var issue in report.Issues.Where(i => i.Severity >= IssueSeverity.Warning))
            {
                if (!string.IsNullOrEmpty(issue.SuggestedFix) &&
                    !report.SuggestedFixes.Contains(issue.SuggestedFix))
                {
                    report.SuggestedFixes.Add(issue.SuggestedFix);
                }
            }

            // Cap suggestions to avoid overwhelm
            if (report.SuggestedFixes.Count > 5)
            {
                report.SuggestedFixes = report.SuggestedFixes.Take(5).ToList();
            }
        }

        // ── Serialization ────────────────────────────────────────

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Serializes a debug report to JSON.
        /// </summary>
        public static string ToJson(DebugReport report) =>
            JsonSerializer.Serialize(report, JsonOptions);

        /// <summary>
        /// Deserializes a debug report from JSON.
        /// </summary>
        public static DebugReport? FromJson(string json) =>
            JsonSerializer.Deserialize<DebugReport>(json, JsonOptions);
    }

    // ── Models ───────────────────────────────────────────────────

    /// <summary>
    /// Issue severity for prompt debugging.
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>Informational — optional improvement.</summary>
        Info = 0,
        /// <summary>Warning — likely impacts quality.</summary>
        Warning = 1,
        /// <summary>Error — will likely cause problems.</summary>
        Error = 2
    }

    /// <summary>
    /// A single diagnostic issue found in a prompt.
    /// </summary>
    public class DebugIssue
    {
        /// <summary>Unique issue identifier (e.g., "AP001", "STRUCT002").</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        /// <summary>Severity level.</summary>
        [JsonPropertyName("severity")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IssueSeverity Severity { get; set; }

        /// <summary>Human-readable description of the issue.</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        /// <summary>Actionable suggestion to fix the issue.</summary>
        [JsonPropertyName("suggestedFix")]
        public string SuggestedFix { get; set; } = "";
    }

    /// <summary>
    /// Complete prompt diagnostic report.
    /// </summary>
    public class DebugReport
    {
        /// <summary>The analyzed prompt text.</summary>
        [JsonPropertyName("promptText")]
        public string PromptText { get; set; } = "";

        /// <summary>Overall clarity score (0-100).</summary>
        [JsonPropertyName("clarityScore")]
        public int ClarityScore { get; set; }

        /// <summary>Clarity grade based on score.</summary>
        [JsonPropertyName("clarityGrade")]
        public string ClarityGrade => ClarityScore >= 90 ? "A" :
            ClarityScore >= 80 ? "B" : ClarityScore >= 70 ? "C" :
            ClarityScore >= 60 ? "D" : "F";

        /// <summary>Detected prompt components (e.g., "Persona/Role", "Task").</summary>
        [JsonPropertyName("components")]
        public List<string> Components { get; set; } = new();

        /// <summary>Detected issues and anti-patterns.</summary>
        [JsonPropertyName("issues")]
        public List<DebugIssue> Issues { get; set; } = new();

        /// <summary>Prioritized improvement suggestions.</summary>
        [JsonPropertyName("suggestedFixes")]
        public List<string> SuggestedFixes { get; set; } = new();

        /// <summary>Word count.</summary>
        [JsonPropertyName("wordCount")]
        public int WordCount { get; set; }

        /// <summary>Sentence count.</summary>
        [JsonPropertyName("sentenceCount")]
        public int SentenceCount { get; set; }

        /// <summary>Line count.</summary>
        [JsonPropertyName("lineCount")]
        public int LineCount { get; set; }

        /// <summary>Estimated token count.</summary>
        [JsonPropertyName("tokenEstimate")]
        public int TokenEstimate { get; set; }

        /// <summary>Average words per sentence.</summary>
        [JsonPropertyName("averageSentenceLength")]
        public double AverageSentenceLength { get; set; }

        /// <summary>Instruction word density as percentage.</summary>
        [JsonPropertyName("instructionDensity")]
        public double InstructionDensity { get; set; }

        /// <summary>Number of messages (for conversation analysis).</summary>
        [JsonPropertyName("messageCount")]
        public int MessageCount { get; set; }

        /// <summary>Error count.</summary>
        [JsonIgnore]
        public int ErrorCount => Issues.Count(i => i.Severity == IssueSeverity.Error);

        /// <summary>Warning count.</summary>
        [JsonIgnore]
        public int WarningCount => Issues.Count(i => i.Severity == IssueSeverity.Warning);

        /// <summary>Summary string for quick display.</summary>
        public override string ToString() =>
            $"Clarity: {ClarityGrade} ({ClarityScore}/100) | " +
            $"Components: {Components.Count} | " +
            $"Issues: {ErrorCount}E/{WarningCount}W/{Issues.Count - ErrorCount - WarningCount}I | " +
            $"~{TokenEstimate} tokens";
    }

    /// <summary>
    /// Comparison between two prompt variants.
    /// </summary>
    public class PromptComparison
    {
        /// <summary>Analysis of the first prompt.</summary>
        [JsonPropertyName("reportA")]
        public DebugReport ReportA { get; set; } = new();

        /// <summary>Analysis of the second prompt.</summary>
        [JsonPropertyName("reportB")]
        public DebugReport ReportB { get; set; } = new();

        /// <summary>Components present only in prompt A.</summary>
        [JsonPropertyName("componentsOnlyInA")]
        public List<string> ComponentsOnlyInA { get; set; } = new();

        /// <summary>Components present only in prompt B.</summary>
        [JsonPropertyName("componentsOnlyInB")]
        public List<string> ComponentsOnlyInB { get; set; } = new();

        /// <summary>Issues found only in prompt A.</summary>
        [JsonPropertyName("issuesOnlyInA")]
        public List<string> IssuesOnlyInA { get; set; } = new();

        /// <summary>Issues found only in prompt B.</summary>
        [JsonPropertyName("issuesOnlyInB")]
        public List<string> IssuesOnlyInB { get; set; } = new();

        /// <summary>Difference in clarity score (A - B). Positive = A is clearer.</summary>
        [JsonPropertyName("clarityDifference")]
        public int ClarityDifference { get; set; }

        /// <summary>Difference in estimated tokens (A - B). Positive = A uses more tokens.</summary>
        [JsonPropertyName("tokenDifference")]
        public int TokenDifference { get; set; }

        /// <summary>Which prompt to prefer and why.</summary>
        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; } = "";
    }

    /// <summary>
    /// Simple chat message for conversation analysis.
    /// </summary>
    public class DebugChatMessage
    {
        /// <summary>Message role (system, user, assistant).</summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        /// <summary>Message content.</summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        /// <summary>Creates a new chat message.</summary>
        public DebugChatMessage() { }

        /// <summary>Creates a new chat message with role and content.</summary>
        public DebugChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
