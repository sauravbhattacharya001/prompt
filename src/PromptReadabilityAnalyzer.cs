namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Readability grade level assessment.
    /// </summary>
    public enum ReadabilityGrade
    {
        /// <summary>Very easy to read (grade 1-5).</summary>
        VeryEasy,
        /// <summary>Easy to read (grade 6-8).</summary>
        Easy,
        /// <summary>Moderate difficulty (grade 9-12).</summary>
        Moderate,
        /// <summary>Difficult to read (grade 13-16, college level).</summary>
        Difficult,
        /// <summary>Very difficult (grade 16+, graduate level).</summary>
        VeryDifficult
    }

    /// <summary>
    /// Individual readability metric result.
    /// </summary>
    public class ReadabilityMetric
    {
        /// <summary>Gets the metric name.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the raw score value.</summary>
        public double Value { get; internal set; }

        /// <summary>Gets a human-readable interpretation.</summary>
        public string Interpretation { get; internal set; } = "";
    }

    /// <summary>
    /// Sentence-level analysis detail.
    /// </summary>
    public class SentenceAnalysis
    {
        /// <summary>Gets the sentence text (truncated if long).</summary>
        public string Text { get; internal set; } = "";

        /// <summary>Gets the word count.</summary>
        public int WordCount { get; internal set; }

        /// <summary>Gets the syllable count.</summary>
        public int SyllableCount { get; internal set; }

        /// <summary>Gets whether this sentence is flagged as too complex.</summary>
        public bool IsFlagged { get; internal set; }

        /// <summary>Gets the reason for flagging, if any.</summary>
        public string? FlagReason { get; internal set; }
    }

    /// <summary>
    /// Full readability analysis result.
    /// </summary>
    public class ReadabilityReport
    {
        /// <summary>Gets the overall readability grade.</summary>
        public ReadabilityGrade Grade { get; internal set; }

        /// <summary>Gets the Flesch-Kincaid grade level (approximate US school grade).</summary>
        public double FleschKincaidGradeLevel { get; internal set; }

        /// <summary>Gets the Flesch reading ease score (0-100, higher = easier).</summary>
        public double FleschReadingEase { get; internal set; }

        /// <summary>Gets the Coleman-Liau index.</summary>
        public double ColemanLiauIndex { get; internal set; }

        /// <summary>Gets the automated readability index (ARI).</summary>
        public double AutomatedReadabilityIndex { get; internal set; }

        /// <summary>Gets individual metric breakdowns.</summary>
        public List<ReadabilityMetric> Metrics { get; internal set; } = new();

        /// <summary>Gets per-sentence analysis.</summary>
        public List<SentenceAnalysis> Sentences { get; internal set; } = new();

        /// <summary>Gets vocabulary diversity (type-token ratio).</summary>
        public double VocabularyDiversity { get; internal set; }

        /// <summary>Gets average word length in characters.</summary>
        public double AverageWordLength { get; internal set; }

        /// <summary>Gets average sentence length in words.</summary>
        public double AverageSentenceLength { get; internal set; }

        /// <summary>Gets the total word count.</summary>
        public int TotalWords { get; internal set; }

        /// <summary>Gets the total sentence count.</summary>
        public int TotalSentences { get; internal set; }

        /// <summary>Gets actionable suggestions to improve readability.</summary>
        public List<string> Suggestions { get; internal set; } = new();

        /// <summary>Gets a one-line summary.</summary>
        public string Summary { get; internal set; } = "";
    }

    /// <summary>
    /// Analyzes prompt text readability using multiple readability formulas.
    /// Provides Flesch-Kincaid, Flesch Reading Ease, Coleman-Liau, and ARI scores,
    /// plus vocabulary diversity, sentence complexity analysis, and improvement suggestions.
    /// </summary>
    /// <example>
    /// <code>
    /// var analyzer = new PromptReadabilityAnalyzer();
    /// var report = analyzer.Analyze("You are a helpful assistant. Answer questions clearly and concisely.");
    /// Console.WriteLine($"Grade: {report.Grade}, FK Level: {report.FleschKincaidGradeLevel:F1}");
    /// foreach (var s in report.Suggestions) Console.WriteLine($"  - {s}");
    ///
    /// // Compare two prompt variants
    /// var comparison = analyzer.Compare(promptA, promptB);
    /// Console.WriteLine($"Easier prompt: {comparison.EasierPrompt}");
    /// </code>
    /// </example>
    public class PromptReadabilityAnalyzer
    {
        private static readonly Regex SentenceSplitter = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
        private static readonly Regex WordSplitter = new(@"\b[a-zA-Z']+\b", RegexOptions.Compiled);
        private static readonly Regex VowelGroup = new(@"[aeiouy]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> ComplexMarkers = new(StringComparer.OrdinalIgnoreCase)
        {
            "however", "furthermore", "nevertheless", "notwithstanding", "consequently",
            "accordingly", "subsequently", "alternatively", "simultaneously", "respectively"
        };

        /// <summary>
        /// Maximum recommended average sentence length for prompts (words).
        /// </summary>
        public int MaxRecommendedSentenceLength { get; set; } = 25;

        /// <summary>
        /// Maximum recommended word length flagging threshold (characters).
        /// </summary>
        public int LongWordThreshold { get; set; } = 12;

        /// <summary>
        /// Analyze the readability of a prompt string.
        /// </summary>
        /// <param name="text">The prompt text to analyze.</param>
        /// <returns>A <see cref="ReadabilityReport"/> with scores, metrics, and suggestions.</returns>
        /// <exception cref="ArgumentException">If text is null or empty.</exception>
        public ReadabilityReport Analyze(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));

            var sentences = SplitSentences(text);
            var allWords = new List<string>();
            var sentenceAnalyses = new List<SentenceAnalysis>();
            int totalSyllables = 0;
            int totalChars = 0;

            foreach (var sentence in sentences)
            {
                var words = ExtractWords(sentence);
                if (words.Count == 0) continue;

                int sentSyllables = words.Sum(CountSyllables);
                totalSyllables += sentSyllables;
                totalChars += words.Sum(w => w.Length);
                allWords.AddRange(words);

                string? flagReason = null;
                bool flagged = false;

                if (words.Count > MaxRecommendedSentenceLength)
                {
                    flagged = true;
                    flagReason = $"Too long ({words.Count} words, recommend ≤{MaxRecommendedSentenceLength})";
                }
                else if (words.Any(w => ComplexMarkers.Contains(w)))
                {
                    flagged = true;
                    var marker = words.First(w => ComplexMarkers.Contains(w));
                    flagReason = $"Contains complex transition word: \"{marker}\"";
                }

                var displayText = sentence.Length > 80 ? sentence[..77] + "..." : sentence;
                sentenceAnalyses.Add(new SentenceAnalysis
                {
                    Text = displayText,
                    WordCount = words.Count,
                    SyllableCount = sentSyllables,
                    IsFlagged = flagged,
                    FlagReason = flagReason
                });
            }

            int totalWords = allWords.Count;
            int totalSentenceCount = sentenceAnalyses.Count;

            if (totalWords == 0 || totalSentenceCount == 0)
            {
                return new ReadabilityReport
                {
                    Grade = ReadabilityGrade.VeryEasy,
                    Summary = "Text too short to analyze meaningfully.",
                    Suggestions = { "Add more content to enable readability analysis." }
                };
            }

            double avgSentenceLen = (double)totalWords / totalSentenceCount;
            double avgSyllablesPerWord = (double)totalSyllables / totalWords;
            double avgWordLen = (double)totalChars / totalWords;

            // Flesch-Kincaid Grade Level
            double fkGrade = 0.39 * avgSentenceLen + 11.8 * avgSyllablesPerWord - 15.59;
            fkGrade = Math.Max(0, Math.Round(fkGrade, 1));

            // Flesch Reading Ease
            double fre = 206.835 - 1.015 * avgSentenceLen - 84.6 * avgSyllablesPerWord;
            fre = Math.Clamp(Math.Round(fre, 1), 0, 100);

            // Coleman-Liau Index
            double L = (double)totalChars / totalWords * 100;
            double S = (double)totalSentenceCount / totalWords * 100;
            double cli = 0.0588 * L - 0.296 * S - 15.8;
            cli = Math.Max(0, Math.Round(cli, 1));

            // Automated Readability Index
            double ari = 4.71 * ((double)totalChars / totalWords) + 0.5 * ((double)totalWords / totalSentenceCount) - 21.43;
            ari = Math.Max(0, Math.Round(ari, 1));

            // Vocabulary diversity (type-token ratio)
            var uniqueWords = new HashSet<string>(allWords.Select(w => w.ToLowerInvariant()));
            double ttr = (double)uniqueWords.Count / totalWords;

            // Determine grade
            var grade = fkGrade switch
            {
                <= 5 => ReadabilityGrade.VeryEasy,
                <= 8 => ReadabilityGrade.Easy,
                <= 12 => ReadabilityGrade.Moderate,
                <= 16 => ReadabilityGrade.Difficult,
                _ => ReadabilityGrade.VeryDifficult
            };

            // Build metrics
            var metrics = new List<ReadabilityMetric>
            {
                new() { Name = "Flesch-Kincaid Grade Level", Value = fkGrade, Interpretation = InterpretFKGrade(fkGrade) },
                new() { Name = "Flesch Reading Ease", Value = fre, Interpretation = InterpretFRE(fre) },
                new() { Name = "Coleman-Liau Index", Value = cli, Interpretation = $"Grade level {cli:F1}" },
                new() { Name = "Automated Readability Index", Value = ari, Interpretation = $"Grade level {ari:F1}" },
                new() { Name = "Vocabulary Diversity (TTR)", Value = Math.Round(ttr, 3), Interpretation = ttr > 0.7 ? "High variety" : ttr > 0.4 ? "Moderate variety" : "Repetitive vocabulary" }
            };

            // Generate suggestions
            var suggestions = GenerateSuggestions(avgSentenceLen, avgSyllablesPerWord, avgWordLen, ttr, sentenceAnalyses, allWords);

            string summary = $"{grade} readability (FK grade {fkGrade:F1}, reading ease {fre:F0}/100) — {totalWords} words, {totalSentenceCount} sentences";

            return new ReadabilityReport
            {
                Grade = grade,
                FleschKincaidGradeLevel = fkGrade,
                FleschReadingEase = fre,
                ColemanLiauIndex = cli,
                AutomatedReadabilityIndex = ari,
                Metrics = metrics,
                Sentences = sentenceAnalyses,
                VocabularyDiversity = Math.Round(ttr, 3),
                AverageWordLength = Math.Round(avgWordLen, 1),
                AverageSentenceLength = Math.Round(avgSentenceLen, 1),
                TotalWords = totalWords,
                TotalSentences = totalSentenceCount,
                Suggestions = suggestions,
                Summary = summary
            };
        }

        /// <summary>
        /// Compare readability of two prompt variants.
        /// </summary>
        /// <param name="textA">First prompt variant.</param>
        /// <param name="textB">Second prompt variant.</param>
        /// <returns>A comparison result identifying which is more readable.</returns>
        public ReadabilityComparison Compare(string textA, string textB)
        {
            var reportA = Analyze(textA);
            var reportB = Analyze(textB);

            string easier = reportA.FleschReadingEase >= reportB.FleschReadingEase ? "A" : "B";
            double diff = Math.Abs(reportA.FleschReadingEase - reportB.FleschReadingEase);

            var differences = new List<string>();
            if (Math.Abs(reportA.AverageSentenceLength - reportB.AverageSentenceLength) > 3)
                differences.Add($"Sentence length differs: A={reportA.AverageSentenceLength:F1}, B={reportB.AverageSentenceLength:F1}");
            if (Math.Abs(reportA.VocabularyDiversity - reportB.VocabularyDiversity) > 0.1)
                differences.Add($"Vocabulary diversity differs: A={reportA.VocabularyDiversity:F3}, B={reportB.VocabularyDiversity:F3}");
            if (Math.Abs(reportA.FleschKincaidGradeLevel - reportB.FleschKincaidGradeLevel) > 1)
                differences.Add($"Grade level differs: A={reportA.FleschKincaidGradeLevel:F1}, B={reportB.FleschKincaidGradeLevel:F1}");

            return new ReadabilityComparison
            {
                ReportA = reportA,
                ReportB = reportB,
                EasierPrompt = easier,
                ReadingEaseDifference = Math.Round(diff, 1),
                KeyDifferences = differences,
                Summary = diff < 5 ? "Both prompts have similar readability."
                    : $"Prompt {easier} is significantly more readable (ease diff: {diff:F1})."
            };
        }

        private static List<string> SplitSentences(string text)
        {
            // Split on sentence-ending punctuation, but also treat newlines as sentence breaks
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var parts = new List<string>();
            foreach (var line in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var lineSentences = SentenceSplitter.Split(line.Trim());
                parts.AddRange(lineSentences.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            // If no sentence breaks found, treat whole text as one sentence
            if (parts.Count == 0 && !string.IsNullOrWhiteSpace(text))
                parts.Add(text.Trim());
            return parts;
        }

        private static List<string> ExtractWords(string text)
        {
            return WordSplitter.Matches(text).Select(m => m.Value).ToList();
        }

        /// <summary>
        /// Estimate syllable count for an English word.
        /// </summary>
        internal static int CountSyllables(string word)
        {
            if (string.IsNullOrEmpty(word)) return 0;
            word = word.ToLowerInvariant();
            if (word.Length <= 2) return 1;

            // Count vowel groups
            int count = VowelGroup.Matches(word).Count;

            // Subtract silent 'e' at end
            if (word.EndsWith("e") && !word.EndsWith("le") && count > 1)
                count--;

            // Common suffixes that add syllables
            if (word.EndsWith("tion") || word.EndsWith("sion"))
                count = Math.Max(count, 2);

            return Math.Max(1, count);
        }

        private List<string> GenerateSuggestions(double avgSentLen, double avgSyllPerWord,
            double avgWordLen, double ttr, List<SentenceAnalysis> sentences, List<string> words)
        {
            var suggestions = new List<string>();

            if (avgSentLen > MaxRecommendedSentenceLength)
                suggestions.Add($"Shorten sentences: average is {avgSentLen:F1} words (recommend ≤{MaxRecommendedSentenceLength}).");

            if (avgSyllPerWord > 1.8)
                suggestions.Add("Simplify vocabulary: high average syllable count suggests complex words. Prefer shorter, common words.");

            if (avgWordLen > 6.5)
                suggestions.Add($"Average word length is {avgWordLen:F1} chars — consider using shorter, simpler words where possible.");

            int longWords = words.Count(w => w.Length >= LongWordThreshold);
            if (longWords > 3)
                suggestions.Add($"Found {longWords} words with {LongWordThreshold}+ characters. Consider replacing with simpler alternatives.");

            if (ttr < 0.3)
                suggestions.Add("Vocabulary is very repetitive. Vary word choice or consolidate repeated instructions.");

            int flaggedCount = sentences.Count(s => s.IsFlagged);
            if (flaggedCount > 0)
                suggestions.Add($"{flaggedCount} sentence(s) flagged for complexity — consider splitting or simplifying them.");

            if (sentences.Count > 0 && sentences.Count < 2 && words.Count > 40)
                suggestions.Add("Consider breaking the prompt into multiple sentences for clarity.");

            if (suggestions.Count == 0)
                suggestions.Add("Readability looks good! No major issues detected.");

            return suggestions;
        }

        private static string InterpretFKGrade(double grade) => grade switch
        {
            <= 5 => "Elementary school level — very easy",
            <= 8 => "Middle school level — easy to read",
            <= 12 => "High school level — moderate difficulty",
            <= 16 => "College level — difficult",
            _ => "Graduate level — very difficult"
        };

        private static string InterpretFRE(double score) => score switch
        {
            >= 80 => "Very easy to read",
            >= 60 => "Standard / easily understood",
            >= 40 => "Somewhat difficult",
            >= 20 => "Difficult to read",
            _ => "Very difficult to read"
        };
    }

    /// <summary>
    /// Result of comparing two prompt variants for readability.
    /// </summary>
    public class ReadabilityComparison
    {
        /// <summary>Gets the readability report for prompt A.</summary>
        public ReadabilityReport ReportA { get; internal set; } = new();

        /// <summary>Gets the readability report for prompt B.</summary>
        public ReadabilityReport ReportB { get; internal set; } = new();

        /// <summary>Gets which prompt is easier to read ("A" or "B").</summary>
        public string EasierPrompt { get; internal set; } = "";

        /// <summary>Gets the Flesch Reading Ease score difference.</summary>
        public double ReadingEaseDifference { get; internal set; }

        /// <summary>Gets key differences between the two prompts.</summary>
        public List<string> KeyDifferences { get; internal set; } = new();

        /// <summary>Gets a summary of the comparison.</summary>
        public string Summary { get; internal set; } = "";
    }
}
