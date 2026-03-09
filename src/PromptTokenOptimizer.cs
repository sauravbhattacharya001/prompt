namespace Prompt
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Represents a distinct section identified within a prompt.
    /// </summary>
    public class PromptSection
    {
        /// <summary>Gets the section heading or identifier.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the raw text content of the section.</summary>
        public string Content { get; internal set; } = "";

        /// <summary>Gets the estimated token count for this section.</summary>
        public int TokenCount { get; internal set; }

        /// <summary>Gets the percentage of total prompt tokens this section uses.</summary>
        public double PercentOfTotal { get; internal set; }

        /// <summary>Gets the start character index in the original prompt.</summary>
        public int StartIndex { get; internal set; }

        /// <summary>Gets the end character index in the original prompt.</summary>
        public int EndIndex { get; internal set; }
    }

    /// <summary>
    /// Represents a pair of instructions that appear redundant.
    /// </summary>
    public class RedundancyPair
    {
        /// <summary>Gets the first instruction text.</summary>
        public string InstructionA { get; internal set; } = "";

        /// <summary>Gets the second instruction text.</summary>
        public string InstructionB { get; internal set; } = "";

        /// <summary>Gets the similarity score (0.0–1.0) between the two instructions.</summary>
        public double Similarity { get; internal set; }

        /// <summary>Gets a suggested merged instruction.</summary>
        public string SuggestedMerge { get; internal set; } = "";

        /// <summary>Gets the estimated tokens saved by merging.</summary>
        public int EstimatedTokensSaved { get; internal set; }
    }

    /// <summary>
    /// A specific optimization recommendation.
    /// </summary>
    public class OptimizationRecommendation
    {
        /// <summary>Gets the recommendation category.</summary>
        public OptimizationCategory Category { get; internal set; }

        /// <summary>Gets a description of the issue.</summary>
        public string Description { get; internal set; } = "";

        /// <summary>Gets the original text span (if applicable).</summary>
        public string OriginalText { get; internal set; } = "";

        /// <summary>Gets the suggested replacement (if applicable).</summary>
        public string SuggestedText { get; internal set; } = "";

        /// <summary>Gets the estimated tokens saved.</summary>
        public int EstimatedTokensSaved { get; internal set; }

        /// <summary>Gets the confidence level (0.0–1.0).</summary>
        public double Confidence { get; internal set; }

        /// <summary>Gets the severity (higher = more impactful).</summary>
        public OptimizationSeverity Severity { get; internal set; }
    }

    /// <summary>
    /// Categories of optimization recommendations.
    /// </summary>
    public enum OptimizationCategory
    {
        /// <summary>Redundant or duplicate instructions.</summary>
        Redundancy,
        /// <summary>Verbose phrasing that can be condensed.</summary>
        Verbosity,
        /// <summary>Structural improvements for clarity.</summary>
        Structure,
        /// <summary>Unnecessary examples or context.</summary>
        ExcessiveContext,
        /// <summary>Filler words and phrases.</summary>
        FillerWords,
        /// <summary>Repeated formatting instructions.</summary>
        FormattingOverhead,
        /// <summary>Overly specific constraints that could be generalized.</summary>
        OverSpecification
    }

    /// <summary>
    /// Severity of an optimization recommendation.
    /// </summary>
    public enum OptimizationSeverity
    {
        /// <summary>Minor saving, low risk.</summary>
        Low,
        /// <summary>Moderate saving.</summary>
        Medium,
        /// <summary>Significant saving, high impact.</summary>
        High,
        /// <summary>Critical — large portion of budget wasted.</summary>
        Critical
    }

    /// <summary>
    /// Complete result of prompt optimization analysis.
    /// </summary>
    public class OptimizationReport
    {
        /// <summary>Gets the original prompt text.</summary>
        public string OriginalPrompt { get; internal set; } = "";

        /// <summary>Gets the estimated total token count.</summary>
        public int TotalTokens { get; internal set; }

        /// <summary>Gets the sections identified in the prompt.</summary>
        public IReadOnlyList<PromptSection> Sections { get; internal set; }
            = Array.Empty<PromptSection>();

        /// <summary>Gets detected redundancy pairs.</summary>
        public IReadOnlyList<RedundancyPair> Redundancies { get; internal set; }
            = Array.Empty<RedundancyPair>();

        /// <summary>Gets all optimization recommendations.</summary>
        public IReadOnlyList<OptimizationRecommendation> Recommendations { get; internal set; }
            = Array.Empty<OptimizationRecommendation>();

        /// <summary>Gets the total estimated tokens that could be saved.</summary>
        public int TotalPotentialSavings => Recommendations.Sum(r => r.EstimatedTokensSaved);

        /// <summary>Gets the potential savings as a percentage of total tokens.</summary>
        public double SavingsPercent => TotalTokens > 0
            ? Math.Round((double)TotalPotentialSavings / TotalTokens * 100, 1)
            : 0;

        /// <summary>Gets an overall optimization score (0–100, higher = more optimized already).</summary>
        public int OptimizationScore { get; internal set; }

        /// <summary>Gets the optimized prompt after applying safe recommendations.</summary>
        public string OptimizedPrompt { get; internal set; } = "";

        /// <summary>Gets the token count after optimization.</summary>
        public int OptimizedTokens { get; internal set; }

        /// <summary>Generates a human-readable summary of the report.</summary>
        public string Summary()
        {
            var lines = new List<string>
            {
                $"=== Prompt Optimization Report ===",
                $"Total tokens: {TotalTokens}",
                $"Sections: {Sections.Count}",
                $"Redundancies found: {Redundancies.Count}",
                $"Recommendations: {Recommendations.Count}",
                $"Potential savings: {TotalPotentialSavings} tokens ({SavingsPercent}%)",
                $"Optimization score: {OptimizationScore}/100",
                $"Optimized tokens: {OptimizedTokens}",
                ""
            };

            if (Sections.Count > 0)
            {
                lines.Add("--- Section Breakdown ---");
                foreach (var s in Sections)
                    lines.Add($"  [{s.Name}] {s.TokenCount} tokens ({s.PercentOfTotal:F1}%)");
                lines.Add("");
            }

            if (Recommendations.Count > 0)
            {
                lines.Add("--- Top Recommendations ---");
                foreach (var r in Recommendations.OrderByDescending(x => x.EstimatedTokensSaved).Take(5))
                    lines.Add($"  [{r.Severity}] {r.Category}: {r.Description} (save ~{r.EstimatedTokensSaved} tokens)");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Configuration for the prompt token optimizer.
    /// </summary>
    public class OptimizerConfig
    {
        /// <summary>Similarity threshold for redundancy detection (0.0–1.0). Default: 0.7.</summary>
        public double RedundancyThreshold { get; set; } = 0.7;

        /// <summary>Minimum tokens in a section to flag for verbosity. Default: 50.</summary>
        public int VerbosityThreshold { get; set; } = 50;

        /// <summary>Whether to auto-apply safe optimizations. Default: true.</summary>
        public bool AutoApply { get; set; } = true;

        /// <summary>Maximum confidence required to auto-apply (only applies recs at or above). Default: 0.8.</summary>
        public double AutoApplyMinConfidence { get; set; } = 0.8;

        /// <summary>Token budget limit. If set, highlights when prompt exceeds budget. Default: null (no limit).</summary>
        public int? TokenBudget { get; set; }

        /// <summary>Custom section delimiter regex. Default: matches markdown headings and double newlines.</summary>
        public string SectionPattern { get; set; } = @"(?=^#{1,3}\s)|(?=\n\n)";
    }

    /// <summary>
    /// Analyzes prompts for token efficiency and provides optimization recommendations.
    /// Goes beyond simple minification to detect redundant instructions, analyze section costs,
    /// and suggest structural improvements.
    /// </summary>
    public class PromptTokenOptimizer
    {
        private readonly OptimizerConfig _config;

        // Common filler patterns with their condensed forms
        private static readonly (Regex Pattern, string Replacement, string Description)[] FillerPatterns = new[]
        {
            (new Regex(@"\bplease\s+make\s+sure\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "ensure", "Verbose instruction phrase"),
            (new Regex(@"\bI\s+would\s+like\s+you\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "", "Unnecessary preamble"),
            (new Regex(@"\bIt\s+is\s+important\s+(?:that\s+)?(?:you\s+)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "", "Unnecessary emphasis"),
            (new Regex(@"\bplease\s+note\s+that\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "Note:", "Verbose note marker"),
            (new Regex(@"\bIn\s+order\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "To", "Verbose 'in order to'"),
            (new Regex(@"\bDue\s+to\s+the\s+fact\s+that\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "Because", "Verbose 'due to the fact that'"),
            (new Regex(@"\bAt\s+this\s+point\s+in\s+time\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "Now", "Verbose time reference"),
            (new Regex(@"\bfor\s+the\s+purpose\s+of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "for", "Verbose 'for the purpose of'"),
            (new Regex(@"\bin\s+the\s+event\s+that\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "if", "Verbose conditional"),
            (new Regex(@"\bwith\s+regard\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "regarding", "Verbose 'with regard to'"),
            (new Regex(@"\bAs\s+a\s+matter\s+of\s+fact\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "In fact", "Verbose filler"),
            (new Regex(@"\bIt\s+should\s+be\s+noted\s+that\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "Note:", "Passive filler"),
            (new Regex(@"\bYou\s+should\s+always\s+make\s+sure\s+(?:that\s+)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "Always ", "Verbose instruction"),
            (new Regex(@"\bI\s+need\s+you\s+to\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)), "", "Unnecessary preamble"),
        };

        // Repeated formatting instruction patterns
        private static readonly Regex[] FormattingPatterns = new[]
        {
            new Regex(@"\bformat\s+(?:the\s+)?(?:output|response|answer)\s+(?:as|in|using)\s+(?:JSON|json)\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
            new Regex(@"\breturn\s+(?:the\s+)?(?:result|output|response)\s+(?:as|in)\s+(?:JSON|json)\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
            new Regex(@"\buse\s+(?:bullet\s+)?(?:points|list|markdown)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
            new Regex(@"\brespond\s+in\s+(?:JSON|json|XML|xml|markdown)\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500)),
        };

        /// <summary>
        /// Creates a new <see cref="PromptTokenOptimizer"/> with default configuration.
        /// </summary>
        public PromptTokenOptimizer() : this(new OptimizerConfig()) { }

        /// <summary>
        /// Creates a new <see cref="PromptTokenOptimizer"/> with the specified configuration.
        /// </summary>
        public PromptTokenOptimizer(OptimizerConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Analyzes a prompt and returns a comprehensive optimization report.
        /// </summary>
        public OptimizationReport Analyze(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return new OptimizationReport
                {
                    OriginalPrompt = prompt ?? "",
                    OptimizedPrompt = prompt ?? "",
                    OptimizationScore = 100
                };
            }

            var totalTokens = EstimateTokens(prompt);
            var sections = IdentifySections(prompt, totalTokens);
            var instructions = ExtractInstructions(prompt);
            var redundancies = DetectRedundancies(instructions);
            var recommendations = new List<OptimizationRecommendation>();

            recommendations.AddRange(DetectFillerWords(prompt));
            recommendations.AddRange(DetectVerbosity(sections));
            recommendations.AddRange(DetectFormattingOverhead(prompt));
            recommendations.AddRange(DetectExcessiveContext(prompt));
            recommendations.AddRange(DetectOverSpecification(prompt));

            foreach (var r in redundancies)
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    Category = OptimizationCategory.Redundancy,
                    Description = $"Redundant instructions (similarity: {r.Similarity:F2})",
                    OriginalText = $"{r.InstructionA}\n---\n{r.InstructionB}",
                    SuggestedText = r.SuggestedMerge,
                    EstimatedTokensSaved = r.EstimatedTokensSaved,
                    Confidence = r.Similarity,
                    Severity = r.EstimatedTokensSaved > 20 ? OptimizationSeverity.High
                        : r.EstimatedTokensSaved > 10 ? OptimizationSeverity.Medium
                        : OptimizationSeverity.Low
                });
            }

            if (_config.TokenBudget.HasValue && totalTokens > _config.TokenBudget.Value)
            {
                var overage = totalTokens - _config.TokenBudget.Value;
                recommendations.Add(new OptimizationRecommendation
                {
                    Category = OptimizationCategory.Structure,
                    Description = $"Prompt exceeds token budget by {overage} tokens ({_config.TokenBudget.Value} limit)",
                    EstimatedTokensSaved = overage,
                    Confidence = 1.0,
                    Severity = OptimizationSeverity.Critical
                });
            }

            var optimizedPrompt = _config.AutoApply
                ? ApplyOptimizations(prompt, recommendations)
                : prompt;
            var optimizedTokens = EstimateTokens(optimizedPrompt);

            var score = CalculateOptimizationScore(totalTokens, recommendations);

            return new OptimizationReport
            {
                OriginalPrompt = prompt,
                TotalTokens = totalTokens,
                Sections = sections,
                Redundancies = redundancies,
                Recommendations = recommendations,
                OptimizationScore = score,
                OptimizedPrompt = optimizedPrompt,
                OptimizedTokens = optimizedTokens
            };
        }

        /// <summary>
        /// Compares two prompts and identifies which is more token-efficient for the same intent.
        /// </summary>
        public TokenEfficiencyComparison Compare(string promptA, string promptB)
        {
            var reportA = Analyze(promptA);
            var reportB = Analyze(promptB);

            return new TokenEfficiencyComparison
            {
                ReportA = reportA,
                ReportB = reportB,
                TokenDifference = reportA.TotalTokens - reportB.TotalTokens,
                MoreEfficientPrompt = reportA.OptimizationScore >= reportB.OptimizationScore ? "A" : "B",
                ScoreDifference = Math.Abs(reportA.OptimizationScore - reportB.OptimizationScore)
            };
        }

        /// <summary>
        /// Estimates the token count for a budget check against a specific model's limit.
        /// </summary>
        public BudgetCheck CheckBudget(string prompt, int tokenLimit, double reserveRatio = 0.2)
        {
            var tokens = EstimateTokens(prompt);
            var reserveTokens = (int)(tokenLimit * reserveRatio);
            var availableForPrompt = tokenLimit - reserveTokens;

            return new BudgetCheck
            {
                PromptTokens = tokens,
                TokenLimit = tokenLimit,
                ReserveTokens = reserveTokens,
                AvailableForPrompt = availableForPrompt,
                WithinBudget = tokens <= availableForPrompt,
                UtilizationPercent = Math.Round((double)tokens / availableForPrompt * 100, 1),
                TokensRemaining = availableForPrompt - tokens
            };
        }

        /// <summary>
        /// Suggests how to split a prompt if it exceeds a token limit.
        /// </summary>
        public IReadOnlyList<string> SuggestSplit(string prompt, int maxTokensPerChunk)
        {
            if (string.IsNullOrEmpty(prompt) || maxTokensPerChunk <= 0)
                return new[] { prompt ?? "" };

            var totalTokens = EstimateTokens(prompt);
            if (totalTokens <= maxTokensPerChunk)
                return new[] { prompt };

            var sections = IdentifySections(prompt, totalTokens);
            var chunks = new List<string>();
            var currentChunk = new List<string>();
            var currentTokens = 0;

            foreach (var section in sections)
            {
                if (currentTokens + section.TokenCount > maxTokensPerChunk && currentChunk.Count > 0)
                {
                    chunks.Add(string.Join("\n\n", currentChunk));
                    currentChunk.Clear();
                    currentTokens = 0;
                }

                if (section.TokenCount > maxTokensPerChunk)
                {
                    // Split oversized section by sentences
                    if (currentChunk.Count > 0)
                    {
                        chunks.Add(string.Join("\n\n", currentChunk));
                        currentChunk.Clear();
                        currentTokens = 0;
                    }
                    var sentences = Regex.Split(section.Content, @"(?<=[.!?])\s+", RegexOptions.None, TimeSpan.FromMilliseconds(500));
                    var sentenceChunk = new List<string>();
                    var sentenceTokens = 0;
                    foreach (var s in sentences)
                    {
                        var st = EstimateTokens(s);
                        if (sentenceTokens + st > maxTokensPerChunk && sentenceChunk.Count > 0)
                        {
                            chunks.Add(string.Join(" ", sentenceChunk));
                            sentenceChunk.Clear();
                            sentenceTokens = 0;
                        }
                        sentenceChunk.Add(s);
                        sentenceTokens += st;
                    }
                    if (sentenceChunk.Count > 0)
                    {
                        currentChunk.Add(string.Join(" ", sentenceChunk));
                        currentTokens += sentenceTokens;
                    }
                }
                else
                {
                    currentChunk.Add(section.Content);
                    currentTokens += section.TokenCount;
                }
            }

            if (currentChunk.Count > 0)
                chunks.Add(string.Join("\n\n", currentChunk));

            return chunks;
        }

        // --- Internal methods ---

        internal static int EstimateTokens(string text) =>
            PromptGuard.EstimateTokens(text);

        internal List<PromptSection> IdentifySections(string prompt, int totalTokens)
        {
            var sections = new List<PromptSection>();

            try
            {
                var parts = Regex.Split(prompt, _config.SectionPattern, RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));
                var index = 0;

                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        index += part.Length;
                        continue;
                    }

                    var name = ExtractSectionName(trimmed);
                    var tokens = EstimateTokens(trimmed);

                    sections.Add(new PromptSection
                    {
                        Name = name,
                        Content = trimmed,
                        TokenCount = tokens,
                        PercentOfTotal = totalTokens > 0 ? Math.Round((double)tokens / totalTokens * 100, 1) : 0,
                        StartIndex = index,
                        EndIndex = index + part.Length
                    });

                    index += part.Length;
                }
            }
            catch (RegexParseException)
            {
                // If custom pattern is invalid, treat as one section
                sections.Add(new PromptSection
                {
                    Name = "Full Prompt",
                    Content = prompt,
                    TokenCount = totalTokens,
                    PercentOfTotal = 100,
                    StartIndex = 0,
                    EndIndex = prompt.Length
                });
            }

            return sections;
        }

        private static string ExtractSectionName(string text)
        {
            var headingMatch = Regex.Match(text, @"^#{1,3}\s+(.+?)(?:\n|$)", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            if (headingMatch.Success)
                return headingMatch.Groups[1].Value.Trim();

            // Use first line as name, truncated
            var firstLine = text.Split('\n')[0].Trim();
            return firstLine.Length > 40 ? firstLine[..37] + "..." : firstLine;
        }

        internal List<string> ExtractInstructions(string prompt)
        {
            var instructions = new List<string>();
            // Split by sentences and imperative-style lines
            var lines = prompt.Split('\n');
            var current = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (current.Length > 0)
                    {
                        instructions.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    continue;
                }

                // Detect instruction-like lines
                if (Regex.IsMatch(trimmed, @"^(?:[-*•]\s|[\d]+[.)]\s|You\s+(?:should|must|need|will)|Always\s|Never\s|Do\s+not|Don't|Ensure|Make\s+sure|Remember)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500)))
                {
                    if (current.Length > 0)
                    {
                        instructions.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    instructions.Add(trimmed);
                }
                else
                {
                    if (current.Length > 0) current.Append(' ');
                    current.Append(trimmed);
                }
            }

            if (current.Length > 0)
                instructions.Add(current.ToString().Trim());

            return instructions;
        }

        internal List<RedundancyPair> DetectRedundancies(List<string> instructions)
        {
            var pairs = new List<RedundancyPair>();

            for (int i = 0; i < instructions.Count; i++)
            {
                for (int j = i + 1; j < instructions.Count; j++)
                {
                    var sim = ComputeSimilarity(instructions[i], instructions[j]);
                    if (sim >= _config.RedundancyThreshold)
                    {
                        var shorter = instructions[i].Length <= instructions[j].Length
                            ? instructions[i] : instructions[j];
                        var longerTokens = EstimateTokens(
                            instructions[i].Length > instructions[j].Length
                                ? instructions[i] : instructions[j]);

                        pairs.Add(new RedundancyPair
                        {
                            InstructionA = instructions[i],
                            InstructionB = instructions[j],
                            Similarity = Math.Round(sim, 3),
                            SuggestedMerge = shorter,
                            EstimatedTokensSaved = longerTokens
                        });
                    }
                }
            }

            return pairs;
        }

        internal static double ComputeSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;

            var wordsA = Tokenize(a);
            var wordsB = Tokenize(b);

            if (wordsA.Count == 0 || wordsB.Count == 0) return 0;

            var setA = new HashSet<string>(wordsA);
            var setB = new HashSet<string>(wordsB);

            var intersection = setA.Intersect(setB).Count();
            var union = setA.Union(setB).Count();

            return union > 0 ? (double)intersection / union : 0;
        }

        private static List<string> Tokenize(string text)
        {
            return Regex.Matches(text.ToLowerInvariant(), @"\b\w+\b", RegexOptions.None, TimeSpan.FromMilliseconds(500))
                .Select(m => m.Value)
                .Where(w => w.Length > 1) // skip single chars
                .ToList();
        }

        internal List<OptimizationRecommendation> DetectFillerWords(string prompt)
        {
            var recs = new List<OptimizationRecommendation>();

            foreach (var (pattern, replacement, description) in FillerPatterns)
            {
                var matches = pattern.Matches(prompt);
                if (matches.Count > 0)
                {
                    var totalSaved = matches.Sum(m => EstimateTokens(m.Value) - EstimateTokens(replacement));
                    if (totalSaved > 0)
                    {
                        recs.Add(new OptimizationRecommendation
                        {
                            Category = OptimizationCategory.FillerWords,
                            Description = $"{description} ({matches.Count} occurrence{(matches.Count > 1 ? "s" : "")})",
                            OriginalText = matches[0].Value,
                            SuggestedText = replacement,
                            EstimatedTokensSaved = totalSaved,
                            Confidence = 0.9,
                            Severity = totalSaved > 10 ? OptimizationSeverity.Medium : OptimizationSeverity.Low
                        });
                    }
                }
            }

            return recs;
        }

        internal List<OptimizationRecommendation> DetectVerbosity(List<PromptSection> sections)
        {
            var recs = new List<OptimizationRecommendation>();

            foreach (var section in sections)
            {
                if (section.TokenCount < _config.VerbosityThreshold) continue;

                // Check for high word-to-instruction ratio
                var sentences = Regex.Split(section.Content, @"(?<=[.!?])\s+", RegexOptions.None, TimeSpan.FromMilliseconds(500));
                var longSentences = sentences.Where(s => EstimateTokens(s) > 30).ToList();

                if (longSentences.Count > 0)
                {
                    var savings = longSentences.Sum(s => EstimateTokens(s) / 4); // estimate 25% reduction
                    recs.Add(new OptimizationRecommendation
                    {
                        Category = OptimizationCategory.Verbosity,
                        Description = $"Section '{section.Name}' has {longSentences.Count} long sentence(s) that could be condensed",
                        OriginalText = longSentences[0],
                        EstimatedTokensSaved = savings,
                        Confidence = 0.6,
                        Severity = savings > 20 ? OptimizationSeverity.Medium : OptimizationSeverity.Low
                    });
                }
            }

            return recs;
        }

        internal List<OptimizationRecommendation> DetectFormattingOverhead(string prompt)
        {
            var recs = new List<OptimizationRecommendation>();
            var formatMentions = new List<string>();

            foreach (var pattern in FormattingPatterns)
            {
                var matches = pattern.Matches(prompt);
                foreach (Match m in matches)
                    formatMentions.Add(m.Value);
            }

            if (formatMentions.Count > 1)
            {
                recs.Add(new OptimizationRecommendation
                {
                    Category = OptimizationCategory.FormattingOverhead,
                    Description = $"Format instructions repeated {formatMentions.Count} times — consolidate into one directive",
                    OriginalText = string.Join("; ", formatMentions.Take(3)),
                    SuggestedText = formatMentions[0], // keep first
                    EstimatedTokensSaved = (formatMentions.Count - 1) * EstimateTokens(formatMentions[0]),
                    Confidence = 0.85,
                    Severity = OptimizationSeverity.Medium
                });
            }

            return recs;
        }

        internal List<OptimizationRecommendation> DetectExcessiveContext(string prompt)
        {
            var recs = new List<OptimizationRecommendation>();

            // Detect long example blocks
            var exampleMatches = Regex.Matches(prompt,
                @"(?:Example|e\.g\.|for\s+example|for\s+instance)[:\s](.+?)(?=\n\n|\z)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(500));

            var totalExampleTokens = 0;
            foreach (Match m in exampleMatches)
                totalExampleTokens += EstimateTokens(m.Value);

            var totalTokens = EstimateTokens(prompt);
            if (totalExampleTokens > totalTokens * 0.3 && totalExampleTokens > 30)
            {
                recs.Add(new OptimizationRecommendation
                {
                    Category = OptimizationCategory.ExcessiveContext,
                    Description = $"Examples consume {Math.Round((double)totalExampleTokens / totalTokens * 100)}% of prompt — consider reducing",
                    EstimatedTokensSaved = totalExampleTokens / 3, // suggest cutting a third
                    Confidence = 0.7,
                    Severity = totalExampleTokens > 100 ? OptimizationSeverity.High : OptimizationSeverity.Medium
                });
            }

            return recs;
        }

        internal List<OptimizationRecommendation> DetectOverSpecification(string prompt)
        {
            var recs = new List<OptimizationRecommendation>();

            // Detect very specific constraint lists
            var constraintPattern = new Regex(
                @"(?:must|should|always|never|do not|don't|ensure|make sure)(?:\s+\w+){5,}",
                RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));

            var constraints = constraintPattern.Matches(prompt);
            if (constraints.Count > 5)
            {
                recs.Add(new OptimizationRecommendation
                {
                    Category = OptimizationCategory.OverSpecification,
                    Description = $"{constraints.Count} detailed constraints detected — consider grouping related constraints",
                    EstimatedTokensSaved = constraints.Count * 3,
                    Confidence = 0.5,
                    Severity = constraints.Count > 10 ? OptimizationSeverity.Medium : OptimizationSeverity.Low
                });
            }

            return recs;
        }

        internal string ApplyOptimizations(string prompt, List<OptimizationRecommendation> recommendations)
        {
            var result = prompt;

            // Apply filler word replacements (high confidence, safe)
            foreach (var rec in recommendations
                .Where(r => r.Category == OptimizationCategory.FillerWords
                    && r.Confidence >= _config.AutoApplyMinConfidence
                    && !string.IsNullOrEmpty(r.OriginalText)))
            {
                result = result.Replace(rec.OriginalText, rec.SuggestedText);
            }

            // Normalize whitespace
            result = Regex.Replace(result, @"\n{3,}", "\n\n", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            result = Regex.Replace(result, @"[ \t]{2,}", " ", RegexOptions.None, TimeSpan.FromMilliseconds(500));

            return result.Trim();
        }

        private int CalculateOptimizationScore(int totalTokens, List<OptimizationRecommendation> recommendations)
        {
            if (totalTokens == 0) return 100;

            var totalPotentialSavings = recommendations.Sum(r => r.EstimatedTokensSaved);
            var wasteRatio = (double)totalPotentialSavings / totalTokens;

            // Score from 0 to 100; lower waste = higher score
            var score = (int)Math.Round(Math.Max(0, Math.Min(100, (1 - wasteRatio) * 100)));

            // Bonus for well-structured prompts (multiple sections)
            // Penalty for too many recommendations
            if (recommendations.Count > 10) score = Math.Max(0, score - 5);
            if (recommendations.Any(r => r.Severity == OptimizationSeverity.Critical))
                score = Math.Max(0, score - 10);

            return score;
        }
    }

    /// <summary>
    /// Result of comparing two prompts for token efficiency.
    /// </summary>
    public class TokenEfficiencyComparison
    {
        /// <summary>Gets the optimization report for prompt A.</summary>
        public OptimizationReport ReportA { get; internal set; } = new();

        /// <summary>Gets the optimization report for prompt B.</summary>
        public OptimizationReport ReportB { get; internal set; } = new();

        /// <summary>Gets the token difference (A - B). Positive means A uses more tokens.</summary>
        public int TokenDifference { get; internal set; }

        /// <summary>Gets which prompt is more efficient ("A" or "B").</summary>
        public string MoreEfficientPrompt { get; internal set; } = "";

        /// <summary>Gets the optimization score difference.</summary>
        public int ScoreDifference { get; internal set; }
    }

    /// <summary>
    /// Result of checking a prompt against a token budget.
    /// </summary>
    public class BudgetCheck
    {
        /// <summary>Gets the estimated prompt token count.</summary>
        public int PromptTokens { get; internal set; }

        /// <summary>Gets the total token limit.</summary>
        public int TokenLimit { get; internal set; }

        /// <summary>Gets the tokens reserved for response.</summary>
        public int ReserveTokens { get; internal set; }

        /// <summary>Gets the tokens available for the prompt.</summary>
        public int AvailableForPrompt { get; internal set; }

        /// <summary>Gets whether the prompt fits within budget.</summary>
        public bool WithinBudget { get; internal set; }

        /// <summary>Gets the utilization percentage.</summary>
        public double UtilizationPercent { get; internal set; }

        /// <summary>Gets the remaining tokens (negative if over budget).</summary>
        public int TokensRemaining { get; internal set; }
    }
}
