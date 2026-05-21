namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Recognized emotion categories for prompt analysis.
    /// </summary>
    public enum Emotion
    {
        /// <summary>Neutral / no strong emotion detected.</summary>
        Neutral,
        /// <summary>Positive, happy, enthusiastic.</summary>
        Joy,
        /// <summary>Frustrated, angry, annoyed.</summary>
        Anger,
        /// <summary>Sad, disappointed, discouraged.</summary>
        Sadness,
        /// <summary>Worried, anxious, uncertain.</summary>
        Fear,
        /// <summary>Curious, surprised, amazed.</summary>
        Surprise,
        /// <summary>Disgusted, repulsed.</summary>
        Disgust,
        /// <summary>Trusting, confident, assured.</summary>
        Trust,
        /// <summary>Expecting, anticipating, hopeful.</summary>
        Anticipation,
        /// <summary>Urgent, pressing, time-sensitive.</summary>
        Urgency
    }

    /// <summary>
    /// Result of emotion analysis on a single text.
    /// </summary>
    public class EmotionScore
    {
        /// <summary>The dominant emotion detected.</summary>
        public Emotion Dominant { get; init; }

        /// <summary>Confidence score for the dominant emotion (0.0–1.0).</summary>
        public double Confidence { get; init; }

        /// <summary>All emotion scores, normalized to sum to 1.0.</summary>
        public IReadOnlyDictionary<Emotion, double> Scores { get; init; } = new Dictionary<Emotion, double>();

        /// <summary>Overall valence: positive (&gt;0), negative (&lt;0), neutral (~0).</summary>
        public double Valence { get; init; }

        /// <summary>Emotional intensity/arousal (0.0–1.0).</summary>
        public double Arousal { get; init; }

        /// <summary>Detected emotional markers (words/phrases that contributed).</summary>
        public IReadOnlyList<string> Markers { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Tracks emotional drift across a sequence of messages.
    /// </summary>
    public class EmotionDrift
    {
        /// <summary>Per-message emotion scores in order.</summary>
        public IReadOnlyList<EmotionScore> Timeline { get; init; } = Array.Empty<EmotionScore>();

        /// <summary>Valence trend direction.</summary>
        public string ValenceTrend { get; init; } = "stable";

        /// <summary>Arousal trend direction.</summary>
        public string ArousalTrend { get; init; } = "stable";

        /// <summary>Whether significant emotional shift was detected.</summary>
        public bool ShiftDetected { get; init; }

        /// <summary>Index where the most significant shift occurred (-1 if none).</summary>
        public int ShiftIndex { get; init; } = -1;

        /// <summary>Summary of the emotional arc.</summary>
        public string Summary { get; init; } = "";
    }

    /// <summary>
    /// Analyzes the emotional tone and sentiment of prompts and responses
    /// using lexicon-based heuristics. Helps users understand how their
    /// prompts come across, detect emotional drift in conversations, and
    /// maintain an appropriate tone for their use case.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var analyzer = new PromptEmotionAnalyzer();
    ///
    /// // Analyze a single prompt
    /// var score = analyzer.Analyze("I'm so frustrated! This keeps failing and I need help urgently!");
    /// Console.WriteLine($"Dominant: {score.Dominant} (confidence: {score.Confidence:P0})");
    /// Console.WriteLine($"Valence: {score.Valence:F2}, Arousal: {score.Arousal:F2}");
    ///
    /// // Track drift across a conversation
    /// var messages = new[]
    /// {
    ///     "Hi, can you help me with something?",
    ///     "This isn't working the way I expected.",
    ///     "I've tried everything and nothing works!",
    ///     "Oh wait, I think I found the issue. Thanks!"
    /// };
    /// var drift = analyzer.TrackDrift(messages);
    /// Console.WriteLine($"Shift detected: {drift.ShiftDetected}");
    /// Console.WriteLine($"Arc: {drift.Summary}");
    ///
    /// // Get tone suggestions
    /// var suggestions = analyzer.SuggestToneAdjustments("FIX THIS NOW!!! It's completely broken!");
    /// foreach (var s in suggestions)
    ///     Console.WriteLine($"  - {s}");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptEmotionAnalyzer
    {
        // Lexicon: emotion → (words, weight)
        private static readonly Dictionary<Emotion, List<(string Word, double Weight)>> Lexicon = new()
        {
            [Emotion.Joy] = new()
            {
                ("happy", 0.8), ("great", 0.7), ("love", 0.9), ("excellent", 0.8), ("wonderful", 0.9),
                ("fantastic", 0.9), ("awesome", 0.8), ("amazing", 0.9), ("good", 0.5), ("nice", 0.5),
                ("thank", 0.6), ("thanks", 0.6), ("pleased", 0.7), ("delighted", 0.8), ("excited", 0.8),
                ("perfect", 0.8), ("brilliant", 0.8), ("enjoy", 0.7), ("glad", 0.7), ("impressive", 0.7),
                ("beautiful", 0.7), ("cheerful", 0.8), ("thrilled", 0.9), ("superb", 0.8), ("outstanding", 0.8)
            },
            [Emotion.Anger] = new()
            {
                ("angry", 0.9), ("frustrated", 0.8), ("annoyed", 0.7), ("furious", 1.0), ("hate", 0.9),
                ("terrible", 0.8), ("awful", 0.8), ("ridiculous", 0.7), ("stupid", 0.8), ("broken", 0.5),
                ("useless", 0.8), ("unacceptable", 0.8), ("outrageous", 0.9), ("infuriating", 0.9),
                ("rage", 0.9), ("mad", 0.7), ("worst", 0.8), ("pathetic", 0.8), ("disgusting", 0.8),
                ("incompetent", 0.8), ("absurd", 0.7), ("horrible", 0.8), ("atrocious", 0.9)
            },
            [Emotion.Sadness] = new()
            {
                ("sad", 0.8), ("disappointed", 0.7), ("unfortunately", 0.5), ("sorry", 0.4), ("miss", 0.5),
                ("regret", 0.7), ("unhappy", 0.8), ("depressed", 0.9), ("hopeless", 0.9), ("miserable", 0.9),
                ("heartbroken", 0.9), ("lonely", 0.7), ("gloomy", 0.7), ("grief", 0.9), ("despair", 0.9),
                ("melancholy", 0.8), ("downcast", 0.7), ("discouraged", 0.7), ("sorrow", 0.8)
            },
            [Emotion.Fear] = new()
            {
                ("worried", 0.7), ("afraid", 0.8), ("scared", 0.8), ("anxious", 0.7), ("nervous", 0.7),
                ("concerned", 0.5), ("uncertain", 0.5), ("risky", 0.6), ("dangerous", 0.7), ("threat", 0.7),
                ("panic", 0.9), ("terrified", 0.9), ("dread", 0.8), ("alarmed", 0.7), ("uneasy", 0.6),
                ("frightened", 0.8), ("apprehensive", 0.6), ("hesitant", 0.5), ("vulnerable", 0.6)
            },
            [Emotion.Surprise] = new()
            {
                ("wow", 0.8), ("surprised", 0.7), ("unexpected", 0.6), ("shocking", 0.8), ("incredible", 0.7),
                ("unbelievable", 0.8), ("astonishing", 0.8), ("remarkable", 0.6), ("curious", 0.5),
                ("strange", 0.5), ("weird", 0.5), ("whoa", 0.8), ("omg", 0.8), ("astounding", 0.8),
                ("startling", 0.7), ("stunning", 0.7), ("mind-blowing", 0.9)
            },
            [Emotion.Disgust] = new()
            {
                ("gross", 0.8), ("disgusting", 0.9), ("revolting", 0.9), ("nasty", 0.8), ("repulsive", 0.9),
                ("vile", 0.9), ("sickening", 0.8), ("foul", 0.8), ("loathsome", 0.9), ("abhorrent", 0.9),
                ("repugnant", 0.9), ("offensive", 0.7), ("distasteful", 0.7), ("yuck", 0.7)
            },
            [Emotion.Trust] = new()
            {
                ("trust", 0.8), ("reliable", 0.7), ("confident", 0.7), ("sure", 0.5), ("certain", 0.6),
                ("dependable", 0.7), ("secure", 0.6), ("honest", 0.7), ("faithful", 0.7), ("loyal", 0.7),
                ("authentic", 0.6), ("genuine", 0.6), ("credible", 0.6), ("proven", 0.6), ("safe", 0.5),
                ("solid", 0.5), ("stable", 0.5), ("consistent", 0.5), ("verified", 0.6)
            },
            [Emotion.Anticipation] = new()
            {
                ("hope", 0.7), ("expect", 0.5), ("looking forward", 0.7), ("eager", 0.8), ("excited", 0.7),
                ("anticipate", 0.7), ("plan", 0.4), ("prepare", 0.4), ("ready", 0.5), ("soon", 0.4),
                ("upcoming", 0.5), ("promising", 0.6), ("optimistic", 0.7), ("can't wait", 0.8),
                ("potential", 0.5), ("prospect", 0.5), ("hopeful", 0.7)
            },
            [Emotion.Urgency] = new()
            {
                ("urgent", 0.9), ("asap", 0.9), ("immediately", 0.8), ("hurry", 0.8), ("now", 0.4),
                ("critical", 0.8), ("emergency", 0.9), ("deadline", 0.7), ("rush", 0.7), ("time-sensitive", 0.8),
                ("priority", 0.6), ("pressing", 0.7), ("right away", 0.8), ("quick", 0.5), ("fast", 0.4),
                ("blocking", 0.7), ("blocker", 0.7), ("showstopper", 0.8), ("overdue", 0.7)
            }
        };

        // Intensifiers boost nearby emotion words
        private static readonly Dictionary<string, double> Intensifiers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["very"] = 1.3, ["extremely"] = 1.5, ["incredibly"] = 1.5, ["absolutely"] = 1.4,
            ["totally"] = 1.3, ["completely"] = 1.4, ["really"] = 1.2, ["so"] = 1.2,
            ["quite"] = 1.1, ["rather"] = 1.1, ["utterly"] = 1.5, ["super"] = 1.3
        };

        // Negators flip valence
        private static readonly HashSet<string> Negators = new(StringComparer.OrdinalIgnoreCase)
        {
            "not", "no", "never", "neither", "nobody", "nothing", "nowhere",
            "nor", "cannot", "can't", "don't", "doesn't", "didn't", "won't",
            "wouldn't", "shouldn't", "couldn't", "isn't", "aren't", "wasn't", "weren't"
        };

        // Punctuation patterns that affect arousal
        private static readonly Regex ExclamationPattern = new(@"!{1,}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex CapsPattern = new(@"\b[A-Z]{2,}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex QuestionPattern = new(@"\?{1,}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex EllipsisPattern = new(@"\.{3,}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Analyzes the emotional content of a single text.
        /// </summary>
        /// <param name="text">The text to analyze.</param>
        /// <returns>Emotion scores with dominant emotion and markers.</returns>
        /// <exception cref="ArgumentException">If text is null or empty.</exception>
        public EmotionScore Analyze(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));

            var words = Tokenize(text);
            var rawScores = new Dictionary<Emotion, double>();
            var markers = new List<string>();

            foreach (var emotion in Lexicon.Keys)
                rawScores[emotion] = 0;

            for (int i = 0; i < words.Count; i++)
            {
                var word = words[i].ToLowerInvariant();

                foreach (var (emotion, lexEntries) in Lexicon)
                {
                    foreach (var (lexWord, weight) in lexEntries)
                    {
                        if (word == lexWord || (lexWord.Contains(' ') && MatchPhrase(words, i, lexWord)))
                        {
                            double adjustedWeight = weight;

                            // Check for intensifier before this word
                            if (i > 0 && Intensifiers.TryGetValue(words[i - 1], out double mult))
                                adjustedWeight *= mult;

                            // Check for negation (within 3 words before)
                            bool negated = false;
                            for (int j = Math.Max(0, i - 3); j < i; j++)
                            {
                                if (Negators.Contains(words[j]))
                                {
                                    negated = true;
                                    break;
                                }
                            }

                            if (negated)
                            {
                                // Negation reduces score and shifts toward neutral
                                adjustedWeight *= 0.3;
                            }

                            rawScores[emotion] += adjustedWeight;
                            markers.Add(lexWord);
                            break; // Only match first lexicon entry per word
                        }
                    }
                }
            }

            // Punctuation-based arousal boost
            double arousalBoost = 0;
            int exclamations = ExclamationPattern.Matches(text).Count;
            int capsWords = CapsPattern.Matches(text).Count;
            arousalBoost += Math.Min(exclamations * 0.15, 0.4);
            arousalBoost += Math.Min(capsWords * 0.1, 0.3);

            // Boost urgency/anger for caps/exclamations
            if (exclamations > 0 || capsWords > 1)
            {
                rawScores[Emotion.Urgency] += exclamations * 0.2 + capsWords * 0.15;
                rawScores[Emotion.Anger] += exclamations * 0.1 + capsWords * 0.1;
            }

            // Normalize scores
            double total = rawScores.Values.Sum();
            var normalized = new Dictionary<Emotion, double>();

            if (total > 0)
            {
                foreach (var (emotion, score) in rawScores)
                    normalized[emotion] = Math.Round(score / total, 4);
            }
            else
            {
                // No emotions detected → neutral
                foreach (var emotion in rawScores.Keys)
                    normalized[emotion] = emotion == Emotion.Neutral ? 1.0 : 0.0;
                normalized[Emotion.Neutral] = 1.0;
            }

            // Add neutral score if not present
            if (!normalized.ContainsKey(Emotion.Neutral))
                normalized[Emotion.Neutral] = 0;

            // Determine dominant
            var dominant = normalized.MaxBy(kv => kv.Value)!.Key;
            double confidence = normalized[dominant];

            // If confidence is very low, default to neutral
            if (total < 0.3)
            {
                dominant = Emotion.Neutral;
                confidence = 1.0 - (total / 0.3);
            }

            // Calculate valence (-1 to +1)
            double positiveSum = (rawScores.GetValueOrDefault(Emotion.Joy) +
                                  rawScores.GetValueOrDefault(Emotion.Trust) * 0.7 +
                                  rawScores.GetValueOrDefault(Emotion.Anticipation) * 0.5 +
                                  rawScores.GetValueOrDefault(Emotion.Surprise) * 0.2);
            double negativeSum = (rawScores.GetValueOrDefault(Emotion.Anger) +
                                  rawScores.GetValueOrDefault(Emotion.Sadness) +
                                  rawScores.GetValueOrDefault(Emotion.Fear) * 0.8 +
                                  rawScores.GetValueOrDefault(Emotion.Disgust) +
                                  rawScores.GetValueOrDefault(Emotion.Urgency) * 0.3);

            double valence = total > 0
                ? Math.Clamp((positiveSum - negativeSum) / total, -1.0, 1.0)
                : 0;

            // Calculate arousal (0 to 1)
            double highArousalSum = rawScores.GetValueOrDefault(Emotion.Anger) +
                                    rawScores.GetValueOrDefault(Emotion.Fear) +
                                    rawScores.GetValueOrDefault(Emotion.Joy) * 0.7 +
                                    rawScores.GetValueOrDefault(Emotion.Surprise) * 0.8 +
                                    rawScores.GetValueOrDefault(Emotion.Urgency);
            double arousal = total > 0
                ? Math.Clamp(highArousalSum / total + arousalBoost, 0, 1.0)
                : arousalBoost;

            return new EmotionScore
            {
                Dominant = dominant,
                Confidence = Math.Round(confidence, 4),
                Scores = normalized,
                Valence = Math.Round(valence, 4),
                Arousal = Math.Round(Math.Min(arousal, 1.0), 4),
                Markers = markers.Distinct().ToList()
            };
        }

        /// <summary>
        /// Tracks emotional drift across a sequence of messages.
        /// </summary>
        /// <param name="messages">Ordered messages to analyze.</param>
        /// <returns>Drift analysis with timeline and shift detection.</returns>
        /// <exception cref="ArgumentException">If messages is null or empty.</exception>
        public EmotionDrift TrackDrift(IEnumerable<string> messages)
        {
            var msgList = messages?.ToList() ?? throw new ArgumentException("Messages cannot be null.", nameof(messages));
            if (msgList.Count == 0)
                throw new ArgumentException("At least one message is required.", nameof(messages));

            var timeline = msgList
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => Analyze(m))
                .ToList();

            if (timeline.Count < 2)
            {
                return new EmotionDrift
                {
                    Timeline = timeline,
                    ValenceTrend = "stable",
                    ArousalTrend = "stable",
                    ShiftDetected = false,
                    Summary = timeline.Count == 1
                        ? $"Single message: {timeline[0].Dominant} (valence: {timeline[0].Valence:F2})"
                        : "No data"
                };
            }

            // Detect valence/arousal trends
            var valences = timeline.Select(s => s.Valence).ToArray();
            var arousals = timeline.Select(s => s.Arousal).ToArray();

            string valenceTrend = DetectTrendDirection(valences);
            string arousalTrend = DetectTrendDirection(arousals);

            // Detect significant shifts (largest valence change between consecutive messages)
            double maxShift = 0;
            int shiftIdx = -1;
            for (int i = 1; i < timeline.Count; i++)
            {
                double delta = Math.Abs(timeline[i].Valence - timeline[i - 1].Valence);
                if (delta > maxShift)
                {
                    maxShift = delta;
                    shiftIdx = i;
                }
            }

            bool shiftDetected = maxShift > 0.4; // Threshold for "significant" shift

            // Build summary
            var firstEmotion = timeline.First().Dominant;
            var lastEmotion = timeline.Last().Dominant;
            string summary;

            if (firstEmotion == lastEmotion)
            {
                summary = $"Consistent {firstEmotion} tone throughout ({timeline.Count} messages)";
            }
            else if (shiftDetected)
            {
                summary = $"Emotional shift from {firstEmotion} to {lastEmotion} " +
                          $"(shift at message {shiftIdx + 1}, Δvalence={maxShift:F2})";
            }
            else
            {
                summary = $"Gradual transition from {firstEmotion} to {lastEmotion} " +
                          $"across {timeline.Count} messages";
            }

            return new EmotionDrift
            {
                Timeline = timeline,
                ValenceTrend = valenceTrend,
                ArousalTrend = arousalTrend,
                ShiftDetected = shiftDetected,
                ShiftIndex = shiftDetected ? shiftIdx : -1,
                Summary = summary
            };
        }

        /// <summary>
        /// Suggests tone adjustments for a prompt based on its emotional content.
        /// Useful for ensuring prompts have the right tone for their intended purpose.
        /// </summary>
        /// <param name="text">The prompt text to evaluate.</param>
        /// <returns>List of actionable tone adjustment suggestions.</returns>
        public IReadOnlyList<string> SuggestToneAdjustments(string text)
        {
            var score = Analyze(text);
            var suggestions = new List<string>();

            // High anger/frustration
            if (score.Scores.GetValueOrDefault(Emotion.Anger) > 0.3)
            {
                suggestions.Add("Consider softening frustrated language — aggressive prompts can lead to defensive or overly apologetic responses.");
                if (score.Markers.Any(m => CapsPattern.IsMatch(m) || m.Length > 0))
                    suggestions.Add("Replace ALL CAPS words with regular casing for a calmer tone.");
            }

            // High urgency
            if (score.Scores.GetValueOrDefault(Emotion.Urgency) > 0.3)
            {
                suggestions.Add("High urgency detected. If this is for an LLM, urgency markers don't speed up processing — consider replacing with specific priority instructions.");
            }

            // Very negative valence
            if (score.Valence < -0.5)
            {
                suggestions.Add("Strongly negative tone detected. Reframing as a constructive request often yields better results.");
            }

            // High arousal (lots of exclamation marks, caps)
            if (score.Arousal > 0.7)
            {
                int excl = ExclamationPattern.Matches(text).Count;
                if (excl > 2)
                    suggestions.Add($"Found {excl} exclamation marks — reducing to 0-1 will make the prompt feel more measured.");
            }

            // Very neutral/flat
            if (score.Dominant == Emotion.Neutral && score.Arousal < 0.1)
            {
                suggestions.Add("Very neutral/flat tone. If seeking creative or enthusiastic responses, adding some positive framing can help.");
            }

            // Fear/uncertainty
            if (score.Scores.GetValueOrDefault(Emotion.Fear) > 0.3)
            {
                suggestions.Add("Uncertain/anxious language detected. Being more direct about what you need can produce more confident responses.");
            }

            // Excessive politeness
            if (score.Markers.Count(m => m == "thank" || m == "thanks" || m == "sorry") > 2)
            {
                suggestions.Add("Multiple politeness markers detected. While polite prompts are fine, excessive hedging can make instructions less clear.");
            }

            if (suggestions.Count == 0)
                suggestions.Add("Tone looks well-balanced for most use cases.");

            return suggestions;
        }

        /// <summary>
        /// Generates a compact emotion summary suitable for logging or display.
        /// </summary>
        /// <param name="text">Text to analyze.</param>
        /// <returns>One-line summary string.</returns>
        public string Summarize(string text)
        {
            var score = Analyze(text);
            string valenceEmoji = score.Valence > 0.3 ? "😊" : score.Valence < -0.3 ? "😟" : "😐";
            string arousalBar = score.Arousal > 0.7 ? "🔥" : score.Arousal > 0.4 ? "⚡" : "💤";
            return $"{valenceEmoji} {score.Dominant} ({score.Confidence:P0}) | Valence: {score.Valence:F2} | Arousal: {arousalBar} {score.Arousal:F2}";
        }

        /// <summary>
        /// Serializes an emotion score to JSON.
        /// </summary>
        public static string ToJson(EmotionScore score, bool indented = true)
        {
            var data = new
            {
                dominant = score.Dominant.ToString(),
                confidence = score.Confidence,
                valence = score.Valence,
                arousal = score.Arousal,
                scores = score.Scores.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                markers = score.Markers
            };
            return JsonSerializer.Serialize(data, SerializationGuards.WriteOptions(indented));
        }

        // ──────────── Internals ────────────

        private static List<string> Tokenize(string text)
        {
            // Simple word tokenizer: split on whitespace and punctuation boundaries
            return Regex.Split(text, @"[\s]+")
                .Where(w => w.Length > 0)
                .Select(w => w.Trim(',', '.', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}'))
                .Where(w => w.Length > 0)
                .ToList();
        }

        private static bool MatchPhrase(List<string> words, int startIndex, string phrase)
        {
            var phraseWords = phrase.Split(' ');
            if (startIndex + phraseWords.Length > words.Count) return false;
            for (int i = 0; i < phraseWords.Length; i++)
            {
                if (!words[startIndex + i].Equals(phraseWords[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private static string DetectTrendDirection(double[] values)
        {
            if (values.Length < 2) return "stable";

            // Simple linear regression slope
            double n = values.Length;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sumX += i;
                sumY += values[i];
                sumXY += i * values[i];
                sumX2 += i * i;
            }

            double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

            if (slope > 0.05) return "improving";
            if (slope < -0.05) return "declining";
            return "stable";
        }
    }
}
