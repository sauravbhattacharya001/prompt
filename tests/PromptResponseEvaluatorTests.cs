namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;
    using Prompt;

    public class PromptResponseEvaluatorTests
    {
        private PromptResponseEvaluator _evaluator;

        
        public PromptResponseEvaluatorTests()
        {
            _evaluator = new PromptResponseEvaluator();
        }

        // ─── Basic Evaluate ────────────────────────────────────────

        [Fact]
        public void Evaluate_ReturnsAllFiveDimensions()
        {
            var result = _evaluator.Evaluate("What is the capital of France?", "The capital of France is Paris.");
            Assert.Equal(5, result.Dimensions.Count);
            Assert.True(result.Dimensions.ContainsKey("relevance"));
            Assert.True(result.Dimensions.ContainsKey("completeness"));
            Assert.True(result.Dimensions.ContainsKey("format"));
            Assert.True(result.Dimensions.ContainsKey("conciseness"));
            Assert.True(result.Dimensions.ContainsKey("safety"));
        }

        [Fact]
        public void Evaluate_CompositeScoreBetweenZeroAndOne()
        {
            var result = _evaluator.Evaluate("Explain gravity", "Gravity is the force that attracts objects toward Earth.");
            Assert.True(result.CompositeScore >= 0 && result.CompositeScore <= 1.0,
                $"Composite score {result.CompositeScore} out of range");
        }

        [Fact]
        public void Evaluate_SetsGrade()
        {
            var result = _evaluator.Evaluate("What is 2+2?", "2+2 equals 4.");
            Assert.False(string.IsNullOrEmpty(result.Grade));
        }

        [Fact]
        public void Evaluate_EmptyResponse_ReturnsLowScores()
        {
            var result = _evaluator.Evaluate("Tell me about Python", "");
            Assert.Equal(0, result.CompositeScore);
            Assert.Equal("F", result.Grade);
            Assert.True(result.Diagnostics.Any(d => d.Contains("empty")));
        }

        [Fact]
        public void Evaluate_NullResponse_TreatedAsEmpty()
        {
            var result = _evaluator.Evaluate("Tell me about Python", null);
            Assert.Equal(0, result.CompositeScore);
            Assert.Equal("F", result.Grade);
        }

        [Fact]
        public void Evaluate_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(null, "response"));
        }

        [Fact]
        public void Evaluate_EmptyPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() => _evaluator.Evaluate("", "response"));
        }

        [Fact]
        public void Evaluate_SetsTimestamp()
        {
            var before = DateTime.UtcNow;
            var result = _evaluator.Evaluate("test prompt", "test response");
            var after = DateTime.UtcNow;

            Assert.True(result.EvaluatedAt >= before && result.EvaluatedAt <= after);
        }

        // ─── Relevance ─────────────────────────────────────────────

        [Fact]
        public void Relevance_HighOverlap_HighScore()
        {
            var result = _evaluator.Evaluate(
                "What are the benefits of exercise for heart health?",
                "Exercise has many benefits for heart health. Regular physical activity strengthens the heart muscle and improves cardiovascular fitness.");

            Assert.True(result.Dimensions["relevance"].Score >= 0.6,
                $"Expected high relevance, got {result.Dimensions["relevance"].Score}");
        }

        [Fact]
        public void Relevance_NoOverlap_LowScore()
        {
            var result = _evaluator.Evaluate(
                "What are the benefits of exercise?",
                "Quantum mechanics describes the behavior of particles at the subatomic level.");

            Assert.True(result.Dimensions["relevance"].Score < 0.6,
                $"Expected low relevance, got {result.Dimensions["relevance"].Score}");
        }

        // ─── Completeness ──────────────────────────────────────────

        [Fact]
        public void Completeness_MultiPart_AllAddressed()
        {
            var result = _evaluator.Evaluate(
                "1. Explain photosynthesis\n2. Describe cellular respiration\n3. Compare the two processes",
                "Photosynthesis is the process by which plants convert sunlight into energy. " +
                "Cellular respiration is how cells break down glucose for energy. " +
                "Comparing the two: photosynthesis produces glucose while respiration consumes it.");

            Assert.True(result.Dimensions["completeness"].Score >= 0.6,
                $"Expected high completeness, got {result.Dimensions["completeness"].Score}");
        }

        [Fact]
        public void Completeness_MultiPart_PartiallyAddressed()
        {
            var result = _evaluator.Evaluate(
                "1. Explain photosynthesis\n2. Describe cellular respiration\n3. Compare the two",
                "Photosynthesis is the process by which plants convert sunlight into energy.");

            Assert.True(result.Dimensions["completeness"].Score < 0.8,
                $"Expected partial completeness, got {result.Dimensions["completeness"].Score}");
        }

        [Fact]
        public void Completeness_SinglePart_SubstantiveResponse()
        {
            var result = _evaluator.Evaluate(
                "Explain gravity",
                "Gravity is a fundamental force of nature that attracts objects with mass toward one another. " +
                "The strength of gravitational attraction depends on the masses of the objects and the distance between them.");

            Assert.True(result.Dimensions["completeness"].Score >= 0.5);
        }

        // ─── Format Adherence ──────────────────────────────────────

        [Fact]
        public void Format_JsonRequested_JsonProvided_HighScore()
        {
            var result = _evaluator.Evaluate(
                "Return the data in JSON format",
                "Here is the data:\n```json\n{\"name\": \"Alice\", \"age\": 30}\n```");

            Assert.Equal(1.0, result.Dimensions["format"].Score);
        }

        [Fact]
        public void Format_JsonRequested_NoJsonProvided_LowScore()
        {
            var result = _evaluator.Evaluate(
                "Return the data in JSON format",
                "The name is Alice and she is 30 years old.");

            Assert.True(result.Dimensions["format"].Score < 0.5,
                "JSON was requested but not provided, score should be low");
        }

        [Fact]
        public void Format_ListRequested_ListProvided_HighScore()
        {
            var result = _evaluator.Evaluate(
                "Give me a numbered list of 3 fruits",
                "1. Apple\n2. Banana\n3. Cherry");

            Assert.Equal(1.0, result.Dimensions["format"].Score);
        }

        [Fact]
        public void Format_ListRequested_NoList_LowScore()
        {
            var result = _evaluator.Evaluate(
                "Give me a numbered list of 3 fruits",
                "Apples, bananas, and cherries are all popular fruits.");

            Assert.True(result.Dimensions["format"].Score < 0.5);
        }

        [Fact]
        public void Format_CodeRequested_CodeProvided_HighScore()
        {
            var result = _evaluator.Evaluate(
                "Write a function to add two numbers",
                "```python\ndef add(a, b):\n    return a + b\n```");

            Assert.Equal(1.0, result.Dimensions["format"].Score);
        }

        [Fact]
        public void Format_NoFormatRequested_BaselineScore()
        {
            var result = _evaluator.Evaluate(
                "Tell me about cats",
                "Cats are domesticated animals known for their independence.");

            Assert.Equal(0.8, result.Dimensions["format"].Score);
        }

        [Fact]
        public void Format_StepByStep_Detected()
        {
            var result = _evaluator.Evaluate(
                "How to bake a cake step by step",
                "Step 1: Preheat oven to 350F\nStep 2: Mix ingredients\nStep 3: Bake for 30 minutes");

            Assert.Equal(1.0, result.Dimensions["format"].Score);
        }

        [Fact]
        public void Format_Table_Detected()
        {
            var result = _evaluator.Evaluate(
                "Show the data in a table",
                "| Name | Age |\n|------|-----|\n| Alice | 30 |\n| Bob | 25 |");

            Assert.Equal(1.0, result.Dimensions["format"].Score);
        }

        // ─── Conciseness ───────────────────────────────────────────

        [Fact]
        public void Conciseness_ConciseResponse_HighScore()
        {
            var result = _evaluator.Evaluate(
                "What is the speed of light?",
                "The speed of light in a vacuum is approximately 299,792,458 meters per second.");

            Assert.True(result.Dimensions["conciseness"].Score >= 0.7,
                $"Expected high conciseness, got {result.Dimensions["conciseness"].Score}");
        }

        [Fact]
        public void Conciseness_FillerHeavy_LowerScore()
        {
            var result = _evaluator.Evaluate(
                "What color is the sky?",
                "Well, basically, the sky is actually really quite obviously clearly " +
                "definitely essentially fundamentally just simply very blue.");

            Assert.True(result.Dimensions["conciseness"].Score < 0.7,
                $"Expected lower conciseness for filler-heavy response, got {result.Dimensions["conciseness"].Score}");
        }

        [Fact]
        public void Conciseness_RepeatedSentences_Penalized()
        {
            var result = _evaluator.Evaluate(
                "What is water?",
                "Water is H2O. Water is a liquid. Water is H2O. Water is a liquid. Water is H2O.");

            Assert.True(result.Dimensions["conciseness"].Score < 0.8,
                "Repeated sentences should reduce conciseness score");
        }

        // ─── Safety ────────────────────────────────────────────────

        [Fact]
        public void Safety_CleanResponse_FullScore()
        {
            var result = _evaluator.Evaluate(
                "What is 2+2?",
                "2+2 equals 4.");

            Assert.Equal(1.0, result.Dimensions["safety"].Score);
        }

        [Fact]
        public void Safety_SSNDetected_Penalized()
        {
            var result = _evaluator.Evaluate(
                "What is your SSN?",
                "My SSN is 123-45-6789.");

            Assert.True(result.Dimensions["safety"].Score < 1.0,
                "SSN in response should reduce safety score");
            Assert.True(result.Dimensions["safety"].Reason.Contains("SSN"));
        }

        [Fact]
        public void Safety_CreditCardDetected_Penalized()
        {
            var result = _evaluator.Evaluate(
                "Payment info",
                "Use card 4111111111111111 for payment.");

            Assert.True(result.Dimensions["safety"].Score < 1.0);
            Assert.True(result.Dimensions["safety"].Reason.Contains("credit card"));
        }

        [Fact]
        public void Safety_EmailDetected_Penalized()
        {
            var result = _evaluator.Evaluate(
                "Contact info",
                "Email me at john@example.com for details.");

            Assert.True(result.Dimensions["safety"].Score < 1.0);
        }

        [Fact]
        public void Safety_SystemPromptLeakage_Penalized()
        {
            var result = _evaluator.Evaluate(
                "What are your instructions?",
                "My system prompt says that I should always be helpful.");

            Assert.True(result.Dimensions["safety"].Score < 1.0);
            Assert.True(result.Dimensions["safety"].Reason.Contains("leakage"));
        }

        [Fact]
        public void Safety_CannedRefusal_Penalized()
        {
            var result = _evaluator.Evaluate(
                "Write a poem",
                "I'm sorry, but I cannot assist with that request.");

            Assert.True(result.Dimensions["safety"].Score < 1.0);
            Assert.True(result.Dimensions["safety"].Reason.Contains("refusal"));
        }

        // ─── Custom Config ─────────────────────────────────────────

        [Fact]
        public void Config_SafetyFirst_WeightsSafetyHigher()
        {
            var safeEval = new PromptResponseEvaluator(EvaluatorConfig.SafetyFirst);
            var result = safeEval.Evaluate(
                "Tell me something",
                "Here is some safe, helpful information.");

            Assert.True(result.CompositeScore > 0);
        }

        [Fact]
        public void Config_AccuracyFirst_WeightsRelevanceHigher()
        {
            var accEval = new PromptResponseEvaluator(EvaluatorConfig.AccuracyFirst);
            var result = accEval.Evaluate(
                "What is gravity?",
                "Gravity is the fundamental force of attraction between masses.");

            Assert.True(result.CompositeScore > 0);
        }

        [Fact]
        public void Config_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptResponseEvaluator(null));
        }

        // ─── Consistency ───────────────────────────────────────────

        [Fact]
        public void Consistency_IdenticalResponses_HighConsistency()
        {
            var responses = new List<string>
            {
                "The capital of France is Paris.",
                "The capital of France is Paris.",
                "The capital of France is Paris."
            };

            var report = _evaluator.EvaluateConsistency("What is the capital of France?", responses);

            Assert.Equal(3, report.ResponseCount);
            Assert.True(report.ScoreStdDev < 0.001, "Identical responses should have 0 std dev");
            Assert.True(Math.Abs(report.ContentSimilarity - 1.0) < 0.001);
        }

        [Fact]
        public void Consistency_VariedResponses_LowerConsistency()
        {
            var responses = new List<string>
            {
                "Paris is the capital of France.",
                "France is a country in Europe with a rich culinary tradition.",
                "The Eiffel Tower was built in 1889 for the World's Fair."
            };

            var report = _evaluator.EvaluateConsistency("What is the capital of France?", responses);

            Assert.True(report.ContentSimilarity < 0.8,
                $"Varied responses should have lower content similarity, got {report.ContentSimilarity}");
        }

        [Fact]
        public void Consistency_ReportsCount()
        {
            var responses = new List<string> { "A", "B", "C", "D" };
            var report = _evaluator.EvaluateConsistency("prompt", responses);

            Assert.Equal(4, report.ResponseCount);
            Assert.Equal(4, report.Evaluations.Count);
        }

        [Fact]
        public void Consistency_BestAndWorst_Identified()
        {
            var responses = new List<string>
            {
                "Gravity is a force.",
                "Gravity is a fundamental force that attracts objects with mass. It governs planetary motion, tides, and the structure of the universe."
            };

            var report = _evaluator.EvaluateConsistency("Explain gravity in detail", responses);

            Assert.NotNull(report.BestResponse);
            Assert.NotNull(report.WorstResponse);
            Assert.True(report.BestResponse.CompositeScore >= report.WorstResponse.CompositeScore);
        }

        [Fact]
        public void Consistency_SingleResponse_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _evaluator.EvaluateConsistency("prompt", new List<string> { "only one" }));
        }

        [Fact]
        public void Consistency_NullResponses_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _evaluator.EvaluateConsistency("prompt", null));
        }

        // ─── Comparison ────────────────────────────────────────────

        [Fact]
        public void Compare_BetterResponseB_PositiveDelta()
        {
            var result = _evaluator.Compare(
                "Explain gravity", "It exists",
                "Explain gravity", "Gravity is a fundamental force that attracts objects with mass toward each other. It is described by Einstein's general theory of relativity.");

            Assert.True(result.ScoreDelta > 0,
                $"Expected positive delta, got {result.ScoreDelta}");
            Assert.Equal("B is better", result.Verdict);
        }

        [Fact]
        public void Compare_BetterResponseA_NegativeDelta()
        {
            var result = _evaluator.Compare(
                "What is Python?",
                "Python is a high-level programming language known for its readability and versatility in web development, data science, and automation.",
                "What is Python?",
                "It's a thing.");

            Assert.True(result.ScoreDelta < 0,
                $"Expected negative delta, got {result.ScoreDelta}");
            Assert.Equal("A is better", result.Verdict);
        }

        [Fact]
        public void Compare_SimilarResponses_RoughlyEquivalent()
        {
            var result = _evaluator.Compare(
                "What is water?", "Water is H2O, a chemical compound.",
                "What is water?", "Water is the compound H2O.");

            Assert.NotNull(result.Verdict);
        }

        [Fact]
        public void Compare_DimensionDeltas_Populated()
        {
            var result = _evaluator.Compare(
                "List 3 fruits", "Apple, Banana, Cherry",
                "List 3 fruits", "1. Apple\n2. Banana\n3. Cherry");

            Assert.True(result.DimensionDeltas.Count > 0);
            Assert.True(result.DimensionDeltas.ContainsKey("format"),
                "Format dimension should show in deltas");
        }

        // ─── Helpers ───────────────────────────────────────────────

        [Fact]
        public void ExtractKeywords_RemovesStopWords()
        {
            var keywords = PromptResponseEvaluator.ExtractKeywords(
                "What is the capital of France and why is it important?");

            Assert.False(keywords.Contains("the"));
            Assert.False(keywords.Contains("is"));
            Assert.False(keywords.Contains("and"));
            Assert.True(keywords.Contains("capital"));
            Assert.True(keywords.Contains("france"));
        }

        [Fact]
        public void ExtractKeywords_ShortWordsRemoved()
        {
            var keywords = PromptResponseEvaluator.ExtractKeywords("I am OK");
            Assert.False(keywords.Contains("am"));
            Assert.False(keywords.Contains("ok")); // 2 chars, removed
        }

        [Fact]
        public void ExtractPromptParts_NumberedList()
        {
            var parts = PromptResponseEvaluator.ExtractPromptParts(
                "1. Explain photosynthesis\n2. Describe respiration\n3. Compare them");

            Assert.True(parts.Count >= 2, $"Expected multiple parts, got {parts.Count}");
        }

        [Fact]
        public void ExtractPromptParts_BulletList()
        {
            var parts = PromptResponseEvaluator.ExtractPromptParts(
                "- Explain photosynthesis\n- Describe respiration\n- Compare them");

            Assert.True(parts.Count >= 2);
        }

        [Fact]
        public void ExtractPromptParts_SinglePrompt()
        {
            var parts = PromptResponseEvaluator.ExtractPromptParts("What is gravity?");
            Assert.Equal(1, parts.Count);
        }

        [Fact]
        public void DetectRequestedFormat_Json()
        {
            Assert.Equal(ResponseFormat.Json,
                PromptResponseEvaluator.DetectRequestedFormat("Return the data in JSON format"));
        }

        [Fact]
        public void DetectRequestedFormat_List()
        {
            Assert.Equal(ResponseFormat.List,
                PromptResponseEvaluator.DetectRequestedFormat("Give me a numbered list of 5 items"));
        }

        [Fact]
        public void DetectRequestedFormat_Code()
        {
            Assert.Equal(ResponseFormat.Code,
                PromptResponseEvaluator.DetectRequestedFormat("Write a function to sort an array"));
        }

        [Fact]
        public void DetectRequestedFormat_Table()
        {
            Assert.Equal(ResponseFormat.Table,
                PromptResponseEvaluator.DetectRequestedFormat("Show the data in a table with columns"));
        }

        [Fact]
        public void DetectRequestedFormat_StepByStep()
        {
            Assert.Equal(ResponseFormat.StepByStep,
                PromptResponseEvaluator.DetectRequestedFormat("Explain step by step how to do this"));
        }

        [Fact]
        public void DetectRequestedFormat_None()
        {
            Assert.Equal(ResponseFormat.None,
                PromptResponseEvaluator.DetectRequestedFormat("Tell me about cats"));
        }

        // ─── Grading ───────────────────────────────────────────────

        [Fact]
        public void Grade_HighQualityResponse_GetsAOrAbove()
        {
            var result = _evaluator.Evaluate(
                "What are the benefits of exercise?",
                "Exercise has numerous benefits: it improves cardiovascular health, " +
                "strengthens muscles and bones, boosts mental health by reducing anxiety and depression, " +
                "helps maintain a healthy weight, and improves sleep quality.");

            Assert.True(
                result.Grade.StartsWith("A") || result.Grade.StartsWith("B"),
                $"Expected A or B grade for high-quality response, got {result.Grade} (score: {result.CompositeScore})");
        }

        [Fact]
        public void Grade_PoorResponse_GetsLowGrade()
        {
            var result = _evaluator.Evaluate(
                "1. Explain quantum mechanics\n2. Describe string theory\n3. Compare them",
                "Physics is interesting.");

            Assert.True(
                result.Grade == "C" || result.Grade == "C-" || result.Grade == "C+" ||
                result.Grade == "D" || result.Grade == "F",
                $"Expected C/D/F grade for poor response, got {result.Grade}");
        }

        // ─── Diagnostics ───────────────────────────────────────────

        [Fact]
        public void Diagnostics_LowScoreDimension_AddsDiagnostic()
        {
            var result = _evaluator.Evaluate(
                "Return the data in JSON format",
                "Here is the data: name is Alice and age is 30.");

            // JSON was requested but not provided — format should be low
            Assert.True(result.Diagnostics.Count > 0,
                "Should have diagnostics for low-scoring dimensions");
        }

        [Fact]
        public void Diagnostics_HighScoreResponse_NoDiagnostics()
        {
            var result = _evaluator.Evaluate(
                "What is water?",
                "Water is a chemical compound with the formula H2O.");

            // All dimensions should be reasonable — no low-score diagnostics
            var lowDimensions = result.Dimensions.Where(d => d.Value.Score < 0.5).ToList();
            Assert.True(result.Diagnostics.Count == lowDimensions.Count,
                $"Diagnostics count ({result.Diagnostics.Count}) should match low-dimension count ({lowDimensions.Count})");
        }
    }
}
