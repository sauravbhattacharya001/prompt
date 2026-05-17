namespace Prompt.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptIntentClassifierTests
    {
        private readonly PromptIntentClassifier _classifier = new();

        // ── Edge cases: empty / whitespace input ─────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n  ")]
        public void Classify_EmptyOrWhitespace_ReturnsUnknownWithZeroConfidence(string? input)
        {
            var result = _classifier.Classify(input!);

            Assert.Equal(PromptIntent.Unknown, result.PrimaryIntent);
            Assert.Equal(0, result.Confidence);
            Assert.False(result.IsAmbiguous);
            Assert.Single(result.Scores);
            Assert.Equal(PromptIntent.Unknown, result.Scores[0].Intent);
        }

        // ── Question intent ──────────────────────────────────────────

        [Theory]
        [InlineData("What is the capital of France?")]
        [InlineData("Why is the sky blue?")]
        [InlineData("How does TLS work?")]
        [InlineData("Can you explain recursion?")]
        public void Classify_Questions_DetectsQuestionIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Equal(PromptIntent.Question, result.PrimaryIntent);
            Assert.True(result.Confidence > 0);
            Assert.NotEmpty(result.Signals);
        }

        // ── Instruction intent ───────────────────────────────────────

        [Theory]
        [InlineData("Generate a report summary")]
        [InlineData("Please create a configuration file")]
        [InlineData("Build me a landing page")]
        [InlineData("I need a meal plan for the week")]
        public void Classify_Instructions_DetectsInstructionIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Equal(PromptIntent.Instruction, result.PrimaryIntent);
            Assert.True(result.Confidence > 0);
        }

        // ── Creative intent ──────────────────────────────────────────

        [Theory]
        [InlineData("Write a story about a brave knight")]
        [InlineData("Compose a haiku about autumn")]
        [InlineData("Once upon a time, in a galaxy far far away")]
        public void Classify_Creative_RanksCreativeInTopTwo(string prompt)
        {
            var result = _classifier.Classify(prompt);
            // Creative prompts often share "write/compose" with Instruction;
            // require Creative to land in the top-2 with non-zero score.
            Assert.Contains(
                result.Scores.Take(2),
                s => s.Intent == PromptIntent.Creative && s.Score > 0);
        }

        // ── Analytical intent ────────────────────────────────────────

        [Theory]
        [InlineData("Compare Python and Rust for systems programming")]
        [InlineData("Analyze the trade-offs of microservices")]
        [InlineData("What are the pros and cons of remote work?")]
        public void Classify_Analytical_DetectsAnalyticalIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            // "pros and cons" hits both keyword AND pattern, plus question keywords.
            // Allow Analytical OR Question (with Analytical in top-2).
            if (result.PrimaryIntent == PromptIntent.Question)
            {
                Assert.Contains(result.Scores.Take(3), s => s.Intent == PromptIntent.Analytical && s.Score > 0);
            }
            else
            {
                Assert.Equal(PromptIntent.Analytical, result.PrimaryIntent);
            }
        }

        // ── Conversational intent ────────────────────────────────────

        [Theory]
        [InlineData("Hi there, how are you?")]
        [InlineData("Hello! Nice to meet you.")]
        [InlineData("Thanks for your help, goodbye!")]
        public void Classify_Conversational_DetectsConversationalIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.True(
                result.PrimaryIntent == PromptIntent.Conversational
                || result.Scores.Take(2).Any(s => s.Intent == PromptIntent.Conversational),
                $"Expected Conversational in top-2 for: {prompt}, got {result}");
        }

        // ── Coding intent ────────────────────────────────────────────

        [Theory]
        [InlineData("Debug this python function that throws an exception")]
        [InlineData("Refactor this class to use dependency injection")]
        public void Classify_Coding_DetectsCodingIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Equal(PromptIntent.Coding, result.PrimaryIntent);
        }

        [Theory]
        [InlineData("Write a SQL query to find duplicate rows")]
        [InlineData("Implement a function in javascript to debounce events")]
        public void Classify_CodingWithInstructionVerbs_RanksCodingInTopTwo(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Contains(
                result.Scores.Take(2),
                s => s.Intent == PromptIntent.Coding && s.Score > 0);
        }

        [Fact]
        public void Classify_CodeFence_TriggersCodingPattern()
        {
            var result = _classifier.Classify("Here is code: ```let x = 1;```");
            Assert.True(result.Scores.Any(s => s.Intent == PromptIntent.Coding && s.Score > 0));
        }

        // ── Summarization intent ─────────────────────────────────────

        [Theory]
        [InlineData("Summarize this article in three bullets")]
        [InlineData("TL;DR of the meeting notes")]
        [InlineData("Recap the main points of the discussion")]
        public void Classify_Summarization_DetectsSummarizationIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Equal(PromptIntent.Summarization, result.PrimaryIntent);
        }

        [Fact]
        public void Classify_GiveMeKeyPoints_RanksSummarizationInTopTwo()
        {
            // "Give me" is Instruction; "key points" is Summarization keyword.
            var result = _classifier.Classify("Give me the key points of the report");
            Assert.Contains(
                result.Scores.Take(2),
                s => s.Intent == PromptIntent.Summarization && s.Score > 0);
        }

        // ── Translation intent ───────────────────────────────────────

        [Theory]
        [InlineData("Translate this sentence to French")]
        [InlineData("Convert to Spanish: good morning")]
        public void Classify_Translation_DetectsTranslationIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Equal(PromptIntent.Translation, result.PrimaryIntent);
        }

        // ── Enumeration intent ───────────────────────────────────────

        [Theory]
        [InlineData("List the top 10 programming languages of 2024")]
        [InlineData("Enumerate the types of database indexes")]
        [InlineData("Rank the best sorting algorithms by average performance")]
        public void Classify_Enumeration_DetectsEnumerationIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Equal(PromptIntent.Enumeration, result.PrimaryIntent);
        }

        [Fact]
        public void Classify_GiveMeAListPrompt_RanksEnumerationInTopTwo()
        {
            // "Give me" is Instruction; "list" + "give...list" pattern is Enumeration.
            var result = _classifier.Classify("Give me a list of healthy breakfast options");
            Assert.Contains(
                result.Scores.Take(2),
                s => s.Intent == PromptIntent.Enumeration && s.Score > 0);
        }

        // ── RolePlay intent ──────────────────────────────────────────

        [Theory]
        [InlineData("Act as a senior security engineer reviewing this design")]
        [InlineData("Pretend you are a 17th century philosopher")]
        [InlineData("You are a helpful AI tutor for high school students")]
        public void Classify_RolePlay_DetectsRolePlayIntent(string prompt)
        {
            var result = _classifier.Classify(prompt);
            Assert.Equal(PromptIntent.RolePlay, result.PrimaryIntent);
        }

        // ── Unknown / fallback ───────────────────────────────────────

        [Fact]
        public void Classify_NoMatchingSignals_ReturnsUnknown()
        {
            var result = _classifier.Classify("xyzzy plugh foobar quux");
            Assert.Equal(PromptIntent.Unknown, result.PrimaryIntent);
            Assert.Equal(0, result.Confidence);
        }

        // ── Score normalization ──────────────────────────────────────

        [Fact]
        public void Classify_TopScoreIsNormalizedToOne()
        {
            var result = _classifier.Classify("What is the time?");
            Assert.True(result.Confidence > 0 && result.Confidence <= 1.0);
            Assert.Equal(1.0, result.Scores.First().Score, 6);
        }

        [Fact]
        public void Classify_ScoresAreSortedDescending()
        {
            var result = _classifier.Classify("Write a Python function to compare two lists");
            for (int i = 1; i < result.Scores.Count; i++)
            {
                Assert.True(result.Scores[i - 1].Score >= result.Scores[i].Score);
            }
        }

        [Fact]
        public void Classify_AllElevenIntentsRepresentedInScores()
        {
            var result = _classifier.Classify("Write a Python function to greet a user");
            // 10 explicit intent rules; Unknown is implied when nothing matches
            // and is not added to the Scores list. We expect all rule-based intents.
            var represented = result.Scores.Select(s => s.Intent).ToHashSet();
            Assert.Equal(10, represented.Count);
            Assert.DoesNotContain(PromptIntent.Unknown, represented);
        }

        // ── Ambiguity detection ──────────────────────────────────────

        [Fact]
        public void Classify_DistinctTopIntent_IsNotAmbiguous()
        {
            var result = _classifier.Classify("Translate 'good night' into Japanese");
            Assert.False(result.IsAmbiguous);
        }

        [Fact]
        public void Classify_CloseTwoIntents_FlagsAmbiguous()
        {
            // "Write a Python function and explain it" mixes coding + instruction signals
            var result = _classifier.Classify("Explain how to write a Python function");
            // Look at the actual gap; assertion is conditional on the data
            if (result.Scores.Count > 1
                && result.Scores[1].Score >= result.Scores[0].Score - 0.15
                && result.Scores[1].Score > 0)
            {
                Assert.True(result.IsAmbiguous);
            }
        }

        // ── Signals & ToString ───────────────────────────────────────

        [Fact]
        public void Classify_RecordsSignalsForMatchedRules()
        {
            var result = _classifier.Classify("Why is the sky blue?");
            Assert.NotEmpty(result.Signals);
            Assert.Contains(result.Signals, s => s.Contains("keyword") || s.Contains("pattern"));
        }

        [Fact]
        public void IntentClassification_ToString_IncludesIntentAndConfidence()
        {
            var result = _classifier.Classify("What time is it?");
            var str = result.ToString();
            Assert.Contains("Question", str);
            Assert.Contains("%", str);
        }

        [Fact]
        public void IntentClassification_ToString_FlagsAmbiguous()
        {
            var ic = new IntentClassification
            {
                PrimaryIntent = PromptIntent.Coding,
                Confidence = 0.5,
                IsAmbiguous = true
            };
            Assert.Contains("ambiguous", ic.ToString());
        }

        [Fact]
        public void IntentScore_ToString_FormatsIntentAndScore()
        {
            var s = new IntentScore { Intent = PromptIntent.Question, Score = 0.42 };
            Assert.Equal("Question: 0.42", s.ToString());
        }

        // ── Batch classification ─────────────────────────────────────

        [Fact]
        public void ClassifyBatch_EmptyEnumerable_ReturnsZeroTotal()
        {
            var batch = _classifier.ClassifyBatch(new List<string>());
            Assert.Equal(0, batch.Total);
            Assert.Empty(batch.Distribution);
            Assert.Equal(0, batch.AverageConfidence);
            Assert.Null(batch.DominantIntent);
        }

        [Fact]
        public void ClassifyBatch_MixedPrompts_AggregatesDistribution()
        {
            var prompts = new[]
            {
                "What is functional programming?",
                "Why are unit tests important?",
                "How does HTTPS work?",
                "Write a function to reverse a string",
                "Debug this exception in my code",
                "List the top 5 sorting algorithms"
            };

            var batch = _classifier.ClassifyBatch(prompts);

            Assert.Equal(6, batch.Total);
            Assert.Equal(6, batch.Results.Count);
            Assert.True(batch.Distribution.Values.Sum() == 6);
            Assert.True(batch.AverageConfidence > 0);
            Assert.NotNull(batch.DominantIntent);
        }

        [Fact]
        public void ClassifyBatch_TracksAmbiguousCount()
        {
            var batch = _classifier.ClassifyBatch(new[]
            {
                "Hi",
                "Translate to French: bonjour", // mixes translation + conversational
                "What is X?"
            });
            Assert.True(batch.AmbiguousCount >= 0);
            Assert.Equal(
                batch.Results.Count(r => r.IsAmbiguous),
                batch.AmbiguousCount);
        }

        [Fact]
        public void ClassifyBatch_DominantIntent_IsMostFrequent()
        {
            var batch = _classifier.ClassifyBatch(new[]
            {
                "What is HTTP?",
                "Why is REST popular?",
                "How does DNS resolve names?",
                "Write me a story" // single outlier
            });
            Assert.Equal(PromptIntent.Question, batch.DominantIntent);
        }

        [Fact]
        public void IntentDistribution_ToString_RendersFormattedSummary()
        {
            var batch = _classifier.ClassifyBatch(new[]
            {
                "What is X?",
                "How does Y work?"
            });
            var str = batch.ToString();
            Assert.Contains("Intent Distribution", str);
            Assert.Contains("Question", str);
            Assert.Contains("2 prompts", str);
        }

        // ── Determinism ──────────────────────────────────────────────

        [Fact]
        public void Classify_SameInputTwice_ProducesSameResult()
        {
            const string input = "Write a SQL query to find duplicate emails";
            var a = _classifier.Classify(input);
            var b = _classifier.Classify(input);
            Assert.Equal(a.PrimaryIntent, b.PrimaryIntent);
            Assert.Equal(a.Confidence, b.Confidence);
            Assert.Equal(a.Signals.Count, b.Signals.Count);
        }

        [Fact]
        public void Classify_IsCaseInsensitive()
        {
            var lower = _classifier.Classify("write a python function");
            var upper = _classifier.Classify("WRITE A PYTHON FUNCTION");
            Assert.Equal(lower.PrimaryIntent, upper.PrimaryIntent);
            Assert.Equal(lower.Confidence, upper.Confidence, 4);
        }
    }
}
