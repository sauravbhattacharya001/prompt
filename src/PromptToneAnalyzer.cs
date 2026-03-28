namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Recognised tone categories for prompt analysis.
    /// Distinct from <see cref="PromptTone"/> which is used by the variant generator.
    /// </summary>
    public enum ToneCategory
    {
        /// <summary>Formal, professional language.</summary>
        Formal,
        /// <summary>Casual, conversational language.</summary>
        Casual,
        /// <summary>Direct, commanding instructions.</summary>
        Assertive,
        /// <summary>Polite, hedged requests.</summary>
        Polite,
        /// <summary>Technical, domain-specific jargon.</summary>
        Technical,
        /// <summary>Creative, playful language.</summary>
        Creative,
        /// <summary>Neutral, balanced tone.</summary>
        Neutral
    }

    /// <summary>
    /// Score for a single tone dimension.
    /// </summary>
    public class ToneScore
    {
        /// <summary>Gets the tone category.</summary>
        public ToneCategory Tone { get; internal set; }

        /// <summary>Gets the confidence score (0.0–1.0).</summary>
        public double Confidence { get; internal set; }

        /// <summary>Gets the evidence phrases that contributed to this score.</summary>
        public List<string> Evidence { get; internal set; } = new();
    }

    /// <summary>
    /// A suggestion to shift the prompt toward a target tone.
    /// </summary>
    public class ToneSuggestion
    {
        /// <summary>Gets the original phrase.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>Gets the suggested replacement.</summary>
        public string Suggested { get; internal set; } = "";

        /// <summary>Gets an explanation of why this change helps.</summary>
        public string Reason { get; internal set; } = "";
    }

    /// <summary>
    /// Complete tone analysis result for a prompt.
    /// </summary>
    public class ToneAnalysisResult
    {
        /// <summary>Gets the dominant tone detected.</summary>
        public ToneCategory DominantTone { get; internal set; }

        /// <summary>Gets the confidence of the dominant tone (0.0–1.0).</summary>
        public double DominantConfidence { get; internal set; }

        /// <summary>Gets all tone scores, ordered by confidence descending.</summary>
        public List<ToneScore> Scores { get; internal set; } = new();

        /// <summary>Gets whether the tone is consistent throughout the prompt.</summary>
        public bool IsConsistent { get; internal set; }

        /// <summary>Gets a description of any tone shifts detected.</summary>
        public string? ConsistencyNote { get; internal set; }

        /// <summary>Gets suggestions to shift toward a target tone (populated when a target is specified).</summary>
        public List<ToneSuggestion> Suggestions { get; internal set; } = new();

        /// <summary>Gets a one-line human-readable summary.</summary>
        public string Summary { get; internal set; } = "";
    }

    /// <summary>
    /// Analyzes the tone of prompts and provides suggestions for tone adjustment.
    /// Detects formality, assertiveness, politeness, technicality, and creativity
    /// using keyword/pattern heuristics. Useful for ensuring prompts match the
    /// desired communication style for a given model or audience.
    /// </summary>
    public static class PromptToneAnalyzer
    {
        private static readonly HashSet<string> FormalMarkers = new(StringComparer.OrdinalIgnoreCase)
        {
            "furthermore", "therefore", "consequently", "hereby", "henceforth",
            "accordingly", "moreover", "nevertheless", "notwithstanding",
            "pursuant", "whereas", "herein", "aforementioned", "hereafter",
            "shall", "utilize", "facilitate", "endeavor", "ascertain"
        };

        private static readonly HashSet<string> CasualMarkers = new(StringComparer.OrdinalIgnoreCase)
        {
            "hey", "hi", "yo", "cool", "awesome", "gonna", "wanna", "gotta",
            "kinda", "sorta", "yeah", "nah", "ok", "okay", "lol", "btw",
            "tbh", "imo", "fyi", "nope", "yep", "stuff", "basically", "totally"
        };

        private static readonly HashSet<string> AssertiveMarkers = new(StringComparer.OrdinalIgnoreCase)
        {
            "always", "never", "must", "ensure", "require", "demand",
            "immediately", "exactly", "strictly", "mandatory", "critical",
            "essential", "imperative", "absolutely", "unconditionally"
        };

        private static readonly HashSet<string> PoliteMarkers = new(StringComparer.OrdinalIgnoreCase)
        {
            "please", "kindly", "would", "could", "might", "perhaps",
            "possibly", "appreciate", "thank", "thanks",
            "grateful", "sorry", "pardon", "excuse", "consider"
        };

        private static readonly HashSet<string> TechnicalMarkers = new(StringComparer.OrdinalIgnoreCase)
        {
            "api", "json", "xml", "http", "sql", "regex", "algorithm",
            "parameter", "schema", "endpoint", "payload", "serialization",
            "deserialization", "polymorphism", "abstraction", "refactor",
            "deploy", "pipeline", "latency", "throughput", "idempotent",
            "deterministic", "asynchronous", "synchronous", "middleware"
        };

        private static readonly HashSet<string> CreativeMarkers = new(StringComparer.OrdinalIgnoreCase)
        {
            "imagine", "pretend", "story", "creative", "fun", "playful",
            "whimsical", "fantasy", "dream", "adventure", "magic", "poem",
            "character", "scene", "dialogue", "narrate", "invent", "craft",
            "compose", "sing", "rhyme", "metaphor", "analogy"
        };

        private static readonly Dictionary<(ToneCategory from, ToneCategory to), List<(string pattern, string replacement, string reason)>> ShiftRules = new()
        {
            [(ToneCategory.Casual, ToneCategory.Formal)] = new()
            {
                ("gonna", "going to", "Replace informal contraction"),
                ("wanna", "want to", "Replace informal contraction"),
                ("gotta", "need to", "Replace informal contraction"),
                ("hey", "greetings", "Use formal salutation"),
                ("cool", "acceptable", "Use formal adjective"),
                ("awesome", "excellent", "Use formal adjective"),
                ("stuff", "materials", "Use precise noun"),
                ("ok", "understood", "Use formal acknowledgement"),
                ("yeah", "yes", "Use formal affirmative"),
                ("nah", "no", "Use formal negative"),
            },
            [(ToneCategory.Formal, ToneCategory.Casual)] = new()
            {
                ("furthermore", "also", "Simplify connector"),
                ("therefore", "so", "Simplify connector"),
                ("consequently", "so", "Simplify connector"),
                ("utilize", "use", "Simplify verb"),
                ("facilitate", "help", "Simplify verb"),
                ("endeavor", "try", "Simplify verb"),
                ("ascertain", "find out", "Simplify verb"),
                ("shall", "will", "Use everyday modal"),
                ("aforementioned", "that", "Simplify reference"),
            },
            [(ToneCategory.Assertive, ToneCategory.Polite)] = new()
            {
                ("must", "could you please", "Soften directive"),
                ("always", "ideally", "Soften absolute"),
                ("never", "preferably avoid", "Soften prohibition"),
                ("immediately", "when convenient", "Soften urgency"),
                ("demand", "request", "Soften verb"),
                ("require", "would appreciate", "Soften requirement"),
            },
            [(ToneCategory.Polite, ToneCategory.Assertive)] = new()
            {
                ("please", "", "Remove hedge"),
                ("kindly", "", "Remove hedge"),
                ("perhaps", "definitely", "Strengthen qualifier"),
                ("possibly", "certainly", "Strengthen qualifier"),
            },
        };

        /// <summary>
        /// Analyze the tone of a prompt.
        /// </summary>
        public static ToneAnalysisResult Analyze(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            return AnalyzeCore(prompt, targetTone: null);
        }

        /// <summary>
        /// Analyze tone and provide suggestions to shift toward a target tone.
        /// </summary>
        public static ToneAnalysisResult AnalyzeWithTarget(string prompt, ToneCategory targetTone)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            return AnalyzeCore(prompt, targetTone);
        }

        /// <summary>
        /// Get the dominant tone of a prompt as a quick check.
        /// </summary>
        public static ToneCategory GetDominantTone(string prompt)
        {
            return Analyze(prompt).DominantTone;
        }

        private static ToneAnalysisResult AnalyzeCore(string prompt, ToneCategory? targetTone)
        {
            var words = Regex.Split(prompt, @"\W+")
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            if (words.Count == 0)
                throw new ArgumentException("Prompt contains no analyzable words.", nameof(prompt));

            var scores = new Dictionary<ToneCategory, (int hits, List<string> evidence)>
            {
                [ToneCategory.Formal] = (0, new()),
                [ToneCategory.Casual] = (0, new()),
                [ToneCategory.Assertive] = (0, new()),
                [ToneCategory.Polite] = (0, new()),
                [ToneCategory.Technical] = (0, new()),
                [ToneCategory.Creative] = (0, new()),
            };

            foreach (var word in words)
            {
                CheckMarker(word, FormalMarkers, scores, ToneCategory.Formal);
                CheckMarker(word, CasualMarkers, scores, ToneCategory.Casual);
                CheckMarker(word, AssertiveMarkers, scores, ToneCategory.Assertive);
                CheckMarker(word, PoliteMarkers, scores, ToneCategory.Polite);
                CheckMarker(word, TechnicalMarkers, scores, ToneCategory.Technical);
                CheckMarker(word, CreativeMarkers, scores, ToneCategory.Creative);
            }

            CheckSentencePatterns(prompt, scores);

            int totalHits = scores.Values.Sum(s => s.hits);

            var toneScores = scores
                .Select(kv => new ToneScore
                {
                    Tone = kv.Key,
                    Confidence = totalHits > 0 ? Math.Round((double)kv.Value.hits / totalHits, 3) : 0,
                    Evidence = kv.Value.evidence.Distinct().Take(5).ToList()
                })
                .OrderByDescending(ts => ts.Confidence)
                .ToList();

            ToneCategory dominant;
            double dominantConfidence;
            if (totalHits == 0)
            {
                dominant = ToneCategory.Neutral;
                dominantConfidence = 1.0;
                toneScores.Insert(0, new ToneScore { Tone = ToneCategory.Neutral, Confidence = 1.0 });
            }
            else
            {
                dominant = toneScores[0].Tone;
                dominantConfidence = toneScores[0].Confidence;
            }

            bool isConsistent = true;
            string? consistencyNote = null;
            if (toneScores.Count >= 2 && toneScores[1].Confidence > 0)
            {
                double gap = toneScores[0].Confidence - toneScores[1].Confidence;
                if (gap < 0.15 && toneScores[1].Confidence >= 0.25)
                {
                    isConsistent = false;
                    consistencyNote = $"Mixed tone detected: {toneScores[0].Tone} ({toneScores[0].Confidence:P0}) " +
                                      $"and {toneScores[1].Tone} ({toneScores[1].Confidence:P0}) are close. " +
                                      "Consider unifying tone for clarity.";
                }
            }

            var suggestions = new List<ToneSuggestion>();
            if (targetTone.HasValue && targetTone.Value != dominant)
            {
                suggestions = GenerateSuggestions(prompt, dominant, targetTone.Value);
            }

            string summary = targetTone.HasValue
                ? $"Dominant tone: {dominant} ({dominantConfidence:P0}). Target: {targetTone.Value}. " +
                  $"{suggestions.Count} suggestion(s) to shift tone."
                : $"Dominant tone: {dominant} ({dominantConfidence:P0}).{(isConsistent ? "" : " " + consistencyNote)}";

            return new ToneAnalysisResult
            {
                DominantTone = dominant,
                DominantConfidence = dominantConfidence,
                Scores = toneScores,
                IsConsistent = isConsistent,
                ConsistencyNote = consistencyNote,
                Suggestions = suggestions,
                Summary = summary
            };
        }

        private static void CheckMarker(string word, HashSet<string> markers,
            Dictionary<ToneCategory, (int hits, List<string> evidence)> scores, ToneCategory tone)
        {
            if (markers.Contains(word))
            {
                var entry = scores[tone];
                entry.hits++;
                entry.evidence.Add(word.ToLowerInvariant());
                scores[tone] = entry;
            }
        }

        private static void CheckSentencePatterns(string prompt,
            Dictionary<ToneCategory, (int hits, List<string> evidence)> scores)
        {
            var sentences = Regex.Split(prompt, @"(?<=[.!?])\s+");
            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.EndsWith("!"))
                {
                    var entry = scores[ToneCategory.Creative];
                    entry.hits++;
                    entry.evidence.Add("exclamation mark");
                    scores[ToneCategory.Creative] = entry;
                }

                if (trimmed.EndsWith("?") &&
                    Regex.IsMatch(trimmed, @"\b(could|would|might|can)\b", RegexOptions.IgnoreCase))
                {
                    var entry = scores[ToneCategory.Polite];
                    entry.hits++;
                    entry.evidence.Add("hedged question");
                    scores[ToneCategory.Polite] = entry;
                }

                var capsWords = Regex.Matches(trimmed, @"\b[A-Z]{3,}\b");
                if (capsWords.Count > 0)
                {
                    var entry = scores[ToneCategory.Assertive];
                    entry.hits += capsWords.Count;
                    entry.evidence.Add("ALL CAPS emphasis");
                    scores[ToneCategory.Assertive] = entry;
                }
            }
        }

        private static List<ToneSuggestion> GenerateSuggestions(string prompt,
            ToneCategory currentTone, ToneCategory targetTone)
        {
            var suggestions = new List<ToneSuggestion>();

            var key = (currentTone, targetTone);
            if (ShiftRules.TryGetValue(key, out var rules))
            {
                foreach (var (pattern, replacement, reason) in rules)
                {
                    if (Regex.IsMatch(prompt, $@"\b{Regex.Escape(pattern)}\b", RegexOptions.IgnoreCase))
                    {
                        suggestions.Add(new ToneSuggestion
                        {
                            Original = pattern,
                            Suggested = replacement,
                            Reason = reason
                        });
                    }
                }
            }

            if (suggestions.Count == 0)
            {
                suggestions.Add(new ToneSuggestion
                {
                    Original = "(general)",
                    Suggested = $"Rewrite with a more {targetTone.ToString().ToLowerInvariant()} tone",
                    Reason = $"No specific word-level shifts found from {currentTone} to {targetTone}. " +
                             "Consider restructuring sentences to match the target tone."
                });
            }

            return suggestions;
        }
    }
}
