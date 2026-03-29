namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The primary intent category of a prompt.
    /// </summary>
    public enum PromptIntent
    {
        /// <summary>Asking for information or clarification.</summary>
        Question,
        /// <summary>Directing the model to perform a specific task.</summary>
        Instruction,
        /// <summary>Requesting creative content (stories, poems, ideas).</summary>
        Creative,
        /// <summary>Requesting analysis, comparison, or evaluation.</summary>
        Analytical,
        /// <summary>Casual dialogue or chitchat.</summary>
        Conversational,
        /// <summary>Asking for code, debugging, or technical implementation.</summary>
        Coding,
        /// <summary>Requesting a summary or condensation of content.</summary>
        Summarization,
        /// <summary>Requesting translation or language conversion.</summary>
        Translation,
        /// <summary>Asking for a list, enumeration, or ranking.</summary>
        Enumeration,
        /// <summary>Role-play, persona, or scenario simulation.</summary>
        RolePlay,
        /// <summary>Could not determine a clear intent.</summary>
        Unknown
    }

    /// <summary>
    /// Result of intent classification for a single prompt.
    /// </summary>
    public class IntentClassification
    {
        /// <summary>The primary detected intent.</summary>
        public PromptIntent PrimaryIntent { get; set; }

        /// <summary>Confidence score for the primary intent (0.0 – 1.0).</summary>
        public double Confidence { get; set; }

        /// <summary>All intents with their scores, sorted descending.</summary>
        public List<IntentScore> Scores { get; set; } = new List<IntentScore>();

        /// <summary>Matched signals that contributed to classification.</summary>
        public List<string> Signals { get; set; } = new List<string>();

        /// <summary>Whether multiple intents scored closely (within 0.15 of top).</summary>
        public bool IsAmbiguous { get; set; }

        public override string ToString()
        {
            var amb = IsAmbiguous ? " (ambiguous)" : "";
            return $"{PrimaryIntent} ({Confidence:P0}){amb}";
        }
    }

    /// <summary>
    /// A single intent with its score.
    /// </summary>
    public class IntentScore
    {
        public PromptIntent Intent { get; set; }
        public double Score { get; set; }

        public override string ToString() => $"{Intent}: {Score:F2}";
    }

    /// <summary>
    /// Classifies prompts by intent using keyword/pattern heuristics.
    /// Useful for routing prompts to specialised handlers, analytics,
    /// and understanding prompt distribution across intent categories.
    /// </summary>
    public class PromptIntentClassifier
    {
        private static readonly Dictionary<PromptIntent, (string[] keywords, string[] patterns)> IntentRules =
            new Dictionary<PromptIntent, (string[], string[])>
            {
                [PromptIntent.Question] = (
                    new[] { "what", "why", "how", "when", "where", "who", "which", "explain", "describe", "define", "meaning", "difference between" },
                    new[] { @"^(what|why|how|when|where|who|which)\b", @"\?\s*$", @"^(can|could|would|is|are|do|does|did|has|have|will|shall)\b.*\?" }
                ),
                [PromptIntent.Instruction] = (
                    new[] { "create", "make", "build", "generate", "write", "give me", "provide", "show me", "tell me", "help me", "set up", "configure", "implement", "design", "produce", "draft", "prepare", "compose" },
                    new[] { @"^(please\s+)?(create|make|build|generate|write|give|provide|show|tell|help|set|configure|implement|design|produce|draft|prepare|compose)\b", @"^(i need|i want)\b" }
                ),
                [PromptIntent.Creative] = (
                    new[] { "story", "poem", "song", "lyrics", "fiction", "imagine", "creative", "fantasy", "fairy tale", "narrative", "screenplay", "dialogue", "haiku", "limerick", "brainstorm", "idea" },
                    new[] { @"\b(write|create|compose)\s+(a\s+)?(story|poem|song|lyrics|script|screenplay|haiku|limerick|narrative)\b", @"\bonce upon a time\b" }
                ),
                [PromptIntent.Analytical] = (
                    new[] { "analyze", "analyse", "compare", "contrast", "evaluate", "assess", "pros and cons", "advantages", "disadvantages", "strengths", "weaknesses", "impact", "implications", "trade-offs", "critique", "review" },
                    new[] { @"\b(compare|contrast|analyze|analyse|evaluate|assess)\b.*\b(and|vs|versus|with)\b", @"\bpros\s+(and|&)\s+cons\b" }
                ),
                [PromptIntent.Conversational] = (
                    new[] { "hello", "hi", "hey", "thanks", "thank you", "goodbye", "bye", "how are you", "nice to meet", "good morning", "good evening", "sup", "yo", "cheers" },
                    new[] { @"^(hi|hello|hey|yo|sup|howdy|greetings)\b", @"^(thanks|thank you|cheers|bye|goodbye)\b" }
                ),
                [PromptIntent.Coding] = (
                    new[] { "code", "function", "class", "method", "api", "bug", "debug", "error", "exception", "syntax", "compile", "runtime", "algorithm", "regex", "sql", "query", "variable", "loop", "array", "refactor", "snippet", "script" },
                    new[] { @"\b(write|create|implement|fix|debug)\s+(a\s+)?(function|class|method|script|program|query|api)\b", @"```", @"\b(python|javascript|java|c#|csharp|typescript|rust|go|ruby|php|swift|kotlin)\b" }
                ),
                [PromptIntent.Summarization] = (
                    new[] { "summarize", "summarise", "summary", "tldr", "tl;dr", "brief", "condense", "shorten", "key points", "main points", "in short", "recap", "overview", "gist", "digest" },
                    new[] { @"\b(summarize|summarise|recap|condense)\b", @"\b(tl;?dr|key\s+points|main\s+points)\b" }
                ),
                [PromptIntent.Translation] = (
                    new[] { "translate", "translation", "convert to", "in english", "in spanish", "in french", "in german", "in japanese", "in chinese", "in korean", "in hindi", "in arabic", "localize" },
                    new[] { @"\btranslate\b.*\b(to|into|from)\b", @"\b(in|to)\s+(english|spanish|french|german|japanese|chinese|korean|hindi|arabic|portuguese|italian|russian|dutch)\b" }
                ),
                [PromptIntent.Enumeration] = (
                    new[] { "list", "enumerate", "name", "top 10", "top 5", "rank", "ranking", "best", "worst", "examples of", "types of", "kinds of", "categories of" },
                    new[] { @"\b(list|enumerate|name)\s+(all|the|some|top|\d+)\b", @"\btop\s+\d+\b", @"\b(give|provide|show)\s+(me\s+)?(a\s+)?list\b" }
                ),
                [PromptIntent.RolePlay] = (
                    new[] { "act as", "pretend", "role play", "roleplay", "you are a", "imagine you are", "simulate", "persona", "character", "impersonate", "behave as", "respond as" },
                    new[] { @"\b(act|behave|respond|speak)\s+(as|like)\b", @"\byou are (a|an|the)\b", @"\b(pretend|imagine)\s+(you|that you)\b" }
                )
            };

        /// <summary>
        /// Classify the intent of a single prompt.
        /// </summary>
        public IntentClassification Classify(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new IntentClassification
                {
                    PrimaryIntent = PromptIntent.Unknown,
                    Confidence = 0,
                    Scores = new List<IntentScore>
                    {
                        new IntentScore { Intent = PromptIntent.Unknown, Score = 0 }
                    }
                };
            }

            var lower = prompt.Trim().ToLowerInvariant();
            var scores = new Dictionary<PromptIntent, double>();
            var signals = new List<string>();

            foreach (var rule in IntentRules)
            {
                double score = 0;

                // Keyword matches
                foreach (var kw in rule.Value.keywords)
                {
                    if (lower.Contains(kw))
                    {
                        score += 1.0;
                        signals.Add($"[{rule.Key}] keyword: \"{kw}\"");
                    }
                }

                // Pattern matches (weighted higher)
                foreach (var pat in rule.Value.patterns)
                {
                    if (Regex.IsMatch(lower, pat, RegexOptions.IgnoreCase))
                    {
                        score += 2.0;
                        signals.Add($"[{rule.Key}] pattern: /{pat}/");
                    }
                }

                scores[rule.Key] = score;
            }

            // Normalise to 0–1
            var maxScore = scores.Values.Max();
            var intentScores = scores
                .Select(kv => new IntentScore
                {
                    Intent = kv.Key,
                    Score = maxScore > 0 ? kv.Value / maxScore : 0
                })
                .OrderByDescending(s => s.Score)
                .ToList();

            var top = intentScores.First();
            var isAmbiguous = intentScores.Count > 1
                && intentScores[1].Score >= top.Score - 0.15;

            return new IntentClassification
            {
                PrimaryIntent = top.Score > 0 ? top.Intent : PromptIntent.Unknown,
                Confidence = top.Score,
                Scores = intentScores,
                Signals = signals,
                IsAmbiguous = isAmbiguous
            };
        }

        /// <summary>
        /// Classify multiple prompts and return a distribution summary.
        /// </summary>
        public IntentDistribution ClassifyBatch(IEnumerable<string> prompts)
        {
            var results = prompts.Select(Classify).ToList();
            var distribution = results
                .GroupBy(r => r.PrimaryIntent)
                .ToDictionary(g => g.Key, g => g.Count());

            return new IntentDistribution
            {
                Total = results.Count,
                Distribution = distribution,
                AmbiguousCount = results.Count(r => r.IsAmbiguous),
                AverageConfidence = results.Count > 0 ? results.Average(r => r.Confidence) : 0,
                Results = results
            };
        }
    }

    /// <summary>
    /// Summary of intent distribution across a batch of prompts.
    /// </summary>
    public class IntentDistribution
    {
        /// <summary>Total prompts classified.</summary>
        public int Total { get; set; }

        /// <summary>Count per intent category.</summary>
        public Dictionary<PromptIntent, int> Distribution { get; set; } = new Dictionary<PromptIntent, int>();

        /// <summary>Number of prompts with ambiguous classification.</summary>
        public int AmbiguousCount { get; set; }

        /// <summary>Average confidence across all classifications.</summary>
        public double AverageConfidence { get; set; }

        /// <summary>Individual classification results.</summary>
        public List<IntentClassification> Results { get; set; } = new List<IntentClassification>();

        /// <summary>
        /// Get the most common intent in the batch.
        /// </summary>
        public PromptIntent? DominantIntent =>
            Distribution.Count > 0
                ? Distribution.OrderByDescending(kv => kv.Value).First().Key
                : (PromptIntent?)null;

        public override string ToString()
        {
            var lines = Distribution
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"  {kv.Key}: {kv.Value} ({(double)kv.Value / Total:P0})");
            return $"Intent Distribution ({Total} prompts, {AmbiguousCount} ambiguous):\n{string.Join("\n", lines)}";
        }
    }
}
