using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Prompt
{
    /// <summary>
    /// Types of LLM failure modes that can be detected and recovered from.
    /// </summary>
    public enum FailureMode
    {
        /// <summary>No failure detected.</summary>
        None,

        /// <summary>Empty or whitespace-only response.</summary>
        EmptyResponse,

        /// <summary>Model refused to answer (safety filters, policy, etc.).</summary>
        Refusal,

        /// <summary>Response was truncated mid-sentence (hit token limit).</summary>
        Truncation,

        /// <summary>Response contains repetition loops (degenerate output).</summary>
        RepetitionLoop,

        /// <summary>Response contains hallucination markers (fabricated citations, etc.).</summary>
        HallucinationMarker,

        /// <summary>Response is off-topic or doesn't address the prompt.</summary>
        OffTopic,

        /// <summary>Response contains only filler/hedging with no substance.</summary>
        FillerOnly,

        /// <summary>Response format doesn't match expected structure (e.g. expected JSON).</summary>
        FormatMismatch,

        /// <summary>Custom failure detected by user-defined predicate.</summary>
        Custom
    }

    /// <summary>
    /// Strategy for recovering from a detected failure.
    /// </summary>
    public enum RecoveryStrategy
    {
        /// <summary>Retry the same prompt as-is.</summary>
        Retry,

        /// <summary>Retry with a modified prompt that addresses the failure.</summary>
        RetryWithHint,

        /// <summary>Use a fallback prompt instead.</summary>
        Fallback,

        /// <summary>Return a default/canned response.</summary>
        DefaultResponse,

        /// <summary>Throw an exception to the caller.</summary>
        Throw,

        /// <summary>Return the failed response as-is (log but don't recover).</summary>
        PassThrough
    }

    /// <summary>
    /// Result of analyzing a response for failures.
    /// </summary>
    public class FailureAnalysis
    {
        /// <summary>The detected failure mode.</summary>
        public FailureMode Mode { get; set; } = FailureMode.None;

        /// <summary>Confidence score from 0.0 to 1.0.</summary>
        public double Confidence { get; set; }

        /// <summary>Human-readable description of what was detected.</summary>
        public string Description { get; set; } = "";

        /// <summary>Evidence strings that triggered the detection.</summary>
        public List<string> Evidence { get; set; } = new();

        /// <summary>Whether a failure was detected.</summary>
        public bool HasFailure => Mode != FailureMode.None;

        /// <summary>Suggested recovery hint to append to retry prompt.</summary>
        public string? SuggestedHint { get; set; }
    }

    /// <summary>
    /// Configuration for how to handle a specific failure mode.
    /// </summary>
    public class RecoveryRule
    {
        /// <summary>The failure mode this rule handles.</summary>
        public FailureMode Mode { get; set; }

        /// <summary>Recovery strategy to use.</summary>
        public RecoveryStrategy Strategy { get; set; } = RecoveryStrategy.Retry;

        /// <summary>Maximum retry attempts for this failure type.</summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>Hint text appended to the prompt on RetryWithHint strategy.</summary>
        public string? Hint { get; set; }

        /// <summary>Fallback prompt for Fallback strategy.</summary>
        public string? FallbackPrompt { get; set; }

        /// <summary>Default response for DefaultResponse strategy.</summary>
        public string? DefaultResponse { get; set; }

        /// <summary>Minimum confidence threshold to trigger this rule (0.0-1.0).</summary>
        public double MinConfidence { get; set; } = 0.5;
    }

    /// <summary>
    /// Record of a recovery attempt.
    /// </summary>
    public class RecoveryAttempt
    {
        /// <summary>Which attempt number (1-based).</summary>
        public int AttemptNumber { get; set; }

        /// <summary>The failure that was detected.</summary>
        public FailureAnalysis Failure { get; set; } = new();

        /// <summary>Strategy that was applied.</summary>
        public RecoveryStrategy StrategyUsed { get; set; }

        /// <summary>The prompt that was sent (may differ from original after hints/fallback).</summary>
        public string PromptUsed { get; set; } = "";

        /// <summary>The response received.</summary>
        public string Response { get; set; } = "";

        /// <summary>Whether this attempt succeeded (no failure detected in response).</summary>
        public bool Succeeded { get; set; }

        /// <summary>Timestamp of the attempt.</summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Result of error recovery execution.
    /// </summary>
    public class RecoveryResult
    {
        /// <summary>The final response (either successful or best-effort).</summary>
        public string Response { get; set; } = "";

        /// <summary>Whether recovery succeeded.</summary>
        public bool Succeeded { get; set; }

        /// <summary>Total attempts made (including the initial one).</summary>
        public int TotalAttempts { get; set; }

        /// <summary>All recovery attempts with details.</summary>
        public List<RecoveryAttempt> Attempts { get; set; } = new();

        /// <summary>The final failure analysis (None if succeeded).</summary>
        public FailureAnalysis FinalAnalysis { get; set; } = new();

        /// <summary>Original prompt that started the process.</summary>
        public string OriginalPrompt { get; set; } = "";
    }

    /// <summary>
    /// Custom failure detector delegate. Return a <see cref="FailureAnalysis"/>
    /// with <c>Mode = Custom</c> if a failure is detected, or <c>Mode = None</c>
    /// if the response is acceptable.
    /// </summary>
    public delegate FailureAnalysis CustomDetector(string prompt, string response);

    /// <summary>
    /// Detects common LLM failure modes in responses and applies configurable
    /// recovery strategies including retries, prompt hints, fallbacks, and
    /// default responses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LLMs can fail in many ways: refusing to answer, generating repetitive
    /// text, truncating mid-sentence, hallucinating citations, or returning
    /// off-topic responses. <c>PromptErrorRecovery</c> detects these failure
    /// modes and automatically applies recovery strategies.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var recovery = new PromptErrorRecovery();
    ///
    /// // Configure rules
    /// recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.RetryWithHint,
    ///     hint: "Please provide a direct answer without disclaimers.");
    /// recovery.AddRule(FailureMode.Truncation, RecoveryStrategy.RetryWithHint,
    ///     hint: "Please provide a concise, complete response.");
    /// recovery.AddRule(FailureMode.EmptyResponse, RecoveryStrategy.Retry, maxRetries: 3);
    /// recovery.AddRule(FailureMode.RepetitionLoop, RecoveryStrategy.Fallback,
    ///     fallbackPrompt: "Summarize the key point in one sentence.");
    ///
    /// // Analyze a response
    /// var analysis = recovery.Analyze("What is 2+2?", "I cannot help with that.");
    /// // analysis.Mode == FailureMode.Refusal
    /// // analysis.Confidence == 0.9
    ///
    /// // Use with a prompt function for automatic recovery
    /// var result = await recovery.ExecuteWithRecoveryAsync(
    ///     "Explain quantum entanglement",
    ///     async (prompt) => await callLlm(prompt));
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptErrorRecovery
    {
        private readonly Dictionary<FailureMode, RecoveryRule> _rules = new();
        private readonly List<CustomDetector> _customDetectors = new();
        private readonly List<RecoveryResult> _history = new();
        private const int MaxHistorySize = 500;
        private const int MaxResponseLength = 1_048_576;

        // Pre-compiled regex patterns for detection
        private static readonly Regex RefusalPattern = new(
            @"\b(I\s+cannot|I'm\s+unable\s+to|I\s+can't|I\s+am\s+not\s+able\s+to|" +
            @"I'm\s+not\s+able\s+to|I\s+don't\s+think\s+I\s+should|" +
            @"I\s+must\s+decline|I\s+will\s+not|I\s+won't|" +
            @"I'm\s+sorry,?\s+but\s+I\s+(cannot|can't|am\s+unable)|" +
            @"as\s+an?\s+AI(\s+language\s+model)?[,\s]+I\s+(cannot|can't|don't)|" +
            @"I'm\s+not\s+comfortable|" +
            @"against\s+my\s+(programming|guidelines|policy)|" +
            @"I\s+have\s+to\s+respectfully\s+decline|" +
            @"it\s+would\s+be\s+inappropriate\s+for\s+me|" +
            @"I'm\s+designed\s+to\s+be\s+helpful.{0,40}but)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        private static readonly Regex HallucinationPattern = new(
            @"(?:" +
            @"\[\d+\](?:\s*\[\d+\]){2,}|" +                          // Excessive inline citations [1][2][3]
            @"(?:doi|DOI):\s*10\.\d{4,}/[^\s]{5,}|" +                // Fabricated DOIs
            @"(?:ISBN|isbn)[:\s]*\d[\d-]{9,}|" +                      // Fabricated ISBNs
            @"(?:according\s+to|as\s+(?:stated|reported|published)\s+(?:in|by))\s+(?:a\s+)?(?:\d{4}\s+)?(?:study|paper|research|article|report)\s+(?:by|in|from)\s+(?:Dr\.?\s+)?[A-Z][a-z]+(?:\s+(?:and|&)\s+[A-Z][a-z]+){0,3}\s+\(\d{4}\)|" + // Fabricated academic citations
            @"https?://(?:www\.)?(?:[a-z0-9-]+\.){1,3}[a-z]{2,}/(?:[a-z0-9_/-]){20,}(?:\?[^\s]{10,})?)" + // Suspiciously specific URLs
            @"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        private static readonly Regex FillerPattern = new(
            @"^\s*(?:" +
            @"(?:That'?s?\s+(?:a\s+)?(?:great|good|excellent|interesting|wonderful|fantastic)\s+(?:question|point|observation)[!.]?\s*){1,}" +
            @"|(?:I'?d\s+be\s+happy\s+to\s+help[!.]\s*)" +
            @"|(?:Sure[!,]\s*(?:I\s+can\s+help\s+(?:with\s+that|you)[!.]\s*)?)" +
            @"|(?:Absolutely[!.]\s*)" +
            @"|(?:Of\s+course[!.]\s*)" +
            @")+\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Creates a new error recovery manager with optional default rules.
        /// </summary>
        /// <param name="useDefaults">
        /// If true, registers sensible default rules for common failure modes.
        /// </param>
        public PromptErrorRecovery(bool useDefaults = true)
        {
            if (useDefaults)
            {
                AddRule(FailureMode.EmptyResponse, RecoveryStrategy.Retry, maxRetries: 2);
                AddRule(FailureMode.Refusal, RecoveryStrategy.RetryWithHint, maxRetries: 1,
                    hint: "Please provide a direct, helpful response. If you have concerns, state them briefly but still attempt an answer.");
                AddRule(FailureMode.Truncation, RecoveryStrategy.RetryWithHint, maxRetries: 1,
                    hint: "Please provide a complete but concise response. Prioritize finishing your answer.");
                AddRule(FailureMode.RepetitionLoop, RecoveryStrategy.RetryWithHint, maxRetries: 1,
                    hint: "Please provide a clear, non-repetitive response.");
                AddRule(FailureMode.FillerOnly, RecoveryStrategy.RetryWithHint, maxRetries: 1,
                    hint: "Please skip pleasantries and provide the actual answer directly.");
            }
        }

        /// <summary>
        /// Adds or updates a recovery rule for a specific failure mode.
        /// </summary>
        public void AddRule(
            FailureMode mode,
            RecoveryStrategy strategy,
            int maxRetries = 2,
            string? hint = null,
            string? fallbackPrompt = null,
            string? defaultResponse = null,
            double minConfidence = 0.5)
        {
            _rules[mode] = new RecoveryRule
            {
                Mode = mode,
                Strategy = strategy,
                MaxRetries = Math.Max(0, Math.Min(maxRetries, 10)),
                Hint = hint,
                FallbackPrompt = fallbackPrompt,
                DefaultResponse = defaultResponse,
                MinConfidence = Math.Clamp(minConfidence, 0.0, 1.0)
            };
        }

        /// <summary>
        /// Removes the recovery rule for a failure mode.
        /// </summary>
        public bool RemoveRule(FailureMode mode) => _rules.Remove(mode);

        /// <summary>
        /// Gets the current rules.
        /// </summary>
        public IReadOnlyDictionary<FailureMode, RecoveryRule> Rules =>
            new Dictionary<FailureMode, RecoveryRule>(_rules);

        /// <summary>
        /// Registers a custom failure detector.
        /// </summary>
        public void AddCustomDetector(CustomDetector detector)
        {
            if (detector == null) throw new ArgumentNullException(nameof(detector));
            _customDetectors.Add(detector);
        }

        /// <summary>
        /// Analyzes a response for failure modes. Returns the highest-confidence
        /// failure detected, or a <c>None</c> analysis if the response looks OK.
        /// </summary>
        /// <param name="prompt">The original prompt that was sent.</param>
        /// <param name="response">The LLM response to analyze.</param>
        /// <returns>Analysis result with failure mode, confidence, and evidence.</returns>
        public FailureAnalysis Analyze(string prompt, string response)
        {
            if (response != null && response.Length > MaxResponseLength)
                response = response.Substring(0, MaxResponseLength);

            var candidates = new List<FailureAnalysis>();

            // Check empty
            if (string.IsNullOrWhiteSpace(response))
            {
                candidates.Add(new FailureAnalysis
                {
                    Mode = FailureMode.EmptyResponse,
                    Confidence = 1.0,
                    Description = "Response is empty or contains only whitespace.",
                    SuggestedHint = "Please provide a substantive response."
                });
            }
            else
            {
                // Check refusal
                var refusalAnalysis = DetectRefusal(response);
                if (refusalAnalysis.HasFailure) candidates.Add(refusalAnalysis);

                // Check truncation
                var truncAnalysis = DetectTruncation(response);
                if (truncAnalysis.HasFailure) candidates.Add(truncAnalysis);

                // Check repetition
                var repAnalysis = DetectRepetition(response);
                if (repAnalysis.HasFailure) candidates.Add(repAnalysis);

                // Check hallucination markers
                var hallAnalysis = DetectHallucination(response);
                if (hallAnalysis.HasFailure) candidates.Add(hallAnalysis);

                // Check filler only
                var fillerAnalysis = DetectFillerOnly(response);
                if (fillerAnalysis.HasFailure) candidates.Add(fillerAnalysis);

                // Custom detectors
                foreach (var detector in _customDetectors)
                {
                    try
                    {
                        var custom = detector(prompt ?? "", response);
                        if (custom != null && custom.HasFailure)
                            candidates.Add(custom);
                    }
                    catch
                    {
                        // Swallow custom detector errors
                    }
                }
            }

            // Return highest confidence failure
            if (candidates.Count == 0)
            {
                return new FailureAnalysis
                {
                    Mode = FailureMode.None,
                    Confidence = 0.0,
                    Description = "No failure detected."
                };
            }

            return candidates.OrderByDescending(c => c.Confidence).First();
        }

        /// <summary>
        /// Analyzes a response and returns ALL detected failures, sorted by confidence.
        /// </summary>
        public List<FailureAnalysis> AnalyzeAll(string prompt, string response)
        {
            if (response != null && response.Length > MaxResponseLength)
                response = response.Substring(0, MaxResponseLength);

            var results = new List<FailureAnalysis>();

            if (string.IsNullOrWhiteSpace(response))
            {
                results.Add(new FailureAnalysis
                {
                    Mode = FailureMode.EmptyResponse,
                    Confidence = 1.0,
                    Description = "Response is empty or contains only whitespace."
                });
                return results;
            }

            var refusal = DetectRefusal(response);
            if (refusal.HasFailure) results.Add(refusal);

            var trunc = DetectTruncation(response);
            if (trunc.HasFailure) results.Add(trunc);

            var rep = DetectRepetition(response);
            if (rep.HasFailure) results.Add(rep);

            var hall = DetectHallucination(response);
            if (hall.HasFailure) results.Add(hall);

            var filler = DetectFillerOnly(response);
            if (filler.HasFailure) results.Add(filler);

            foreach (var detector in _customDetectors)
            {
                try
                {
                    var custom = detector(prompt ?? "", response);
                    if (custom != null && custom.HasFailure)
                        results.Add(custom);
                }
                catch { }
            }

            return results.OrderByDescending(r => r.Confidence).ToList();
        }

        /// <summary>
        /// Executes a prompt with automatic error recovery. Calls the provided
        /// function, analyzes the response, and retries/falls back as configured.
        /// </summary>
        /// <param name="prompt">The original prompt.</param>
        /// <param name="executeFunc">
        /// Async function that sends a prompt and returns the response.
        /// </param>
        /// <returns>Recovery result with the final response and attempt details.</returns>
        public async System.Threading.Tasks.Task<RecoveryResult> ExecuteWithRecoveryAsync(
            string prompt,
            Func<string, System.Threading.Tasks.Task<string>> executeFunc)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
            if (executeFunc == null)
                throw new ArgumentNullException(nameof(executeFunc));

            var result = new RecoveryResult { OriginalPrompt = prompt };
            string currentPrompt = prompt;
            int attempt = 0;
            int maxTotalAttempts = 10; // Safety cap

            while (attempt < maxTotalAttempts)
            {
                attempt++;
                string response;
                try
                {
                    response = await executeFunc(currentPrompt);
                }
                catch (Exception ex)
                {
                    result.Attempts.Add(new RecoveryAttempt
                    {
                        AttemptNumber = attempt,
                        Failure = new FailureAnalysis
                        {
                            Mode = FailureMode.Custom,
                            Confidence = 1.0,
                            Description = $"Exception during execution: {ex.Message}"
                        },
                        StrategyUsed = RecoveryStrategy.Retry,
                        PromptUsed = currentPrompt,
                        Response = "",
                        Succeeded = false
                    });

                    if (attempt >= maxTotalAttempts) break;
                    continue;
                }

                var analysis = Analyze(prompt, response);

                var attemptRecord = new RecoveryAttempt
                {
                    AttemptNumber = attempt,
                    Failure = analysis,
                    PromptUsed = currentPrompt,
                    Response = response ?? ""
                };

                if (!analysis.HasFailure)
                {
                    attemptRecord.Succeeded = true;
                    attemptRecord.StrategyUsed = RecoveryStrategy.PassThrough;
                    result.Attempts.Add(attemptRecord);
                    result.Response = response ?? "";
                    result.Succeeded = true;
                    result.TotalAttempts = attempt;
                    result.FinalAnalysis = analysis;
                    AddToHistory(result);
                    return result;
                }

                // Find applicable rule
                if (!_rules.TryGetValue(analysis.Mode, out var rule)
                    || analysis.Confidence < rule.MinConfidence)
                {
                    // No rule or below confidence threshold — pass through
                    attemptRecord.StrategyUsed = RecoveryStrategy.PassThrough;
                    attemptRecord.Succeeded = false;
                    result.Attempts.Add(attemptRecord);
                    result.Response = response ?? "";
                    result.Succeeded = false;
                    result.TotalAttempts = attempt;
                    result.FinalAnalysis = analysis;
                    AddToHistory(result);
                    return result;
                }

                attemptRecord.StrategyUsed = rule.Strategy;
                result.Attempts.Add(attemptRecord);

                // Check if we've exhausted retries for this failure mode
                int failureModeAttempts = result.Attempts
                    .Count(a => a.Failure.Mode == analysis.Mode && !a.Succeeded);
                if (failureModeAttempts > rule.MaxRetries)
                {
                    result.Response = response ?? "";
                    result.Succeeded = false;
                    result.TotalAttempts = attempt;
                    result.FinalAnalysis = analysis;
                    AddToHistory(result);

                    if (rule.Strategy == RecoveryStrategy.Throw)
                        throw new PromptRecoveryException(analysis, result);

                    return result;
                }

                // Apply strategy
                switch (rule.Strategy)
                {
                    case RecoveryStrategy.Retry:
                        currentPrompt = prompt; // Retry original
                        break;

                    case RecoveryStrategy.RetryWithHint:
                        string hint = rule.Hint ?? analysis.SuggestedHint ?? "";
                        currentPrompt = string.IsNullOrWhiteSpace(hint)
                            ? prompt
                            : $"{prompt}\n\n[Note: {hint}]";
                        break;

                    case RecoveryStrategy.Fallback:
                        if (!string.IsNullOrWhiteSpace(rule.FallbackPrompt))
                            currentPrompt = rule.FallbackPrompt;
                        else
                            currentPrompt = prompt;
                        break;

                    case RecoveryStrategy.DefaultResponse:
                        result.Response = rule.DefaultResponse ?? "";
                        result.Succeeded = true;
                        result.TotalAttempts = attempt;
                        result.FinalAnalysis = new FailureAnalysis { Mode = FailureMode.None };
                        AddToHistory(result);
                        return result;

                    case RecoveryStrategy.Throw:
                        throw new PromptRecoveryException(analysis, result);

                    case RecoveryStrategy.PassThrough:
                        result.Response = response ?? "";
                        result.Succeeded = false;
                        result.TotalAttempts = attempt;
                        result.FinalAnalysis = analysis;
                        AddToHistory(result);
                        return result;
                }
            }

            // Exhausted all attempts
            result.TotalAttempts = attempt;
            result.Succeeded = false;
            if (result.Attempts.Count > 0)
            {
                var last = result.Attempts[result.Attempts.Count - 1];
                result.Response = last.Response;
                result.FinalAnalysis = last.Failure;
            }
            AddToHistory(result);
            return result;
        }

        /// <summary>
        /// Gets recovery execution history.
        /// </summary>
        public List<RecoveryResult> GetHistory(int? limit = null)
        {
            if (limit.HasValue && limit.Value > 0 && limit.Value < _history.Count)
                return _history.Skip(_history.Count - limit.Value).ToList();
            return new List<RecoveryResult>(_history);
        }

        /// <summary>
        /// Gets aggregate statistics from recovery history.
        /// </summary>
        public RecoveryStatistics GetStatistics()
        {
            if (_history.Count == 0)
            {
                return new RecoveryStatistics
                {
                    TotalExecutions = 0,
                    SuccessRate = 0
                };
            }

            var stats = new RecoveryStatistics
            {
                TotalExecutions = _history.Count,
                SuccessfulRecoveries = _history.Count(r => r.Succeeded),
                FailedRecoveries = _history.Count(r => !r.Succeeded),
                TotalAttempts = _history.Sum(r => r.TotalAttempts),
                AverageAttempts = _history.Average(r => r.TotalAttempts),
                SuccessRate = (double)_history.Count(r => r.Succeeded) / _history.Count
            };

            // Failure mode breakdown
            var allFailures = _history
                .SelectMany(r => r.Attempts)
                .Where(a => a.Failure.HasFailure)
                .GroupBy(a => a.Failure.Mode)
                .ToDictionary(g => g.Key, g => g.Count());
            stats.FailureModeBreakdown = allFailures;

            // Strategy effectiveness
            var stratGroups = _history
                .SelectMany(r => r.Attempts)
                .Where(a => a.StrategyUsed != RecoveryStrategy.PassThrough)
                .GroupBy(a => a.StrategyUsed);
            foreach (var g in stratGroups)
            {
                int total = g.Count();
                int succeeded = g.Count(a => a.Succeeded);
                stats.StrategyEffectiveness[g.Key] =
                    total > 0 ? (double)succeeded / total : 0;
            }

            return stats;
        }

        /// <summary>
        /// Clears recovery history.
        /// </summary>
        public void ClearHistory() => _history.Clear();

        /// <summary>
        /// Exports configuration and statistics as JSON.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var data = new
            {
                rules = _rules.Values.Select(r => new
                {
                    mode = r.Mode.ToString(),
                    strategy = r.Strategy.ToString(),
                    maxRetries = r.MaxRetries,
                    hint = r.Hint,
                    fallbackPrompt = r.FallbackPrompt,
                    defaultResponse = r.DefaultResponse,
                    minConfidence = r.MinConfidence
                }),
                customDetectors = _customDetectors.Count,
                statistics = GetStatistics()
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        #region Detection Methods

        private FailureAnalysis DetectRefusal(string response)
        {
            var matches = RefusalPattern.Matches(response);
            if (matches.Count == 0)
                return new FailureAnalysis { Mode = FailureMode.None };

            // Higher confidence with more refusal phrases or shorter responses
            double baseConfidence = 0.7;
            if (matches.Count > 1) baseConfidence += 0.1;
            if (response.Length < 200) baseConfidence += 0.1;

            // Check if the refusal is the dominant content
            int refusalTextLength = matches.Cast<Match>().Sum(m => m.Length);
            double refusalRatio = (double)refusalTextLength / response.Length;
            if (refusalRatio > 0.3) baseConfidence += 0.1;

            return new FailureAnalysis
            {
                Mode = FailureMode.Refusal,
                Confidence = Math.Min(baseConfidence, 1.0),
                Description = $"Detected {matches.Count} refusal pattern(s) in response.",
                Evidence = matches.Cast<Match>().Select(m => m.Value).Distinct().ToList(),
                SuggestedHint = "Please provide a direct answer. If you have safety concerns, briefly note them but still attempt a helpful response."
            };
        }

        private FailureAnalysis DetectTruncation(string response)
        {
            if (string.IsNullOrWhiteSpace(response) || response.Length < 50)
                return new FailureAnalysis { Mode = FailureMode.None };

            var evidence = new List<string>();
            double confidence = 0.0;

            string trimmed = response.TrimEnd();

            // Ends mid-sentence (no terminal punctuation)
            char lastChar = trimmed[trimmed.Length - 1];
            bool hasTerminal = lastChar == '.' || lastChar == '!' || lastChar == '?'
                || lastChar == '"' || lastChar == '\'' || lastChar == ')'
                || lastChar == ']' || lastChar == '}' || lastChar == ':'
                || lastChar == '`' || lastChar == '*' || lastChar == '-';

            if (!hasTerminal && trimmed.Length > 100)
            {
                confidence += 0.5;
                evidence.Add($"Ends with '{trimmed.Substring(Math.Max(0, trimmed.Length - 30))}' (no terminal punctuation)");
            }

            // Unbalanced code fences
            int fenceCount = Regex.Matches(response, @"```").Count;
            if (fenceCount % 2 != 0)
            {
                confidence += 0.3;
                evidence.Add($"Unbalanced code fences ({fenceCount} found)");
            }

            // Unbalanced brackets/parens
            int openBrackets = response.Count(c => c == '[') - response.Count(c => c == ']');
            int openParens = response.Count(c => c == '(') - response.Count(c => c == ')');
            if (openBrackets > 2 || openParens > 2)
            {
                confidence += 0.2;
                evidence.Add($"Unbalanced delimiters (brackets: {openBrackets}, parens: {openParens})");
            }

            // Numbered list that stops abruptly
            var numberedItems = Regex.Matches(response, @"^\s*(\d+)[.)]\s", RegexOptions.Multiline);
            if (numberedItems.Count >= 2)
            {
                int lastNumber = int.Parse(numberedItems[numberedItems.Count - 1].Groups[1].Value);
                // Check if the last item appears incomplete (no period at end of its line)
                int lastItemPos = numberedItems[numberedItems.Count - 1].Index;
                string afterLastItem = response.Substring(lastItemPos);
                int nextNewline = afterLastItem.IndexOf('\n');
                string lastLine = nextNewline >= 0
                    ? afterLastItem.Substring(0, nextNewline)
                    : afterLastItem;
                if (lastLine.TrimEnd().Length < 10)
                {
                    confidence += 0.2;
                    evidence.Add($"Numbered list item {lastNumber} appears incomplete");
                }
            }

            if (confidence < 0.3)
                return new FailureAnalysis { Mode = FailureMode.None };

            return new FailureAnalysis
            {
                Mode = FailureMode.Truncation,
                Confidence = Math.Min(confidence, 1.0),
                Description = "Response appears to be truncated.",
                Evidence = evidence,
                SuggestedHint = "Please provide a complete but concise response. Ensure you finish all sentences and close all code blocks."
            };
        }

        private FailureAnalysis DetectRepetition(string response)
        {
            if (string.IsNullOrWhiteSpace(response) || response.Length < 100)
                return new FailureAnalysis { Mode = FailureMode.None };

            var evidence = new List<string>();
            double confidence = 0.0;

            // Check for repeated sentences
            var sentences = Regex.Split(response, @"(?<=[.!?])\s+")
                .Where(s => s.Trim().Length > 20)
                .ToList();

            if (sentences.Count >= 4)
            {
                var sentenceCounts = sentences
                    .GroupBy(s => s.Trim().ToLowerInvariant())
                    .Where(g => g.Count() > 1)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                if (sentenceCounts.Count > 0)
                {
                    int maxRepeats = sentenceCounts[0].Count();
                    double repeatRatio = (double)sentenceCounts.Sum(g => g.Count()) / sentences.Count;

                    if (maxRepeats >= 3 || repeatRatio > 0.4)
                    {
                        confidence += 0.7;
                        evidence.Add($"Sentence repeated {maxRepeats}x: \"{sentenceCounts[0].Key.Substring(0, Math.Min(60, sentenceCounts[0].Key.Length))}...\"");
                    }
                    else if (maxRepeats >= 2)
                    {
                        confidence += 0.4;
                        evidence.Add($"{sentenceCounts.Count} sentence(s) repeated");
                    }
                }
            }

            // Check for repeated phrases (n-grams)
            var words = Regex.Split(response.ToLowerInvariant(), @"\s+")
                .Where(w => w.Length > 0)
                .ToArray();

            if (words.Length >= 20)
            {
                // Check 5-grams
                var ngrams = new Dictionary<string, int>();
                for (int i = 0; i <= words.Length - 5; i++)
                {
                    string ngram = string.Join(" ", words, i, 5);
                    ngrams[ngram] = ngrams.GetValueOrDefault(ngram) + 1;
                }

                var repeatedNgrams = ngrams.Where(kv => kv.Value >= 3).ToList();
                if (repeatedNgrams.Count > 0)
                {
                    int worst = repeatedNgrams.Max(kv => kv.Value);
                    confidence += Math.Min(0.3 + worst * 0.1, 0.5);
                    evidence.Add($"{repeatedNgrams.Count} 5-word phrase(s) repeated 3+ times (worst: {worst}x)");
                }
            }

            if (confidence < 0.4)
                return new FailureAnalysis { Mode = FailureMode.None };

            return new FailureAnalysis
            {
                Mode = FailureMode.RepetitionLoop,
                Confidence = Math.Min(confidence, 1.0),
                Description = "Response contains repetitive/looping content.",
                Evidence = evidence,
                SuggestedHint = "Please provide a clear, non-repetitive response. Each sentence should add new information."
            };
        }

        private FailureAnalysis DetectHallucination(string response)
        {
            var matches = HallucinationPattern.Matches(response);
            if (matches.Count == 0)
                return new FailureAnalysis { Mode = FailureMode.None };

            // More matches = higher confidence
            double confidence = Math.Min(0.4 + matches.Count * 0.15, 0.95);

            return new FailureAnalysis
            {
                Mode = FailureMode.HallucinationMarker,
                Confidence = confidence,
                Description = $"Detected {matches.Count} potential hallucination marker(s).",
                Evidence = matches.Cast<Match>()
                    .Select(m => m.Value.Length > 80
                        ? m.Value.Substring(0, 80) + "..."
                        : m.Value)
                    .Distinct()
                    .Take(5)
                    .ToList(),
                SuggestedHint = "Only include information you are confident about. Do not fabricate citations, URLs, or references."
            };
        }

        private FailureAnalysis DetectFillerOnly(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new FailureAnalysis { Mode = FailureMode.None };

            if (!FillerPattern.IsMatch(response))
                return new FailureAnalysis { Mode = FailureMode.None };

            return new FailureAnalysis
            {
                Mode = FailureMode.FillerOnly,
                Confidence = 0.85,
                Description = "Response contains only filler/pleasantries with no substance.",
                Evidence = new List<string> { response.Trim() },
                SuggestedHint = "Please skip pleasantries and provide the actual answer directly."
            };
        }

        #endregion

        private void AddToHistory(RecoveryResult result)
        {
            _history.Add(result);
            if (_history.Count > MaxHistorySize)
            {
                int removeCount = MaxHistorySize / 10;
                _history.RemoveRange(0, removeCount);
            }
        }
    }

    /// <summary>
    /// Aggregate statistics from error recovery history.
    /// </summary>
    public class RecoveryStatistics
    {
        /// <summary>Total executions tracked.</summary>
        public int TotalExecutions { get; set; }

        /// <summary>Successful recoveries.</summary>
        public int SuccessfulRecoveries { get; set; }

        /// <summary>Failed recoveries.</summary>
        public int FailedRecoveries { get; set; }

        /// <summary>Total attempts across all executions.</summary>
        public int TotalAttempts { get; set; }

        /// <summary>Average attempts per execution.</summary>
        public double AverageAttempts { get; set; }

        /// <summary>Success rate (0.0-1.0).</summary>
        public double SuccessRate { get; set; }

        /// <summary>Count of each failure mode encountered.</summary>
        public Dictionary<FailureMode, int> FailureModeBreakdown { get; set; } = new();

        /// <summary>Success rate per strategy (0.0-1.0).</summary>
        public Dictionary<RecoveryStrategy, double> StrategyEffectiveness { get; set; } = new();
    }

    /// <summary>
    /// Exception thrown when recovery strategy is <see cref="RecoveryStrategy.Throw"/>.
    /// </summary>
    public class PromptRecoveryException : Exception
    {
        /// <summary>The failure analysis that triggered the exception.</summary>
        public FailureAnalysis Analysis { get; }

        /// <summary>The full recovery result up to the point of failure.</summary>
        public RecoveryResult Result { get; }

        /// <summary>Creates a new recovery exception.</summary>
        public PromptRecoveryException(FailureAnalysis analysis, RecoveryResult result)
            : base($"Prompt recovery failed: {analysis.Mode} ({analysis.Description})")
        {
            Analysis = analysis;
            Result = result;
        }
    }
}
