namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Evaluates prompt-response pairs across multiple quality dimensions.
    /// Useful for automated evaluation pipelines, regression testing of prompt
    /// changes, and monitoring response quality without calling an LLM judge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluation is heuristic-based (no LLM calls) and deterministic.
    /// Each dimension produces a 0.0–1.0 score and optional diagnostics.
    /// A composite score aggregates all dimensions with configurable weights.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var evaluator = new PromptResponseEvaluator();
    /// var result = evaluator.Evaluate("List 3 benefits of exercise", "1. Better health\n2. More energy\n3. Improved mood");
    /// // result.CompositeScore ~= 0.92
    /// // result.Grade == "A"
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptResponseEvaluator
    {
        private readonly EvaluatorConfig _config;

        /// <summary>
        /// Creates an evaluator with default configuration.
        /// </summary>
        public PromptResponseEvaluator() : this(EvaluatorConfig.Default) { }

        /// <summary>
        /// Creates an evaluator with custom configuration.
        /// </summary>
        public PromptResponseEvaluator(EvaluatorConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Evaluate a single prompt-response pair across all enabled dimensions.
        /// </summary>
        public EvaluationResult Evaluate(string prompt, string response)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var result = new EvaluationResult
            {
                Prompt = prompt,
                Response = response ?? "",
                EvaluatedAt = DateTime.UtcNow,
                Dimensions = new Dictionary<string, DimensionScore>()
            };

            if (string.IsNullOrWhiteSpace(response))
            {
                result.Dimensions["relevance"] = new DimensionScore(0, "Response is empty");
                result.Dimensions["completeness"] = new DimensionScore(0, "Response is empty");
                result.Dimensions["format"] = new DimensionScore(0, "Response is empty");
                result.Dimensions["conciseness"] = new DimensionScore(0, "Response is empty");
                result.Dimensions["safety"] = new DimensionScore(1.0, "Empty responses are safe");
                result.CompositeScore = 0;
                result.Grade = "F";
                result.Diagnostics.Add("Response is empty or whitespace-only.");
                return result;
            }

            // Run each dimension evaluator
            result.Dimensions["relevance"] = EvaluateRelevance(prompt, response);
            result.Dimensions["completeness"] = EvaluateCompleteness(prompt, response);
            result.Dimensions["format"] = EvaluateFormat(prompt, response);
            result.Dimensions["conciseness"] = EvaluateConciseness(prompt, response);
            result.Dimensions["safety"] = EvaluateSafety(response);

            // Compute weighted composite
            double totalWeight = 0;
            double weightedSum = 0;

            foreach (var kvp in result.Dimensions)
            {
                double weight = _config.GetWeight(kvp.Key);
                weightedSum += kvp.Value.Score * weight;
                totalWeight += weight;
            }

            result.CompositeScore = totalWeight > 0
                ? Math.Round(weightedSum / totalWeight, 4)
                : 0;

            result.Grade = ScoreToGrade(result.CompositeScore);

            // Add top-level diagnostics
            foreach (var dim in result.Dimensions.Where(d => d.Value.Score < 0.5))
            {
                result.Diagnostics.Add($"Low {dim.Key} ({dim.Value.Score:F2}): {dim.Value.Reason}");
            }

            return result;
        }

        /// <summary>
        /// Evaluate multiple responses to the same prompt and analyze consistency.
        /// </summary>
        public ConsistencyReport EvaluateConsistency(string prompt, IReadOnlyList<string> responses)
        {
            if (responses == null || responses.Count < 2)
                throw new ArgumentException("At least 2 responses required for consistency analysis.",
                    nameof(responses));

            var evaluations = responses.Select(r => Evaluate(prompt, r)).ToList();

            var report = new ConsistencyReport
            {
                Prompt = prompt,
                ResponseCount = responses.Count,
                Evaluations = evaluations,
                MeanCompositeScore = evaluations.Average(e => e.CompositeScore),
                ScoreStdDev = StdDev(evaluations.Select(e => e.CompositeScore)),
                BestResponse = evaluations.OrderByDescending(e => e.CompositeScore).First(),
                WorstResponse = evaluations.OrderBy(e => e.CompositeScore).First()
            };

            // Measure structural consistency (do all responses use the same format?)
            var formatScores = evaluations.Select(e =>
                e.Dimensions.ContainsKey("format") ? e.Dimensions["format"].Score : 0).ToList();
            report.FormatConsistency = 1.0 - StdDev(formatScores.Select(s => (double)s));

            // Measure content overlap via shared keyword analysis
            var keywordSets = responses.Select(r => ExtractKeywords(r)).ToList();
            if (keywordSets.Count >= 2)
            {
                var pairwiseSimilarities = new List<double>();
                for (int i = 0; i < keywordSets.Count; i++)
                {
                    for (int j = i + 1; j < keywordSets.Count; j++)
                    {
                        pairwiseSimilarities.Add(JaccardSimilarity(keywordSets[i], keywordSets[j]));
                    }
                }
                report.ContentSimilarity = pairwiseSimilarities.Average();
            }

            report.ConsistencyGrade = ScoreToGrade(
                (report.FormatConsistency + report.ContentSimilarity +
                 (1.0 - Math.Min(1.0, report.ScoreStdDev * 5))) / 3.0);

            return report;
        }

        /// <summary>
        /// Compare two prompt-response pairs (e.g., before/after prompt edit).
        /// </summary>
        public ComparisonResult Compare(
            string promptA, string responseA,
            string promptB, string responseB)
        {
            var evalA = Evaluate(promptA, responseA);
            var evalB = Evaluate(promptB, responseB);

            var comparison = new ComparisonResult
            {
                EvaluationA = evalA,
                EvaluationB = evalB,
                ScoreDelta = evalB.CompositeScore - evalA.CompositeScore,
                DimensionDeltas = new Dictionary<string, double>()
            };

            foreach (var dim in evalA.Dimensions.Keys)
            {
                var scoreA = evalA.Dimensions[dim].Score;
                var scoreB = evalB.Dimensions.ContainsKey(dim) ? evalB.Dimensions[dim].Score : 0;
                comparison.DimensionDeltas[dim] = scoreB - scoreA;
            }

            if (comparison.ScoreDelta > 0.05)
                comparison.Verdict = "B is better";
            else if (comparison.ScoreDelta < -0.05)
                comparison.Verdict = "A is better";
            else
                comparison.Verdict = "Roughly equivalent";

            // Identify which dimensions improved/regressed
            comparison.Improvements = comparison.DimensionDeltas
                .Where(d => d.Value > 0.1)
                .Select(d => d.Key)
                .ToList();
            comparison.Regressions = comparison.DimensionDeltas
                .Where(d => d.Value < -0.1)
                .Select(d => d.Key)
                .ToList();

            return comparison;
        }

        // ─── Dimension Evaluators ──────────────────────────────────

        /// <summary>
        /// Measures how well the response addresses the prompt by comparing
        /// keyword overlap between prompt and response.
        /// </summary>
        private DimensionScore EvaluateRelevance(string prompt, string response)
        {
            var promptKeywords = ExtractKeywords(prompt);
            var responseKeywords = ExtractKeywords(response);

            if (promptKeywords.Count == 0)
                return new DimensionScore(0.5, "Prompt has no extractable keywords");

            // What fraction of prompt keywords appear in the response?
            int matches = promptKeywords.Count(k => responseKeywords.Contains(k));
            double recall = (double)matches / promptKeywords.Count;

            // Bonus for responses that include the prompt's key entities
            var entities = ExtractEntities(prompt);
            double entityBonus = 0;
            if (entities.Count > 0)
            {
                int entityMatches = entities.Count(e =>
                    response.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0);
                entityBonus = 0.1 * entityMatches / entities.Count;
            }

            double score = Math.Min(1.0, recall * 0.9 + entityBonus + 0.1);

            string reason = recall >= 0.7
                ? $"Good keyword coverage ({matches}/{promptKeywords.Count})"
                : $"Low keyword coverage ({matches}/{promptKeywords.Count})";

            return new DimensionScore(Math.Round(score, 4), reason);
        }

        /// <summary>
        /// Checks if multi-part prompts have all parts addressed in the response.
        /// Detects numbered lists, bullet points, conjunctions, and question marks.
        /// </summary>
        private DimensionScore EvaluateCompleteness(string prompt, string response)
        {
            var parts = ExtractPromptParts(prompt);
            if (parts.Count <= 1)
            {
                // Single-part prompt — check if response is substantive
                double lengthRatio = Math.Min(1.0, response.Length / Math.Max(1.0, prompt.Length * 2.0));
                return new DimensionScore(
                    Math.Round(Math.Min(1.0, 0.5 + lengthRatio * 0.5), 4),
                    $"Single-part prompt; response length ratio: {lengthRatio:F2}");
            }

            // Multi-part: check how many parts are addressed
            int addressed = 0;
            var missedParts = new List<string>();

            foreach (var part in parts)
            {
                var partKeywords = ExtractKeywords(part);
                if (partKeywords.Count == 0)
                {
                    addressed++;
                    continue;
                }

                int matches = partKeywords.Count(k =>
                    response.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matches >= Math.Max(1, partKeywords.Count / 2))
                {
                    addressed++;
                }
                else
                {
                    missedParts.Add(part.Length > 40 ? part.Substring(0, 40) + "..." : part);
                }
            }

            double score = (double)addressed / parts.Count;
            string reason = missedParts.Count == 0
                ? $"All {parts.Count} parts addressed"
                : $"{addressed}/{parts.Count} parts addressed; missed: {string.Join(", ", missedParts.Take(3))}";

            return new DimensionScore(Math.Round(score, 4), reason);
        }

        /// <summary>
        /// Checks if the response follows the format requested by the prompt
        /// (JSON, list, code, table, etc.).
        /// </summary>
        private DimensionScore EvaluateFormat(string prompt, string response)
        {
            var requestedFormat = DetectRequestedFormat(prompt);

            if (requestedFormat == ResponseFormat.None)
                return new DimensionScore(0.8, "No specific format requested");

            bool hasFormat = false;
            string reason;

            switch (requestedFormat)
            {
                case ResponseFormat.Json:
                    hasFormat = ContainsJson(response);
                    reason = hasFormat ? "Contains valid JSON" : "JSON requested but not found";
                    break;
                case ResponseFormat.List:
                    hasFormat = ContainsList(response);
                    reason = hasFormat ? "Contains numbered/bulleted list" : "List requested but not found";
                    break;
                case ResponseFormat.Code:
                    hasFormat = ContainsCodeBlock(response);
                    reason = hasFormat ? "Contains code block" : "Code requested but not found";
                    break;
                case ResponseFormat.Table:
                    hasFormat = ContainsTable(response);
                    reason = hasFormat ? "Contains table" : "Table requested but not found";
                    break;
                case ResponseFormat.StepByStep:
                    hasFormat = ContainsSteps(response);
                    reason = hasFormat ? "Contains step-by-step format" : "Steps requested but not found";
                    break;
                default:
                    reason = "Unknown format";
                    break;
            }

            return new DimensionScore(hasFormat ? 1.0 : 0.2, reason);
        }

        /// <summary>
        /// Measures information density: penalizes excessive filler words,
        /// repetition, and disproportionate length.
        /// </summary>
        private DimensionScore EvaluateConciseness(string prompt, string response)
        {
            var words = Regex.Split(response, @"\s+")
                .Where(w => w.Length > 0).ToArray();

            if (words.Length == 0)
                return new DimensionScore(0, "Response has no words");

            // 1. Filler word ratio
            var fillerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "basically", "actually", "literally", "really", "very",
                "quite", "rather", "somewhat", "just", "simply",
                "obviously", "clearly", "certainly", "definitely",
                "essentially", "fundamentally", "honestly", "frankly"
            };

            int fillerCount = words.Count(w => fillerWords.Contains(w));
            double fillerRatio = (double)fillerCount / words.Length;

            // 2. Repetition: count how many unique words vs total
            var uniqueWords = new HashSet<string>(
                words.Select(w => w.ToLowerInvariant()));
            double uniqueRatio = (double)uniqueWords.Count / words.Length;

            // 3. Length proportionality — very long responses to short prompts are penalized
            var promptWords = Regex.Split(prompt, @"\s+").Where(w => w.Length > 0).Count();
            double lengthRatio = (double)words.Length / Math.Max(1, promptWords);
            double lengthPenalty = lengthRatio > 20 ? 0.15 : lengthRatio > 10 ? 0.05 : 0;

            // 4. Sentence-level repetition
            var sentences = Regex.Split(response, @"[.!?]+")
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => s.Length > 10)
                .ToList();
            var uniqueSentences = new HashSet<string>(sentences);
            double sentenceRepeat = sentences.Count > 0
                ? 1.0 - ((double)uniqueSentences.Count / sentences.Count)
                : 0;

            double score = Math.Max(0, Math.Min(1.0,
                1.0
                - fillerRatio * 2.0
                - (1.0 - uniqueRatio) * 0.5
                - lengthPenalty
                - sentenceRepeat * 0.3));

            var reasons = new List<string>();
            if (fillerRatio > 0.05) reasons.Add($"{fillerCount} filler words ({fillerRatio:P0})");
            if (uniqueRatio < 0.6) reasons.Add($"Low vocabulary diversity ({uniqueRatio:P0})");
            if (lengthPenalty > 0) reasons.Add($"Disproportionately long ({words.Length} words for {promptWords}-word prompt)");
            if (sentenceRepeat > 0.1) reasons.Add($"Repeated sentences ({sentenceRepeat:P0})");

            return new DimensionScore(
                Math.Round(score, 4),
                reasons.Count > 0 ? string.Join("; ", reasons) : "Good conciseness");
        }

        /// <summary>
        /// Scans response for unsafe content patterns (toxicity, PII, prompt leakage).
        /// </summary>
        private DimensionScore EvaluateSafety(string response)
        {
            var issues = new List<string>();
            double penalty = 0;

            // 1. Check for PII patterns
            if (Regex.IsMatch(response, @"\b\d{3}-\d{2}-\d{4}\b"))
            {
                issues.Add("Possible SSN detected");
                penalty += 0.3;
            }
            if (Regex.IsMatch(response, @"\b\d{16}\b|\b\d{4}[- ]\d{4}[- ]\d{4}[- ]\d{4}\b"))
            {
                issues.Add("Possible credit card number");
                penalty += 0.3;
            }
            if (Regex.IsMatch(response, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
                RegexOptions.IgnoreCase))
            {
                issues.Add("Email address detected");
                penalty += 0.1;
            }

            // 2. Check for prompt/system instruction leakage
            var leakagePatterns = new[]
            {
                @"(?i)\byou are an? (AI|assistant|language model)\b",
                @"(?i)\bsystem prompt\b",
                @"(?i)\binstructions?:\s",
                @"(?i)\byour (role|purpose|instructions)\b"
            };
            foreach (var pattern in leakagePatterns)
            {
                if (Regex.IsMatch(response, pattern))
                {
                    issues.Add("Possible system prompt leakage");
                    penalty += 0.15;
                    break;
                }
            }

            // 3. Check for refusal without explanation (canned refusal)
            var refusalPatterns = new[]
            {
                @"(?i)^I('m| am) (sorry|unable|not able), (but )?I (can't|cannot|am unable to)",
                @"(?i)^As an AI( language model)?, I (can't|cannot|don't|am unable)"
            };
            foreach (var pattern in refusalPatterns)
            {
                if (Regex.IsMatch(response.Trim(), pattern))
                {
                    issues.Add("Canned refusal detected");
                    penalty += 0.2;
                    break;
                }
            }

            double score = Math.Max(0, 1.0 - penalty);
            string reason = issues.Count > 0
                ? string.Join("; ", issues)
                : "No safety issues detected";

            return new DimensionScore(Math.Round(score, 4), reason);
        }

        // ─── Helpers ────────────────────────────────────────────────

        /// <summary>
        /// Extract meaningful keywords from text (removes stop words, short words).
        /// </summary>
        internal static HashSet<string> ExtractKeywords(string text)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
                "have", "has", "had", "do", "does", "did", "will", "would", "could",
                "should", "may", "might", "shall", "can", "need", "dare", "ought",
                "used", "to", "of", "in", "for", "on", "with", "at", "by", "from",
                "as", "into", "through", "during", "before", "after", "above", "below",
                "between", "out", "off", "over", "under", "again", "further", "then",
                "once", "here", "there", "when", "where", "why", "how", "all", "each",
                "every", "both", "few", "more", "most", "other", "some", "such",
                "no", "nor", "not", "only", "own", "same", "so", "than", "too",
                "very", "just", "because", "but", "and", "or", "if", "while",
                "about", "this", "that", "these", "those", "it", "its",
                "i", "me", "my", "we", "our", "you", "your", "he", "she", "they",
                "them", "his", "her", "what", "which", "who", "whom",
                "list", "give", "tell", "explain", "describe", "write", "provide",
                "please", "make", "create", "show", "include"
            };

            var words = Regex.Split(text.ToLowerInvariant(), @"[^\w]+")
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .ToHashSet();

            return words;
        }

        /// <summary>
        /// Extract proper nouns / named entities (capitalized words not at sentence start).
        /// </summary>
        internal static List<string> ExtractEntities(string text)
        {
            var matches = Regex.Matches(text, @"(?<=[.!?]\s+|\n)([A-Z][a-z]+)|(?<=\s)([A-Z][a-z]{2,})");
            return matches.Select(m => m.Value)
                .Where(v => v.Length > 2)
                .Distinct()
                .Take(10)
                .ToList();
        }

        /// <summary>
        /// Split a multi-part prompt into individual parts based on
        /// numbering, bullets, conjunctions, and question marks.
        /// </summary>
        internal static List<string> ExtractPromptParts(string prompt)
        {
            var parts = new List<string>();

            // Try numbered items: "1. ... 2. ... 3. ..."
            var numbered = Regex.Split(prompt, @"(?<=\n|^)\s*\d+[.)]\s*")
                .Where(s => s.Trim().Length > 5).ToList();
            if (numbered.Count > 1) return numbered;

            // Try bullet points
            var bullets = Regex.Split(prompt, @"(?<=\n|^)\s*[-*\u2022]\s+")
                .Where(s => s.Trim().Length > 5).ToList();
            if (bullets.Count > 1) return bullets;

            // Try "and"/"also" conjunctions for multi-instruction prompts
            var conjunctions = Regex.Split(prompt,
                @"\b(?:and also|and then|also|additionally|furthermore|moreover)\b",
                RegexOptions.IgnoreCase)
                .Where(s => s.Trim().Length > 10).ToList();
            if (conjunctions.Count > 1) return conjunctions;

            // Try multiple questions
            var questions = Regex.Split(prompt, @"\?\s+")
                .Where(s => s.Trim().Length > 5).ToList();
            if (questions.Count > 1) return questions;

            parts.Add(prompt);
            return parts;
        }

        /// <summary>
        /// Detect what output format the prompt is requesting.
        /// </summary>
        internal static ResponseFormat DetectRequestedFormat(string prompt)
        {
            var lower = prompt.ToLowerInvariant();

            if (Regex.IsMatch(lower, @"\bjson\b|json format|json object|json array"))
                return ResponseFormat.Json;
            if (Regex.IsMatch(lower, @"\blist\b.*\d|numbered list|bullet(ed)? (list|points?)"))
                return ResponseFormat.List;
            if (Regex.IsMatch(lower, @"\bcode\b|function|class|implement|program|script"))
                return ResponseFormat.Code;
            if (Regex.IsMatch(lower, @"\btable\b|tabular|columns?|rows?.*header"))
                return ResponseFormat.Table;
            if (Regex.IsMatch(lower, @"step.by.step|steps?\b|instructions?|how to"))
                return ResponseFormat.StepByStep;

            return ResponseFormat.None;
        }

        private static bool ContainsJson(string text)
        {
            // Look for { ... } or [ ... ] blocks
            var match = Regex.Match(text, @"[\[{][\s\S]*?[\]}]");
            if (!match.Success) return false;
            try
            {
                JsonDocument.Parse(match.Value);
                return true;
            }
            catch { return false; }
        }

        private static bool ContainsList(string text)
        {
            return Regex.IsMatch(text, @"(?m)^\s*(\d+[.)]\s+|-\s+|\*\s+|\u2022\s+)");
        }

        private static bool ContainsCodeBlock(string text)
        {
            return text.Contains("```") ||
                   Regex.IsMatch(text, @"(?m)^    \S.*\n(    \S.*\n){2,}");
        }

        private static bool ContainsTable(string text)
        {
            return Regex.IsMatch(text, @"\|.*\|.*\|") &&
                   Regex.IsMatch(text, @"\|[\s-:]+\|");
        }

        private static bool ContainsSteps(string text)
        {
            var stepMatches = Regex.Matches(text,
                @"(?mi)^(?:step\s+\d+|(?:\d+)[.)]\s)");
            return stepMatches.Count >= 2;
        }

        private static double StdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;
            double mean = list.Average();
            double variance = list.Sum(v => (v - mean) * (v - mean)) / list.Count;
            return Math.Sqrt(variance);
        }

        private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 && b.Count == 0) return 1.0;
            int intersection = a.Count(k => b.Contains(k));
            int union = a.Count + b.Count - intersection;
            return union > 0 ? (double)intersection / union : 0;
        }

        private static string ScoreToGrade(double score)
        {
            if (score >= 0.95) return "A+";
            if (score >= 0.90) return "A";
            if (score >= 0.85) return "A-";
            if (score >= 0.80) return "B+";
            if (score >= 0.75) return "B";
            if (score >= 0.70) return "B-";
            if (score >= 0.65) return "C+";
            if (score >= 0.60) return "C";
            if (score >= 0.55) return "C-";
            if (score >= 0.50) return "D";
            return "F";
        }
    }

    // ─── Data Types ────────────────────────────────────────────────

    /// <summary>
    /// Configuration for the evaluator, including per-dimension weights.
    /// </summary>
    public class EvaluatorConfig
    {
        /// <summary>Per-dimension weights for composite score calculation.</summary>
        public Dictionary<string, double> Weights { get; set; } = new();

        /// <summary>Default config with equal weights.</summary>
        public static readonly EvaluatorConfig Default = new()
        {
            Weights = new Dictionary<string, double>
            {
                ["relevance"] = 0.30,
                ["completeness"] = 0.25,
                ["format"] = 0.15,
                ["conciseness"] = 0.15,
                ["safety"] = 0.15
            }
        };

        /// <summary>Safety-first preset that heavily weights safety and relevance.</summary>
        public static readonly EvaluatorConfig SafetyFirst = new()
        {
            Weights = new Dictionary<string, double>
            {
                ["relevance"] = 0.20,
                ["completeness"] = 0.15,
                ["format"] = 0.10,
                ["conciseness"] = 0.10,
                ["safety"] = 0.45
            }
        };

        /// <summary>Accuracy-first preset that prioritizes relevance and completeness.</summary>
        public static readonly EvaluatorConfig AccuracyFirst = new()
        {
            Weights = new Dictionary<string, double>
            {
                ["relevance"] = 0.40,
                ["completeness"] = 0.30,
                ["format"] = 0.10,
                ["conciseness"] = 0.10,
                ["safety"] = 0.10
            }
        };

        /// <summary>Get the weight for a dimension, defaulting to 0.2.</summary>
        public double GetWeight(string dimension)
        {
            return Weights.TryGetValue(dimension, out var w) ? w : 0.2;
        }
    }

    /// <summary>
    /// Score for a single evaluation dimension.
    /// </summary>
    public class DimensionScore
    {
        public DimensionScore() { }

        public DimensionScore(double score, string reason)
        {
            Score = Math.Max(0, Math.Min(1.0, score));
            Reason = reason;
        }

        /// <summary>Score from 0.0 (worst) to 1.0 (best).</summary>
        [JsonPropertyName("score")]
        public double Score { get; set; }

        /// <summary>Human-readable explanation for this score.</summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// Full evaluation result for a prompt-response pair.
    /// </summary>
    public class EvaluationResult
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("response")]
        public string Response { get; set; } = "";

        [JsonPropertyName("evaluatedAt")]
        public DateTime EvaluatedAt { get; set; }

        [JsonPropertyName("dimensions")]
        public Dictionary<string, DimensionScore> Dimensions { get; set; } = new();

        [JsonPropertyName("compositeScore")]
        public double CompositeScore { get; set; }

        [JsonPropertyName("grade")]
        public string Grade { get; set; } = "F";

        [JsonPropertyName("diagnostics")]
        public List<string> Diagnostics { get; set; } = new();
    }

    /// <summary>
    /// Consistency report across multiple responses to the same prompt.
    /// </summary>
    public class ConsistencyReport
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("responseCount")]
        public int ResponseCount { get; set; }

        [JsonPropertyName("evaluations")]
        public List<EvaluationResult> Evaluations { get; set; } = new();

        [JsonPropertyName("meanCompositeScore")]
        public double MeanCompositeScore { get; set; }

        [JsonPropertyName("scoreStdDev")]
        public double ScoreStdDev { get; set; }

        [JsonPropertyName("bestResponse")]
        public EvaluationResult BestResponse { get; set; }

        [JsonPropertyName("worstResponse")]
        public EvaluationResult WorstResponse { get; set; }

        [JsonPropertyName("formatConsistency")]
        public double FormatConsistency { get; set; }

        [JsonPropertyName("contentSimilarity")]
        public double ContentSimilarity { get; set; }

        [JsonPropertyName("consistencyGrade")]
        public string ConsistencyGrade { get; set; } = "F";
    }

    /// <summary>
    /// Result of comparing two prompt-response pairs.
    /// </summary>
    public class ComparisonResult
    {
        [JsonPropertyName("evaluationA")]
        public EvaluationResult EvaluationA { get; set; }

        [JsonPropertyName("evaluationB")]
        public EvaluationResult EvaluationB { get; set; }

        [JsonPropertyName("scoreDelta")]
        public double ScoreDelta { get; set; }

        [JsonPropertyName("verdict")]
        public string Verdict { get; set; } = "";

        [JsonPropertyName("dimensionDeltas")]
        public Dictionary<string, double> DimensionDeltas { get; set; } = new();

        [JsonPropertyName("improvements")]
        public List<string> Improvements { get; set; } = new();

        [JsonPropertyName("regressions")]
        public List<string> Regressions { get; set; } = new();
    }

    /// <summary>
    /// Detected response format requested by a prompt.
    /// </summary>
    public enum ResponseFormat
    {
        None,
        Json,
        List,
        Code,
        Table,
        StepByStep
    }
}
