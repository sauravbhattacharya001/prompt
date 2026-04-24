namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    // ────────────────────────────────────────────
    //  PromptContractTester – Design-by-Contract Verification for Prompts
    //
    //  Define preconditions, postconditions, and invariants for prompts
    //  and their expected responses. Run contracts against prompt/response
    //  pairs to detect violations, measure contract coverage, and generate
    //  structured reports. Supports autonomous batch verification with
    //  severity classification, violation clustering, and proactive
    //  improvement recommendations.
    // ────────────────────────────────────────────

    /// <summary>The phase at which a contract applies.</summary>
    public enum ContractPhase
    {
        /// <summary>Must hold before the prompt is sent.</summary>
        Precondition,
        /// <summary>Must hold after the response is received.</summary>
        Postcondition,
        /// <summary>Must hold at all times (both prompt and response).</summary>
        Invariant
    }

    /// <summary>Severity of a contract violation.</summary>
    public enum ContractSeverity
    {
        /// <summary>Advisory – minor style or preference issue.</summary>
        Advisory,
        /// <summary>Warning – could cause quality degradation.</summary>
        Warning,
        /// <summary>Error – likely to produce incorrect results.</summary>
        Error,
        /// <summary>Critical – fundamental contract breach.</summary>
        Critical
    }

    /// <summary>The type of check a contract performs.</summary>
    public enum ContractCheckType
    {
        /// <summary>Text must contain a substring.</summary>
        Contains,
        /// <summary>Text must not contain a substring.</summary>
        NotContains,
        /// <summary>Text must match a regex pattern.</summary>
        MatchesRegex,
        /// <summary>Text must not match a regex pattern.</summary>
        NotMatchesRegex,
        /// <summary>Text length must be within bounds.</summary>
        LengthBetween,
        /// <summary>Word count must be within bounds.</summary>
        WordCountBetween,
        /// <summary>Text must start with a prefix.</summary>
        StartsWith,
        /// <summary>Text must end with a suffix.</summary>
        EndsWith,
        /// <summary>Custom predicate function.</summary>
        CustomPredicate,
        /// <summary>Estimated token count must be within bounds.</summary>
        TokenCountBetween,
        /// <summary>Text must contain all specified keywords.</summary>
        ContainsAllKeywords,
        /// <summary>Text must contain at least one of specified keywords.</summary>
        ContainsAnyKeyword,
        /// <summary>Sentiment must not be negative (simple heuristic).</summary>
        NotNegativeSentiment,
        /// <summary>Response must be valid JSON.</summary>
        IsValidJson,
        /// <summary>Response must not echo the prompt verbatim.</summary>
        NoPromptLeakage
    }

    /// <summary>A single contract rule that can be verified against text.</summary>
    public class PromptContract
    {
        /// <summary>Unique identifier for this contract.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>Human-readable name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Description of what this contract enforces.</summary>
        public string Description { get; set; } = "";

        /// <summary>When this contract applies.</summary>
        public ContractPhase Phase { get; set; }

        /// <summary>What kind of check to perform.</summary>
        public ContractCheckType CheckType { get; set; }

        /// <summary>Severity if violated.</summary>
        public ContractSeverity Severity { get; set; } = ContractSeverity.Error;

        /// <summary>Primary parameter (substring, regex, prefix, etc.).</summary>
        public string Parameter { get; set; } = "";

        /// <summary>Secondary parameter (upper bound for range checks).</summary>
        public string Parameter2 { get; set; } = "";

        /// <summary>List of keywords for keyword-based checks.</summary>
        public List<string> Keywords { get; set; } = new();

        /// <summary>Custom predicate for CustomPredicate check type.</summary>
        public Func<string, bool>? Predicate { get; set; }

        /// <summary>Tags for grouping and filtering.</summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>Whether this contract is enabled.</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>Result of verifying a single contract.</summary>
    public class ContractVerificationResult
    {
        /// <summary>The contract that was checked.</summary>
        public PromptContract Contract { get; set; } = new();

        /// <summary>Whether the contract passed.</summary>
        public bool Passed { get; set; }

        /// <summary>Explanation of what happened.</summary>
        public string Message { get; set; } = "";

        /// <summary>The text that was checked.</summary>
        public string CheckedText { get; set; } = "";

        /// <summary>Actual measured value (for range checks).</summary>
        public string ActualValue { get; set; } = "";
    }

    /// <summary>Full report from verifying a prompt/response pair.</summary>
    public class ContractReport
    {
        /// <summary>All individual verification results.</summary>
        public List<ContractVerificationResult> Results { get; set; } = new();

        /// <summary>Prompt that was tested.</summary>
        public string Prompt { get; set; } = "";

        /// <summary>Response that was tested (if any).</summary>
        public string Response { get; set; } = "";

        /// <summary>When the verification ran.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Total contracts checked.</summary>
        public int TotalChecked => Results.Count;

        /// <summary>Number that passed.</summary>
        public int Passed => Results.Count(r => r.Passed);

        /// <summary>Number that failed.</summary>
        public int Failed => Results.Count(r => !r.Passed);

        /// <summary>Pass rate as a percentage.</summary>
        public double PassRate => TotalChecked == 0 ? 100.0 : (double)Passed / TotalChecked * 100.0;

        /// <summary>Highest severity among violations.</summary>
        public ContractSeverity? HighestViolationSeverity =>
            Results.Where(r => !r.Passed).Select(r => r.Contract.Severity).OrderByDescending(s => s).FirstOrDefault();

        /// <summary>Violations grouped by phase.</summary>
        public Dictionary<ContractPhase, List<ContractVerificationResult>> ViolationsByPhase =>
            Results.Where(r => !r.Passed).GroupBy(r => r.Contract.Phase).ToDictionary(g => g.Key, g => g.ToList());

        /// <summary>Violations grouped by severity.</summary>
        public Dictionary<ContractSeverity, int> ViolationsBySeverity =>
            Results.Where(r => !r.Passed).GroupBy(r => r.Contract.Severity).ToDictionary(g => g.Key, g => g.Count());

        /// <summary>Proactive improvement recommendations.</summary>
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>Result from batch verification of multiple prompt/response pairs.</summary>
    public class BatchContractReport
    {
        /// <summary>Individual reports for each pair.</summary>
        public List<ContractReport> Reports { get; set; } = new();

        /// <summary>Overall pass rate across all pairs.</summary>
        public double OverallPassRate =>
            Reports.Count == 0 ? 100.0 : Reports.Average(r => r.PassRate);

        /// <summary>Most frequently violated contracts.</summary>
        public List<(string ContractId, string ContractName, int ViolationCount)> TopViolations { get; set; } = new();

        /// <summary>Contracts that never failed (fully trusted).</summary>
        public List<string> FullyTrustedContracts { get; set; } = new();

        /// <summary>Proactive recommendations based on patterns.</summary>
        public List<string> PatternInsights { get; set; } = new();
    }

    /// <summary>
    /// Design-by-contract verification engine for prompts and responses.
    /// Define contracts (preconditions, postconditions, invariants), then verify
    /// prompt/response pairs against them for structured quality assurance.
    /// </summary>
    public class PromptContractTester
    {
        private readonly List<PromptContract> _contracts = new();

        // ── Negative-sentiment word list (simple heuristic) ──
        private static readonly HashSet<string> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "error", "fail", "failed", "failure", "wrong", "bad", "terrible",
            "horrible", "awful", "broken", "crash", "bug", "issue", "problem",
            "cannot", "unable", "refuse", "refused", "deny", "denied", "reject",
            "sorry", "unfortunately", "impossible", "never", "hate", "worst"
        };

        /// <summary>All registered contracts.</summary>
        public IReadOnlyList<PromptContract> Contracts => _contracts.AsReadOnly();

        // ── Contract Registration ──

        /// <summary>Add a custom contract.</summary>
        public PromptContractTester AddContract(PromptContract contract)
        {
            _contracts.Add(contract);
            return this;
        }

        /// <summary>Add a precondition that the prompt must contain a substring.</summary>
        public PromptContractTester RequirePromptContains(string substring, string? name = null, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name ?? $"Prompt must contain '{Truncate(substring, 30)}'",
                Phase = ContractPhase.Precondition,
                CheckType = ContractCheckType.Contains,
                Parameter = substring,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a precondition that the prompt must not contain a substring.</summary>
        public PromptContractTester RequirePromptNotContains(string substring, string? name = null, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name ?? $"Prompt must not contain '{Truncate(substring, 30)}'",
                Phase = ContractPhase.Precondition,
                CheckType = ContractCheckType.NotContains,
                Parameter = substring,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a precondition that the prompt must match a regex.</summary>
        public PromptContractTester RequirePromptMatches(string pattern, string? name = null, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name ?? $"Prompt must match /{Truncate(pattern, 30)}/",
                Phase = ContractPhase.Precondition,
                CheckType = ContractCheckType.MatchesRegex,
                Parameter = pattern,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a precondition that prompt length is within bounds.</summary>
        public PromptContractTester RequirePromptLength(int min, int max, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = $"Prompt length must be {min}–{max} chars",
                Phase = ContractPhase.Precondition,
                CheckType = ContractCheckType.LengthBetween,
                Parameter = min.ToString(),
                Parameter2 = max.ToString(),
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a precondition that prompt word count is within bounds.</summary>
        public PromptContractTester RequirePromptWordCount(int min, int max, ContractSeverity severity = ContractSeverity.Warning)
        {
            _contracts.Add(new PromptContract
            {
                Name = $"Prompt word count must be {min}–{max}",
                Phase = ContractPhase.Precondition,
                CheckType = ContractCheckType.WordCountBetween,
                Parameter = min.ToString(),
                Parameter2 = max.ToString(),
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition that the response must contain a substring.</summary>
        public PromptContractTester RequireResponseContains(string substring, string? name = null, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name ?? $"Response must contain '{Truncate(substring, 30)}'",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.Contains,
                Parameter = substring,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition that the response must not contain a substring.</summary>
        public PromptContractTester RequireResponseNotContains(string substring, string? name = null, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name ?? $"Response must not contain '{Truncate(substring, 30)}'",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.NotContains,
                Parameter = substring,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition that the response must be valid JSON.</summary>
        public PromptContractTester RequireResponseIsJson(ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = "Response must be valid JSON",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.IsValidJson,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition that the response must not leak the prompt.</summary>
        public PromptContractTester RequireNoPromptLeakage(ContractSeverity severity = ContractSeverity.Critical)
        {
            _contracts.Add(new PromptContract
            {
                Name = "Response must not echo prompt verbatim",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.NoPromptLeakage,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition on response length.</summary>
        public PromptContractTester RequireResponseLength(int min, int max, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = $"Response length must be {min}–{max} chars",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.LengthBetween,
                Parameter = min.ToString(),
                Parameter2 = max.ToString(),
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition on response word count.</summary>
        public PromptContractTester RequireResponseWordCount(int min, int max, ContractSeverity severity = ContractSeverity.Warning)
        {
            _contracts.Add(new PromptContract
            {
                Name = $"Response word count must be {min}–{max}",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.WordCountBetween,
                Parameter = min.ToString(),
                Parameter2 = max.ToString(),
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition that the response sentiment is not negative.</summary>
        public PromptContractTester RequirePositiveResponse(ContractSeverity severity = ContractSeverity.Warning)
        {
            _contracts.Add(new PromptContract
            {
                Name = "Response should not have negative sentiment",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.NotNegativeSentiment,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition that the response contains all specified keywords.</summary>
        public PromptContractTester RequireResponseContainsAll(IEnumerable<string> keywords, string? name = null, ContractSeverity severity = ContractSeverity.Error)
        {
            var kw = keywords.ToList();
            _contracts.Add(new PromptContract
            {
                Name = name ?? $"Response must contain all: [{string.Join(", ", kw.Take(3))}{(kw.Count > 3 ? "..." : "")}]",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.ContainsAllKeywords,
                Keywords = kw,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a postcondition that the response contains at least one keyword.</summary>
        public PromptContractTester RequireResponseContainsAny(IEnumerable<string> keywords, string? name = null, ContractSeverity severity = ContractSeverity.Warning)
        {
            var kw = keywords.ToList();
            _contracts.Add(new PromptContract
            {
                Name = name ?? $"Response must contain any: [{string.Join(", ", kw.Take(3))}{(kw.Count > 3 ? "..." : "")}]",
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.ContainsAnyKeyword,
                Keywords = kw,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add an invariant that must hold for both prompt and response.</summary>
        public PromptContractTester AddInvariant(string name, ContractCheckType checkType, string parameter = "", string parameter2 = "", ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name,
                Phase = ContractPhase.Invariant,
                CheckType = checkType,
                Parameter = parameter,
                Parameter2 = parameter2,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add an invariant with a custom predicate.</summary>
        public PromptContractTester AddInvariant(string name, Func<string, bool> predicate, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name,
                Phase = ContractPhase.Invariant,
                CheckType = ContractCheckType.CustomPredicate,
                Predicate = predicate,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a custom precondition predicate.</summary>
        public PromptContractTester RequirePrompt(string name, Func<string, bool> predicate, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name,
                Phase = ContractPhase.Precondition,
                CheckType = ContractCheckType.CustomPredicate,
                Predicate = predicate,
                Severity = severity
            });
            return this;
        }

        /// <summary>Add a custom postcondition predicate.</summary>
        public PromptContractTester RequireResponse(string name, Func<string, bool> predicate, ContractSeverity severity = ContractSeverity.Error)
        {
            _contracts.Add(new PromptContract
            {
                Name = name,
                Phase = ContractPhase.Postcondition,
                CheckType = ContractCheckType.CustomPredicate,
                Predicate = predicate,
                Severity = severity
            });
            return this;
        }

        // ── Preset Contract Suites ──

        /// <summary>Load a safety-focused contract suite (injection, leakage, length bounds).</summary>
        public PromptContractTester UseSafetyPreset()
        {
            RequirePromptLength(1, 50_000, ContractSeverity.Error);
            RequirePromptNotContains("ignore all previous instructions", "No instruction override", ContractSeverity.Critical);
            RequirePromptNotContains("disregard", "No disregard pattern", ContractSeverity.Warning);
            RequireNoPromptLeakage();
            RequireResponseLength(1, 100_000, ContractSeverity.Error);
            RequireResponseNotContains("As an AI language model", "No AI self-reference", ContractSeverity.Advisory);
            return this;
        }

        /// <summary>Load a JSON-output contract suite.</summary>
        public PromptContractTester UseJsonOutputPreset()
        {
            RequirePromptContains("JSON", "Prompt should mention JSON format", ContractSeverity.Warning);
            RequireResponseIsJson();
            RequireResponseLength(2, 100_000, ContractSeverity.Error);
            RequireResponseNotContains("```", "No markdown code fences in JSON output", ContractSeverity.Warning);
            return this;
        }

        /// <summary>Load a conversational quality contract suite.</summary>
        public PromptContractTester UseConversationalPreset()
        {
            RequirePromptLength(1, 10_000, ContractSeverity.Warning);
            RequireResponseLength(10, 50_000, ContractSeverity.Warning);
            RequireResponseWordCount(3, 5000, ContractSeverity.Warning);
            RequirePositiveResponse(ContractSeverity.Advisory);
            RequireNoPromptLeakage();
            return this;
        }

        // ── Verification ──

        /// <summary>Verify a prompt (preconditions only, before sending).</summary>
        public ContractReport VerifyPrompt(string prompt)
        {
            var results = new List<ContractVerificationResult>();
            foreach (var c in _contracts.Where(c => c.Enabled))
            {
                if (c.Phase == ContractPhase.Precondition || c.Phase == ContractPhase.Invariant)
                {
                    results.Add(Evaluate(c, prompt, null));
                }
            }
            var report = new ContractReport { Prompt = prompt, Results = results };
            report.Recommendations = GenerateRecommendations(report);
            return report;
        }

        /// <summary>Verify a prompt/response pair (all contract phases).</summary>
        public ContractReport Verify(string prompt, string response)
        {
            var results = new List<ContractVerificationResult>();
            foreach (var c in _contracts.Where(c => c.Enabled))
            {
                switch (c.Phase)
                {
                    case ContractPhase.Precondition:
                        results.Add(Evaluate(c, prompt, null));
                        break;
                    case ContractPhase.Postcondition:
                        results.Add(Evaluate(c, response, prompt));
                        break;
                    case ContractPhase.Invariant:
                        // Invariants check both prompt and response
                        var promptResult = Evaluate(c, prompt, null);
                        if (!promptResult.Passed)
                        {
                            promptResult.Message = $"[Prompt] {promptResult.Message}";
                            results.Add(promptResult);
                        }
                        var responseResult = Evaluate(c, response, prompt);
                        if (!responseResult.Passed)
                        {
                            responseResult.Message = $"[Response] {responseResult.Message}";
                            results.Add(responseResult);
                        }
                        if (promptResult.Passed && responseResult.Passed)
                        {
                            results.Add(promptResult); // Add one pass record
                        }
                        break;
                }
            }
            var report = new ContractReport { Prompt = prompt, Response = response, Results = results };
            report.Recommendations = GenerateRecommendations(report);
            return report;
        }

        /// <summary>Batch-verify multiple prompt/response pairs and generate aggregate insights.</summary>
        public BatchContractReport VerifyBatch(IEnumerable<(string Prompt, string Response)> pairs)
        {
            var reports = pairs.Select(p => Verify(p.Prompt, p.Response)).ToList();
            var batch = new BatchContractReport { Reports = reports };

            // Top violations
            var violationCounts = reports
                .SelectMany(r => r.Results.Where(v => !v.Passed))
                .GroupBy(v => v.Contract.Id)
                .Select(g => (Id: g.Key, Name: g.First().Contract.Name, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();
            batch.TopViolations = violationCounts;

            // Fully trusted contracts
            var allContractIds = _contracts.Select(c => c.Id).ToHashSet();
            var violatedIds = reports
                .SelectMany(r => r.Results.Where(v => !v.Passed).Select(v => v.Contract.Id))
                .ToHashSet();
            batch.FullyTrustedContracts = allContractIds.Except(violatedIds).ToList();

            // Pattern insights
            batch.PatternInsights = GenerateBatchInsights(batch);

            return batch;
        }

        // ── Export ──

        /// <summary>Export a contract report as Markdown.</summary>
        public static string ToMarkdown(ContractReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Prompt Contract Verification Report");
            sb.AppendLine();
            sb.AppendLine($"**Timestamp:** {report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"**Pass Rate:** {report.PassRate:F1}% ({report.Passed}/{report.TotalChecked})");

            if (report.HighestViolationSeverity.HasValue)
                sb.AppendLine($"**Highest Violation:** {report.HighestViolationSeverity}");

            sb.AppendLine();
            sb.AppendLine("## Results");
            sb.AppendLine();
            sb.AppendLine("| Status | Phase | Severity | Contract | Message |");
            sb.AppendLine("|--------|-------|----------|----------|---------|");

            foreach (var r in report.Results)
            {
                var status = r.Passed ? "✅" : "❌";
                sb.AppendLine($"| {status} | {r.Contract.Phase} | {r.Contract.Severity} | {EscapeMd(r.Contract.Name)} | {EscapeMd(r.Message)} |");
            }

            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Recommendations");
                sb.AppendLine();
                foreach (var rec in report.Recommendations)
                    sb.AppendLine($"- {rec}");
            }

            return sb.ToString();
        }

        /// <summary>Export a contract report as JSON.</summary>
        public static string ToJson(ContractReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"timestamp\": \"{report.Timestamp:O}\",");
            sb.AppendLine($"  \"passRate\": {report.PassRate:F1},");
            sb.AppendLine($"  \"total\": {report.TotalChecked},");
            sb.AppendLine($"  \"passed\": {report.Passed},");
            sb.AppendLine($"  \"failed\": {report.Failed},");
            sb.AppendLine("  \"results\": [");

            for (int i = 0; i < report.Results.Count; i++)
            {
                var r = report.Results[i];
                var comma = i < report.Results.Count - 1 ? "," : "";
                sb.AppendLine($"    {{\"contract\": \"{EscapeJson(r.Contract.Name)}\", \"phase\": \"{r.Contract.Phase}\", \"severity\": \"{r.Contract.Severity}\", \"passed\": {(r.Passed ? "true" : "false")}, \"message\": \"{EscapeJson(r.Message)}\"}}{comma}");
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"recommendations\": [");
            for (int i = 0; i < report.Recommendations.Count; i++)
            {
                var comma = i < report.Recommendations.Count - 1 ? "," : "";
                sb.AppendLine($"    \"{EscapeJson(report.Recommendations[i])}\"{comma}");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── Internal Evaluation ──

        private ContractVerificationResult Evaluate(PromptContract contract, string text, string? promptForLeakage)
        {
            var result = new ContractVerificationResult
            {
                Contract = contract,
                CheckedText = Truncate(text, 100)
            };

            try
            {
                switch (contract.CheckType)
                {
                    case ContractCheckType.Contains:
                        result.Passed = text.Contains(contract.Parameter, StringComparison.OrdinalIgnoreCase);
                        result.Message = result.Passed
                            ? $"Found '{Truncate(contract.Parameter, 30)}'"
                            : $"Missing required substring '{Truncate(contract.Parameter, 30)}'";
                        break;

                    case ContractCheckType.NotContains:
                        result.Passed = !text.Contains(contract.Parameter, StringComparison.OrdinalIgnoreCase);
                        result.Message = result.Passed
                            ? $"Correctly absent: '{Truncate(contract.Parameter, 30)}'"
                            : $"Found forbidden substring '{Truncate(contract.Parameter, 30)}'";
                        break;

                    case ContractCheckType.MatchesRegex:
                        result.Passed = Regex.IsMatch(text, contract.Parameter, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                        result.Message = result.Passed
                            ? $"Matches pattern /{Truncate(contract.Parameter, 30)}/"
                            : $"Does not match required pattern /{Truncate(contract.Parameter, 30)}/";
                        break;

                    case ContractCheckType.NotMatchesRegex:
                        result.Passed = !Regex.IsMatch(text, contract.Parameter, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                        result.Message = result.Passed
                            ? $"Correctly does not match /{Truncate(contract.Parameter, 30)}/"
                            : $"Matches forbidden pattern /{Truncate(contract.Parameter, 30)}/";
                        break;

                    case ContractCheckType.LengthBetween:
                        {
                            int min = int.Parse(contract.Parameter);
                            int max = int.Parse(contract.Parameter2);
                            int len = text.Length;
                            result.Passed = len >= min && len <= max;
                            result.ActualValue = len.ToString();
                            result.Message = result.Passed
                                ? $"Length {len} is within [{min}, {max}]"
                                : $"Length {len} is outside [{min}, {max}]";
                        }
                        break;

                    case ContractCheckType.WordCountBetween:
                        {
                            int min = int.Parse(contract.Parameter);
                            int max = int.Parse(contract.Parameter2);
                            int wc = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                            result.Passed = wc >= min && wc <= max;
                            result.ActualValue = wc.ToString();
                            result.Message = result.Passed
                                ? $"Word count {wc} is within [{min}, {max}]"
                                : $"Word count {wc} is outside [{min}, {max}]";
                        }
                        break;

                    case ContractCheckType.StartsWith:
                        result.Passed = text.StartsWith(contract.Parameter, StringComparison.OrdinalIgnoreCase);
                        result.Message = result.Passed
                            ? $"Starts with '{Truncate(contract.Parameter, 30)}'"
                            : $"Does not start with '{Truncate(contract.Parameter, 30)}'";
                        break;

                    case ContractCheckType.EndsWith:
                        result.Passed = text.EndsWith(contract.Parameter, StringComparison.OrdinalIgnoreCase);
                        result.Message = result.Passed
                            ? $"Ends with '{Truncate(contract.Parameter, 30)}'"
                            : $"Does not end with '{Truncate(contract.Parameter, 30)}'";
                        break;

                    case ContractCheckType.CustomPredicate:
                        if (contract.Predicate != null)
                        {
                            result.Passed = contract.Predicate(text);
                            result.Message = result.Passed ? "Custom predicate passed" : "Custom predicate failed";
                        }
                        else
                        {
                            result.Passed = false;
                            result.Message = "No predicate function defined";
                        }
                        break;

                    case ContractCheckType.TokenCountBetween:
                        {
                            int min = int.Parse(contract.Parameter);
                            int max = int.Parse(contract.Parameter2);
                            // Rough token estimate: ~4 chars per token
                            int tokens = Math.Max(1, text.Length / 4);
                            result.Passed = tokens >= min && tokens <= max;
                            result.ActualValue = tokens.ToString();
                            result.Message = result.Passed
                                ? $"Estimated {tokens} tokens is within [{min}, {max}]"
                                : $"Estimated {tokens} tokens is outside [{min}, {max}]";
                        }
                        break;

                    case ContractCheckType.ContainsAllKeywords:
                        {
                            var missing = contract.Keywords.Where(k => !text.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
                            result.Passed = missing.Count == 0;
                            result.Message = result.Passed
                                ? $"All {contract.Keywords.Count} keywords found"
                                : $"Missing keywords: {string.Join(", ", missing.Take(5))}";
                        }
                        break;

                    case ContractCheckType.ContainsAnyKeyword:
                        {
                            var found = contract.Keywords.FirstOrDefault(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
                            result.Passed = found != null;
                            result.Message = result.Passed
                                ? $"Found keyword '{found}'"
                                : $"None of {contract.Keywords.Count} keywords found";
                        }
                        break;

                    case ContractCheckType.NotNegativeSentiment:
                        {
                            var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                            int total = words.Length;
                            int negCount = words.Count(w => NegativeWords.Contains(w.Trim('.', ',', '!', '?', ';', ':')));
                            double negRatio = total == 0 ? 0 : (double)negCount / total;
                            result.Passed = negRatio < 0.15;
                            result.ActualValue = $"{negRatio:P1}";
                            result.Message = result.Passed
                                ? $"Negative word ratio {negRatio:P1} is acceptable"
                                : $"Negative word ratio {negRatio:P1} exceeds 15% threshold ({negCount} negative words)";
                        }
                        break;

                    case ContractCheckType.IsValidJson:
                        {
                            var trimmed = text.Trim();
                            result.Passed = (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                                            (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
                            // Basic structural check + try parse
                            if (result.Passed)
                            {
                                try
                                {
                                    System.Text.Json.JsonDocument.Parse(trimmed);
                                    result.Message = "Valid JSON structure";
                                }
                                catch (System.Text.Json.JsonException ex)
                                {
                                    result.Passed = false;
                                    result.Message = $"Invalid JSON: {Truncate(ex.Message, 60)}";
                                }
                            }
                            else
                            {
                                result.Message = "Text does not start with {{ or [";
                            }
                        }
                        break;

                    case ContractCheckType.NoPromptLeakage:
                        {
                            if (promptForLeakage != null && promptForLeakage.Length > 20)
                            {
                                // Check if response contains a significant portion of the prompt
                                var promptChunks = ChunkText(promptForLeakage, 50);
                                int leakedChunks = promptChunks.Count(chunk => text.Contains(chunk, StringComparison.OrdinalIgnoreCase));
                                double leakRatio = promptChunks.Count == 0 ? 0 : (double)leakedChunks / promptChunks.Count;
                                result.Passed = leakRatio < 0.5;
                                result.ActualValue = $"{leakRatio:P0}";
                                result.Message = result.Passed
                                    ? $"Prompt leakage ratio {leakRatio:P0} is acceptable"
                                    : $"Prompt leakage detected: {leakRatio:P0} of prompt echoed in response";
                            }
                            else
                            {
                                result.Passed = true;
                                result.Message = "Prompt too short to check for leakage";
                            }
                        }
                        break;

                    default:
                        result.Passed = false;
                        result.Message = $"Unknown check type: {contract.CheckType}";
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Message = $"Evaluation error: {Truncate(ex.Message, 80)}";
            }

            return result;
        }

        private List<string> GenerateRecommendations(ContractReport report)
        {
            var recs = new List<string>();
            var violations = report.Results.Where(r => !r.Passed).ToList();

            if (violations.Count == 0)
            {
                recs.Add("All contracts satisfied. Consider adding more specific contracts to increase coverage.");
                return recs;
            }

            var criticals = violations.Where(v => v.Contract.Severity == ContractSeverity.Critical).ToList();
            if (criticals.Count > 0)
                recs.Add($"🚨 {criticals.Count} critical violation(s) detected — address these immediately before deployment.");

            var preconditionFails = violations.Where(v => v.Contract.Phase == ContractPhase.Precondition).ToList();
            if (preconditionFails.Count > 0)
                recs.Add($"Fix {preconditionFails.Count} precondition failure(s) — the prompt itself needs adjustment before sending.");

            var postconditionFails = violations.Where(v => v.Contract.Phase == ContractPhase.Postcondition).ToList();
            if (postconditionFails.Count > 0)
                recs.Add($"Address {postconditionFails.Count} postcondition failure(s) — consider adding output format instructions to the prompt.");

            if (violations.Any(v => v.Contract.CheckType == ContractCheckType.NoPromptLeakage))
                recs.Add("Prompt leakage detected — add explicit 'do not repeat these instructions' to your system prompt.");

            if (violations.Any(v => v.Contract.CheckType == ContractCheckType.IsValidJson))
                recs.Add("JSON validation failed — ensure your prompt explicitly requests JSON output and specifies the schema.");

            if (violations.Any(v => v.Contract.CheckType == ContractCheckType.NotNegativeSentiment))
                recs.Add("Negative sentiment detected — consider adjusting prompt tone or adding positivity constraints.");

            return recs;
        }

        private List<string> GenerateBatchInsights(BatchContractReport batch)
        {
            var insights = new List<string>();

            if (batch.Reports.Count == 0) return insights;

            if (batch.OverallPassRate >= 95)
                insights.Add("✅ Excellent contract compliance across the batch (>95% pass rate).");
            else if (batch.OverallPassRate >= 80)
                insights.Add("⚠️ Good compliance but some contracts are frequently violated — review top violations.");
            else
                insights.Add("🚨 Low compliance rate — contracts may be too strict or prompts need significant rework.");

            if (batch.TopViolations.Count > 0)
            {
                var top = batch.TopViolations[0];
                insights.Add($"Most violated contract: '{top.ContractName}' failed {top.ViolationCount}/{batch.Reports.Count} times.");
            }

            if (batch.FullyTrustedContracts.Count > 0)
                insights.Add($"{batch.FullyTrustedContracts.Count} contract(s) never violated — these are well-established guarantees.");

            // Check for degradation pattern
            if (batch.Reports.Count >= 3)
            {
                var lastThree = batch.Reports.TakeLast(3).Select(r => r.PassRate).ToList();
                if (lastThree[0] > lastThree[1] && lastThree[1] > lastThree[2])
                    insights.Add("📉 Pass rate is declining across recent entries — possible quality drift detected.");
            }

            return insights;
        }

        // ── Helpers ──

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max] + "…";

        private static string EscapeMd(string s) =>
            s.Replace("|", "\\|").Replace("\n", " ");

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static List<string> ChunkText(string text, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i + chunkSize <= text.Length; i += chunkSize)
                chunks.Add(text.Substring(i, chunkSize));
            return chunks;
        }
    }
}
