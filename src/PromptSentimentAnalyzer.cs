namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Detected tone category for a prompt.
    /// </summary>
    public enum SentimentTone
    {
        /// <summary>Neutral, balanced phrasing.</summary>
        Neutral,
        /// <summary>Polite, courteous language (please, thank you, would you).</summary>
        Polite,
        /// <summary>Direct, commanding language (do this, you must).</summary>
        Assertive,
        /// <summary>Formal, professional register.</summary>
        Formal,
        /// <summary>Casual, conversational register.</summary>
        Casual,
        /// <summary>Urgent or demanding language.</summary>
        Urgent,
        /// <summary>Encouraging, supportive language.</summary>
        Encouraging
    }

    /// <summary>
    /// Sentiment polarity of the prompt.
    /// </summary>
    public enum SentimentPolarity
    {
        /// <summary>Positive sentiment.</summary>
        Positive,
        /// <summary>Neutral sentiment.</summary>
        Neutral,
        /// <summary>Negative sentiment.</summary>
        Negative
    }

    /// <summary>
    /// Individual tone signal detected in text.
    /// </summary>
    public class ToneSignal
    {
        /// <summary>Gets the tone category.</summary>
        public SentimentTone Tone { get; internal set; }

        /// <summary>Gets the matched phrase or pattern.</summary>
        public string Match { get; internal set; } = "";

        /// <summary>Gets the signal strength (0.0 - 1.0).</summary>
        public double Strength { get; internal set; }
    }

    /// <summary>
    /// Tone shift suggestion to adjust prompt register.
    /// </summary>
    public class ToneShiftSuggestion
    {
        /// <summary>Gets the original phrase.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>Gets the suggested replacement.</summary>
        public string Replacement { get; internal set; } = "";

        /// <summary>Gets the target tone of the replacement.</summary>
        public SentimentTone TargetTone { get; internal set; }

        /// <summary>Gets an explanation of the shift.</summary>
        public string Reason { get; internal set; } = "";
    }

    /// <summary>
    /// Complete sentiment analysis result for a prompt.
    /// </summary>
    public class SentimentReport
    {
        /// <summary>Gets the analyzed text.</summary>
        public string Text { get; internal set; } = "";

        /// <summary>Gets the dominant tone.</summary>
        public SentimentTone DominantTone { get; internal set; }

        /// <summary>Gets the sentiment polarity.</summary>
        public SentimentPolarity Polarity { get; internal set; }

        /// <summary>Gets the confidence score for the dominant tone (0.0 - 1.0).</summary>
        public double Confidence { get; internal set; }

        /// <summary>Gets all detected tone signals.</summary>
        public List<ToneSignal> Signals { get; internal set; } = new();

        /// <summary>Gets the tone distribution as percentages.</summary>
        public Dictionary<SentimentTone, double> ToneDistribution { get; internal set; } = new();

        /// <summary>Gets word count.</summary>
        public int WordCount { get; internal set; }

        /// <summary>Gets sentence count.</summary>
        public int SentenceCount { get; internal set; }

        /// <summary>Gets question count.</summary>
        public int QuestionCount { get; internal set; }

        /// <summary>Gets exclamation count.</summary>
        public int ExclamationCount { get; internal set; }

        /// <summary>Gets imperative sentence ratio (0.0 - 1.0).</summary>
        public double ImperativeRatio { get; internal set; }

        /// <summary>Gets suggestions to shift tone toward a target.</summary>
        public List<ToneShiftSuggestion> Suggestions { get; internal set; } = new();

        /// <summary>
        /// Returns a formatted summary.
        /// </summary>
        public string ToSummary()
        {
            var lines = new List<string>
            {
                $"Sentiment Analysis Report",
                $"========================",
                $"Dominant Tone : {DominantTone}",
                $"Polarity      : {Polarity}",
                $"Confidence    : {Confidence:P0}",
                $"Words         : {WordCount}",
                $"Sentences     : {SentenceCount}",
                $"Questions     : {QuestionCount}",
                $"Exclamations  : {ExclamationCount}",
                $"Imperative %  : {ImperativeRatio:P0}",
                "",
                "Tone Distribution:"
            };
            foreach (var kv in ToneDistribution.OrderByDescending(x => x.Value))
            {
                int barLen = (int)(kv.Value * 30);
                string bar = new string('█', barLen) + new string('░', 30 - barLen);
                lines.Add($"  {kv.Key,-14} [{bar}] {kv.Value:P0}");
            }
            if (Signals.Count > 0)
            {
                lines.Add("");
                lines.Add($"Signals ({Signals.Count}):");
                foreach (var s in Signals.Take(10))
                    lines.Add($"  [{s.Tone}] \"{s.Match}\" (strength: {s.Strength:F2})");
                if (Signals.Count > 10)
                    lines.Add($"  ... and {Signals.Count - 10} more");
            }
            if (Suggestions.Count > 0)
            {
                lines.Add("");
                lines.Add($"Tone Shift Suggestions ({Suggestions.Count}):");
                foreach (var s in Suggestions.Take(5))
                    lines.Add($"  \"{s.Original}\" → \"{s.Replacement}\" ({s.TargetTone}: {s.Reason})");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Result of comparing sentiment between two prompts.
    /// </summary>
    public class SentimentComparison
    {
        /// <summary>Gets the report for prompt A.</summary>
        public SentimentReport ReportA { get; internal set; } = new();

        /// <summary>Gets the report for prompt B.</summary>
        public SentimentReport ReportB { get; internal set; } = new();

        /// <summary>Gets whether the dominant tone differs.</summary>
        public bool ToneShifted { get; internal set; }

        /// <summary>Gets whether the polarity differs.</summary>
        public bool PolarityShifted { get; internal set; }

        /// <summary>Gets a summary of differences.</summary>
        public List<string> Differences { get; internal set; } = new();
    }

    /// <summary>
    /// Analyzes prompt text for tone, sentiment polarity, and register.
    /// Provides heuristic-based detection of polite, assertive, formal, casual,
    /// urgent, and encouraging language with tone shift suggestions.
    /// </summary>
    /// <example>
    /// <code>
    /// var analyzer = new PromptSentimentAnalyzer();
    /// var report = analyzer.Analyze("Please summarize this document carefully.");
    /// Console.WriteLine(report.DominantTone); // Polite
    /// Console.WriteLine(report.ToSummary());
    ///
    /// // Get suggestions to shift to a more assertive tone
    /// var shifted = analyzer.Analyze("Please summarize this document carefully.",
    ///     targetTone: SentimentTone.Assertive);
    /// foreach (var s in shifted.Suggestions)
    ///     Console.WriteLine($"{s.Original} → {s.Replacement}");
    ///
    /// // Compare two prompts
    /// var cmp = analyzer.Compare(
    ///     "Please help me write a summary.",
    ///     "Write a summary now.");
    /// Console.WriteLine(cmp.ToneShifted); // true
    /// </code>
    /// </example>
    public class PromptSentimentAnalyzer
    {
        private static readonly Regex WordRegex = new(@"\b\w+\b", RegexOptions.Compiled);
        private static readonly Regex SentenceRegex = new(@"[^.!?]*[.!?]+", RegexOptions.Compiled);

        private static readonly Dictionary<SentimentTone, (string pattern, double weight)[]> TonePatterns = new()
        {
            [SentimentTone.Polite] = new[]
            {
                (@"\bplease\b", 0.8),
                (@"\bthank(?:s| you)\b", 0.7),
                (@"\bwould you\b", 0.7),
                (@"\bcould you\b", 0.7),
                (@"\bkindly\b", 0.8),
                (@"\bif you (?:don't mind|could|would)\b", 0.9),
                (@"\bi(?:'d| would) appreciate\b", 0.8),
                (@"\bmay i\b", 0.6),
                (@"\bI was wondering\b", 0.7),
                (@"\bwould it be possible\b", 0.8),
            },
            [SentimentTone.Assertive] = new[]
            {
                (@"\byou must\b", 0.9),
                (@"\byou need to\b", 0.7),
                (@"\bdo not\b", 0.6),
                (@"\bdon't\b", 0.5),
                (@"\bnever\b", 0.6),
                (@"\balways\b", 0.5),
                (@"\bmake sure\b", 0.7),
                (@"\bensure\b", 0.6),
                (@"\bit is essential\b", 0.8),
                (@"\byou shall\b", 0.8),
            },
            [SentimentTone.Formal] = new[]
            {
                (@"\bhereby\b", 0.9),
                (@"\bfurthermore\b", 0.7),
                (@"\bmoreover\b", 0.7),
                (@"\bnevertheless\b", 0.8),
                (@"\bnotwithstanding\b", 0.9),
                (@"\baccordingly\b", 0.7),
                (@"\bwherein\b", 0.8),
                (@"\btherefore\b", 0.6),
                (@"\bconsequently\b", 0.7),
                (@"\bregarding\b", 0.5),
            },
            [SentimentTone.Casual] = new[]
            {
                (@"\bhey\b", 0.8),
                (@"\bhi\b", 0.5),
                (@"\bcool\b", 0.5),
                (@"\bawesome\b", 0.6),
                (@"\bstuff\b", 0.5),
                (@"\bkinda\b", 0.7),
                (@"\bwanna\b", 0.8),
                (@"\bgonna\b", 0.8),
                (@"\byeah\b", 0.7),
                (@"\bnope\b", 0.7),
                (@"\blol\b", 0.9),
                (@"\bbtw\b", 0.8),
            },
            [SentimentTone.Urgent] = new[]
            {
                (@"\bimmediately\b", 0.9),
                (@"\burgent(?:ly)?\b", 0.9),
                (@"\basap\b", 0.9),
                (@"\bright now\b", 0.8),
                (@"\bcritical\b", 0.7),
                (@"\btime.sensitive\b", 0.8),
                (@"\bhurry\b", 0.8),
                (@"\bdeadline\b", 0.6),
                (@"\bimminent\b", 0.7),
                (@"!{2,}", 0.7),
            },
            [SentimentTone.Encouraging] = new[]
            {
                (@"\bgreat job\b", 0.8),
                (@"\bwell done\b", 0.8),
                (@"\bkeep (?:it )?up\b", 0.7),
                (@"\bexcellent\b", 0.6),
                (@"\bfantastic\b", 0.7),
                (@"\bwonderful\b", 0.7),
                (@"\byou(?:'re| are) doing (?:great|well|amazing)\b", 0.9),
                (@"\bi believe\b", 0.5),
                (@"\bimpressive\b", 0.6),
                (@"\bbravo\b", 0.8),
            }
        };

        private static readonly Dictionary<string, double> PositiveWords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["good"] = 0.5, ["great"] = 0.7, ["excellent"] = 0.8, ["best"] = 0.7,
            ["helpful"] = 0.6, ["clear"] = 0.5, ["effective"] = 0.6, ["accurate"] = 0.6,
            ["improve"] = 0.5, ["benefit"] = 0.5, ["success"] = 0.6, ["perfect"] = 0.7,
            ["wonderful"] = 0.7, ["fantastic"] = 0.7, ["amazing"] = 0.7, ["love"] = 0.6,
        };

        private static readonly Dictionary<string, double> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bad"] = 0.5, ["wrong"] = 0.6, ["error"] = 0.5, ["fail"] = 0.6,
            ["terrible"] = 0.8, ["awful"] = 0.7, ["never"] = 0.4, ["worst"] = 0.8,
            ["poor"] = 0.5, ["broken"] = 0.5, ["useless"] = 0.7, ["hate"] = 0.7,
            ["ugly"] = 0.5, ["horrible"] = 0.7, ["stupid"] = 0.6, ["annoying"] = 0.5,
        };

        // Tone shift replacements: (source pattern, replacement, target tone, reason)
        private static readonly (string pattern, string replacement, SentimentTone target, string reason)[] ShiftRules =
        {
            // Polite → Assertive
            (@"\bplease\s+", "", SentimentTone.Assertive, "Remove hedging for directness"),
            (@"\bcould you\b", "You should", SentimentTone.Assertive, "Strengthen request to directive"),
            (@"\bwould you\b", "You must", SentimentTone.Assertive, "Strengthen request to command"),
            (@"\bif you could\b", "You need to", SentimentTone.Assertive, "Remove conditional phrasing"),
            (@"\bI was wondering if\b", "I need you to", SentimentTone.Assertive, "Remove hedging"),

            // Assertive → Polite
            (@"\byou must\b", "Could you please", SentimentTone.Polite, "Soften command to request"),
            (@"\byou need to\b", "Would you kindly", SentimentTone.Polite, "Soften directive"),
            (@"\bdo not\b", "Please avoid", SentimentTone.Polite, "Soften prohibition"),
            (@"\bmake sure\b", "It would be great if you could", SentimentTone.Polite, "Soften requirement"),

            // Casual → Formal
            (@"\bhey\b", "Greetings", SentimentTone.Formal, "Formalize greeting"),
            (@"\bkinda\b", "somewhat", SentimentTone.Formal, "Use standard language"),
            (@"\bwanna\b", "would like to", SentimentTone.Formal, "Formalize contraction"),
            (@"\bgonna\b", "going to", SentimentTone.Formal, "Expand contraction"),
            (@"\bstuff\b", "materials", SentimentTone.Formal, "Use precise terminology"),
            (@"\bcool\b", "acceptable", SentimentTone.Formal, "Use formal register"),

            // Formal → Casual
            (@"\bfurthermore\b", "also", SentimentTone.Casual, "Simplify connector"),
            (@"\bnevertheless\b", "still", SentimentTone.Casual, "Simplify connector"),
            (@"\baccordingly\b", "so", SentimentTone.Casual, "Simplify connector"),
            (@"\btherefore\b", "so", SentimentTone.Casual, "Simplify connector"),
            (@"\bregarding\b", "about", SentimentTone.Casual, "Simplify preposition"),
        };

        /// <summary>
        /// Analyze sentiment, tone, and register of a prompt.
        /// </summary>
        /// <param name="text">The prompt text to analyze.</param>
        /// <param name="targetTone">Optional target tone; when set, generates shift suggestions toward it.</param>
        /// <returns>A <see cref="SentimentReport"/> with tone distribution, signals, and optional suggestions.</returns>
        /// <exception cref="ArgumentException">Thrown when text is null or whitespace.</exception>
        public SentimentReport Analyze(string text, SentimentTone? targetTone = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text must not be null or empty.", nameof(text));

            var report = new SentimentReport { Text = text };
            var words = WordRegex.Matches(text);
            var sentences = SentenceRegex.Matches(text);

            report.WordCount = words.Count;
            report.SentenceCount = Math.Max(sentences.Count, 1);
            report.QuestionCount = text.Count(c => c == '?');
            report.ExclamationCount = text.Count(c => c == '!');

            // Detect imperative sentences (start with a verb-like word, no subject)
            int imperativeCount = 0;
            foreach (Match s in sentences)
            {
                string trimmed = s.Value.Trim();
                if (trimmed.Length == 0) continue;
                // Simple heuristic: starts with uppercase verb (no pronoun/article)
                var firstWord = WordRegex.Match(trimmed);
                if (firstWord.Success)
                {
                    string fw = firstWord.Value.ToLowerInvariant();
                    if (!IsSubjectWord(fw) && !IsArticle(fw) && !trimmed.EndsWith("?"))
                        imperativeCount++;
                }
            }
            report.ImperativeRatio = (double)imperativeCount / report.SentenceCount;

            // Detect tone signals
            var toneScores = new Dictionary<SentimentTone, double>();
            foreach (SentimentTone tone in Enum.GetValues(typeof(SentimentTone)))
                toneScores[tone] = 0;

            string lower = text.ToLowerInvariant();
            foreach (var (tone, patterns) in TonePatterns)
            {
                foreach (var (pattern, weight) in patterns)
                {
                    foreach (Match m in Regex.Matches(lower, pattern, RegexOptions.IgnoreCase))
                    {
                        report.Signals.Add(new ToneSignal
                        {
                            Tone = tone,
                            Match = m.Value,
                            Strength = weight
                        });
                        toneScores[tone] += weight;
                    }
                }
            }

            // Add imperative score to Assertive
            toneScores[SentimentTone.Assertive] += report.ImperativeRatio * 2.0;

            // Compute distribution
            double totalScore = toneScores.Values.Sum();
            if (totalScore < 0.01)
            {
                // No strong signals → Neutral
                report.DominantTone = SentimentTone.Neutral;
                report.Confidence = 0.5;
                report.ToneDistribution[SentimentTone.Neutral] = 1.0;
            }
            else
            {
                foreach (var kv in toneScores)
                    report.ToneDistribution[kv.Key] = kv.Value / totalScore;

                var dominant = toneScores.OrderByDescending(x => x.Value).First();
                report.DominantTone = dominant.Key;
                report.Confidence = Math.Min(1.0, dominant.Value / totalScore);
            }

            // Polarity
            double posScore = 0, negScore = 0;
            foreach (Match w in words)
            {
                string wl = w.Value.ToLowerInvariant();
                if (PositiveWords.TryGetValue(wl, out double pv)) posScore += pv;
                if (NegativeWords.TryGetValue(wl, out double nv)) negScore += nv;
            }
            if (posScore > negScore + 0.3) report.Polarity = SentimentPolarity.Positive;
            else if (negScore > posScore + 0.3) report.Polarity = SentimentPolarity.Negative;
            else report.Polarity = SentimentPolarity.Neutral;

            // Generate shift suggestions if target tone specified
            if (targetTone.HasValue)
            {
                var target = targetTone.Value;
                foreach (var (pattern, replacement, ruleTone, reason) in ShiftRules)
                {
                    if (ruleTone != target) continue;
                    foreach (Match m in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
                    {
                        report.Suggestions.Add(new ToneShiftSuggestion
                        {
                            Original = m.Value,
                            Replacement = replacement,
                            TargetTone = target,
                            Reason = reason
                        });
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Compare the sentiment of two prompts side by side.
        /// </summary>
        /// <param name="textA">First prompt.</param>
        /// <param name="textB">Second prompt.</param>
        /// <returns>A <see cref="SentimentComparison"/> highlighting tone and polarity shifts.</returns>
        public SentimentComparison Compare(string textA, string textB)
        {
            var a = Analyze(textA);
            var b = Analyze(textB);
            var cmp = new SentimentComparison
            {
                ReportA = a,
                ReportB = b,
                ToneShifted = a.DominantTone != b.DominantTone,
                PolarityShifted = a.Polarity != b.Polarity,
            };

            if (cmp.ToneShifted)
                cmp.Differences.Add($"Tone shifted from {a.DominantTone} to {b.DominantTone}");
            if (cmp.PolarityShifted)
                cmp.Differences.Add($"Polarity shifted from {a.Polarity} to {b.Polarity}");

            // Compare distribution deltas
            foreach (SentimentTone tone in Enum.GetValues(typeof(SentimentTone)))
            {
                double da = a.ToneDistribution.GetValueOrDefault(tone);
                double db = b.ToneDistribution.GetValueOrDefault(tone);
                double delta = db - da;
                if (Math.Abs(delta) > 0.15)
                    cmp.Differences.Add($"{tone}: {delta:+0.0%;-0.0%} change");
            }

            if (Math.Abs(a.ImperativeRatio - b.ImperativeRatio) > 0.2)
                cmp.Differences.Add($"Imperative ratio: {a.ImperativeRatio:P0} → {b.ImperativeRatio:P0}");

            return cmp;
        }

        /// <summary>
        /// Apply tone shift rules to rewrite a prompt toward the target tone.
        /// </summary>
        /// <param name="text">Original prompt text.</param>
        /// <param name="targetTone">Desired tone to shift toward.</param>
        /// <returns>Rewritten prompt text.</returns>
        public string Rewrite(string text, SentimentTone targetTone)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text must not be null or empty.", nameof(text));

            string result = text;
            foreach (var (pattern, replacement, tone, _) in ShiftRules)
            {
                if (tone != targetTone) continue;
                result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
            }
            return result.Trim();
        }

        private static bool IsSubjectWord(string w) =>
            w is "i" or "you" or "he" or "she" or "it" or "we" or "they" or
                 "this" or "that" or "these" or "those" or "who" or "what" or
                 "which" or "there" or "here";

        private static bool IsArticle(string w) =>
            w is "a" or "an" or "the" or "my" or "your" or "his" or "her" or
                 "its" or "our" or "their" or "some" or "any" or "each" or "every";
    }
}
