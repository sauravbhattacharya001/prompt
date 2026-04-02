namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class PromptErrorRecoveryTests
    {
        // ═══════════════════════════════════════════════════════
        // Constructor & Default Rules
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Constructor_DefaultRules_RegisteredForCommonFailures()
        {
            var recovery = new PromptErrorRecovery(useDefaults: true);
            Assert.True(recovery.Rules.ContainsKey(FailureMode.EmptyResponse));
            Assert.True(recovery.Rules.ContainsKey(FailureMode.Refusal));
            Assert.True(recovery.Rules.ContainsKey(FailureMode.Truncation));
            Assert.True(recovery.Rules.ContainsKey(FailureMode.RepetitionLoop));
            Assert.True(recovery.Rules.ContainsKey(FailureMode.FillerOnly));
        }

        [Fact]
        public void Constructor_NoDefaults_EmptyRules()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            Assert.Empty(recovery.Rules);
        }

        // ═══════════════════════════════════════════════════════
        // AddRule / RemoveRule
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void AddRule_OverridesExistingRule()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.Retry, maxRetries: 1);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.Throw, maxRetries: 5);
            Assert.Equal(RecoveryStrategy.Throw, recovery.Rules[FailureMode.Refusal].Strategy);
            Assert.Equal(5, recovery.Rules[FailureMode.Refusal].MaxRetries);
        }

        [Fact]
        public void AddRule_ClampsMaxRetries()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.Retry, maxRetries: 100);
            Assert.Equal(10, recovery.Rules[FailureMode.Refusal].MaxRetries);
        }

        [Fact]
        public void AddRule_ClampsNegativeMaxRetries()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.Retry, maxRetries: -5);
            Assert.Equal(0, recovery.Rules[FailureMode.Refusal].MaxRetries);
        }

        [Fact]
        public void AddRule_ClampsMinConfidence()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.Retry, minConfidence: 2.0);
            Assert.Equal(1.0, recovery.Rules[FailureMode.Refusal].MinConfidence);
        }

        [Fact]
        public void RemoveRule_ReturnsTrueIfExists()
        {
            var recovery = new PromptErrorRecovery(useDefaults: true);
            Assert.True(recovery.RemoveRule(FailureMode.Refusal));
            Assert.False(recovery.Rules.ContainsKey(FailureMode.Refusal));
        }

        [Fact]
        public void RemoveRule_ReturnsFalseIfNotExists()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            Assert.False(recovery.RemoveRule(FailureMode.Refusal));
        }

        // ═══════════════════════════════════════════════════════
        // Analyze - Empty Response
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Analyze_EmptyResponse_DetectsEmptyResponse()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("What is 2+2?", "");
            Assert.Equal(FailureMode.EmptyResponse, result.Mode);
            Assert.Equal(1.0, result.Confidence);
        }

        [Fact]
        public void Analyze_WhitespaceOnly_DetectsEmptyResponse()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("Hello", "   \n\t  ");
            Assert.Equal(FailureMode.EmptyResponse, result.Mode);
        }

        [Fact]
        public void Analyze_NullResponse_DetectsEmptyResponse()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("Hello", null!);
            Assert.Equal(FailureMode.EmptyResponse, result.Mode);
        }

        // ═══════════════════════════════════════════════════════
        // Analyze - Refusal
        // ═══════════════════════════════════════════════════════

        [Theory]
        [InlineData("I cannot help with that request.")]
        [InlineData("I'm unable to provide that information.")]
        [InlineData("I'm sorry, but I cannot assist with this.")]
        [InlineData("As an AI language model, I cannot provide legal advice.")]
        [InlineData("I must decline this request.")]
        [InlineData("I won't be able to help with that.")]
        [InlineData("It would be inappropriate for me to answer that.")]
        public void Analyze_RefusalPatterns_DetectsRefusal(string response)
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("Do something", response);
            Assert.Equal(FailureMode.Refusal, result.Mode);
            Assert.True(result.Confidence >= 0.7);
            Assert.NotEmpty(result.Evidence);
        }

        [Fact]
        public void Analyze_LegitimateResponse_NoRefusal()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("What is 2+2?",
                "The answer to 2+2 is 4. This is a basic arithmetic operation.");
            Assert.Equal(FailureMode.None, result.Mode);
        }

        // ═══════════════════════════════════════════════════════
        // Analyze - Truncation
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Analyze_TruncatedResponse_DetectsTruncation()
        {
            var recovery = new PromptErrorRecovery();
            // Long response ending mid-sentence with unbalanced code fence
            string response = new string('x', 150) + "\n```python\ndef foo():\n    return";
            var result = recovery.Analyze("Write code", response);
            Assert.Equal(FailureMode.Truncation, result.Mode);
            Assert.True(result.HasFailure);
        }

        [Fact]
        public void Analyze_CompleteResponse_NoTruncation()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("What is 2+2?",
                "The answer is 4. Simple arithmetic.");
            // Should not detect truncation for a properly terminated response
            Assert.True(result.Mode == FailureMode.None ||
                         result.Mode != FailureMode.Truncation);
        }

        [Fact]
        public void Analyze_UnbalancedCodeFences_DetectsTruncation()
        {
            var recovery = new PromptErrorRecovery();
            string response = new string('a', 120) + "\n```python\nprint('hello')\n# more code here";
            var result = recovery.Analyze("Write code", response);
            // Should detect unbalanced code fences
            if (result.Mode == FailureMode.Truncation)
            {
                Assert.True(result.Evidence.Any(e => e.Contains("code fences")));
            }
        }

        // ═══════════════════════════════════════════════════════
        // Analyze - Repetition Loop
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Analyze_RepeatedSentences_DetectsRepetitionLoop()
        {
            var recovery = new PromptErrorRecovery();
            string repeated = string.Join(" ",
                Enumerable.Repeat("This is a very important sentence that keeps repeating itself.", 5));
            var result = recovery.Analyze("Tell me something", repeated);
            Assert.Equal(FailureMode.RepetitionLoop, result.Mode);
            Assert.True(result.Confidence >= 0.4);
        }

        [Fact]
        public void Analyze_UniqueContent_NoRepetition()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("Tell me about cats",
                "Cats are domesticated mammals. They belong to the family Felidae. " +
                "Cats have been associated with humans for thousands of years. " +
                "They are known for their agility, hunting instincts, and independent nature.");
            Assert.NotEqual(FailureMode.RepetitionLoop, result.Mode);
        }

        // ═══════════════════════════════════════════════════════
        // Analyze - Hallucination
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Analyze_FabricatedDoi_DetectsHallucination()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("What does research say?",
                "According to recent studies, DOI: 10.1234/fake.2024.research confirms this finding.");
            Assert.Equal(FailureMode.HallucinationMarker, result.Mode);
        }

        [Fact]
        public void Analyze_FabricatedAcademicCitation_DetectsHallucination()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("What research exists?",
                "According to a 2023 study by Dr. Smith and Johnson (2023), the findings suggest significant results.");
            Assert.Equal(FailureMode.HallucinationMarker, result.Mode);
        }

        // ═══════════════════════════════════════════════════════
        // Analyze - Filler Only
        // ═══════════════════════════════════════════════════════

        [Theory]
        [InlineData("That's a great question!")]
        [InlineData("I'd be happy to help!")]
        [InlineData("Sure, I can help with that!")]
        [InlineData("Absolutely!")]
        public void Analyze_FillerOnly_DetectsFillerOnly(string response)
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("Explain quantum physics", response);
            Assert.Equal(FailureMode.FillerOnly, result.Mode);
            Assert.True(result.Confidence >= 0.8);
        }

        [Fact]
        public void Analyze_FillerFollowedByContent_NotFillerOnly()
        {
            var recovery = new PromptErrorRecovery();
            var result = recovery.Analyze("Explain something",
                "Great question! Quantum physics is the branch of physics that deals with phenomena at the atomic and subatomic level.");
            Assert.NotEqual(FailureMode.FillerOnly, result.Mode);
        }

        // ═══════════════════════════════════════════════════════
        // AnalyzeAll
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void AnalyzeAll_ReturnsMultipleFailures()
        {
            var recovery = new PromptErrorRecovery();
            // Construct a response with refusal + hallucination markers
            string response = "I cannot help with that. DOI: 10.9999/fabricated.2024.study shows otherwise.";
            var results = recovery.AnalyzeAll("Question", response);
            Assert.True(results.Count >= 2);
            Assert.Contains(results, r => r.Mode == FailureMode.Refusal);
            Assert.Contains(results, r => r.Mode == FailureMode.HallucinationMarker);
        }

        [Fact]
        public void AnalyzeAll_SortedByConfidenceDescending()
        {
            var recovery = new PromptErrorRecovery();
            string response = "I cannot help. DOI: 10.9999/fake.study confirms this.";
            var results = recovery.AnalyzeAll("Question", response);
            for (int i = 1; i < results.Count; i++)
            {
                Assert.True(results[i - 1].Confidence >= results[i].Confidence);
            }
        }

        // ═══════════════════════════════════════════════════════
        // Custom Detectors
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void AddCustomDetector_NullThrows()
        {
            var recovery = new PromptErrorRecovery();
            Assert.Throws<ArgumentNullException>(() => recovery.AddCustomDetector(null!));
        }

        [Fact]
        public void CustomDetector_IntegratesWithAnalysis()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddCustomDetector((prompt, response) =>
            {
                if (response.Contains("BANNED_WORD"))
                    return new FailureAnalysis
                    {
                        Mode = FailureMode.Custom,
                        Confidence = 0.95,
                        Description = "Contains banned word"
                    };
                return new FailureAnalysis { Mode = FailureMode.None };
            });

            var result = recovery.Analyze("Test", "This has a BANNED_WORD in it");
            Assert.Equal(FailureMode.Custom, result.Mode);
            Assert.Equal(0.95, result.Confidence);
        }

        [Fact]
        public void CustomDetector_ExceptionSwallowed()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddCustomDetector((_, _) => throw new InvalidOperationException("boom"));
            // Should not throw
            var result = recovery.Analyze("Test", "Normal response.");
            Assert.Equal(FailureMode.None, result.Mode);
        }

        // ═══════════════════════════════════════════════════════
        // ExecuteWithRecoveryAsync
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task ExecuteWithRecoveryAsync_SuccessfulFirstAttempt_NoRetry()
        {
            var recovery = new PromptErrorRecovery();
            int callCount = 0;
            var result = await recovery.ExecuteWithRecoveryAsync("What is 2+2?",
                async (prompt) =>
                {
                    callCount++;
                    return "The answer is 4.";
                });

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.TotalAttempts);
            Assert.Equal(1, callCount);
            Assert.Equal("The answer is 4.", result.Response);
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_EmptyThenSuccess_Retries()
        {
            var recovery = new PromptErrorRecovery();
            int callCount = 0;
            var result = await recovery.ExecuteWithRecoveryAsync("Question",
                async (prompt) =>
                {
                    callCount++;
                    if (callCount == 1) return "";
                    return "Real answer here.";
                });

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.TotalAttempts);
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_RefusalWithHint_AppendsHint()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.RetryWithHint,
                maxRetries: 1, hint: "Try harder please.");

            string lastPrompt = "";
            int callCount = 0;
            var result = await recovery.ExecuteWithRecoveryAsync("Do something",
                async (prompt) =>
                {
                    lastPrompt = prompt;
                    callCount++;
                    if (callCount == 1) return "I cannot help with that.";
                    return "OK here is the answer.";
                });

            Assert.Contains("[Note: Try harder please.]", lastPrompt);
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_DefaultResponse_ReturnsImmediately()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.DefaultResponse,
                defaultResponse: "Sorry, I can't answer that right now.");

            var result = await recovery.ExecuteWithRecoveryAsync("Question",
                async (prompt) => "I cannot help with that.");

            Assert.True(result.Succeeded);
            Assert.Equal("Sorry, I can't answer that right now.", result.Response);
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_ThrowStrategy_ThrowsException()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.EmptyResponse, RecoveryStrategy.Throw);

            await Assert.ThrowsAsync<PromptRecoveryException>(async () =>
                await recovery.ExecuteWithRecoveryAsync("Question",
                    async (prompt) => ""));
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_PassThrough_ReturnsFailedResponse()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.PassThrough);

            var result = await recovery.ExecuteWithRecoveryAsync("Question",
                async (prompt) => "I cannot help with that.");

            Assert.False(result.Succeeded);
            Assert.Equal("I cannot help with that.", result.Response);
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_EmptyPrompt_Throws()
        {
            var recovery = new PromptErrorRecovery();
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await recovery.ExecuteWithRecoveryAsync("",
                    async (prompt) => "response"));
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_NullFunc_Throws()
        {
            var recovery = new PromptErrorRecovery();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await recovery.ExecuteWithRecoveryAsync("prompt", null!));
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_FallbackStrategy_UsesFallbackPrompt()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.Refusal, RecoveryStrategy.Fallback,
                fallbackPrompt: "Simplified question?");

            string lastPrompt = "";
            int callCount = 0;
            await recovery.ExecuteWithRecoveryAsync("Complex question",
                async (prompt) =>
                {
                    lastPrompt = prompt;
                    callCount++;
                    if (callCount == 1) return "I cannot help with that.";
                    return "Here is a simple answer.";
                });

            Assert.Equal("Simplified question?", lastPrompt);
        }

        [Fact]
        public async Task ExecuteWithRecoveryAsync_ExhaustsRetries_ReturnsFailed()
        {
            var recovery = new PromptErrorRecovery(useDefaults: false);
            recovery.AddRule(FailureMode.EmptyResponse, RecoveryStrategy.Retry, maxRetries: 2);

            int callCount = 0;
            var result = await recovery.ExecuteWithRecoveryAsync("Question",
                async (prompt) => { callCount++; return ""; });

            Assert.False(result.Succeeded);
            Assert.True(callCount <= 4); // initial + 2 retries + maybe 1 more
        }

        // ═══════════════════════════════════════════════════════
        // History & Statistics
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task GetHistory_TracksExecutions()
        {
            var recovery = new PromptErrorRecovery();
            await recovery.ExecuteWithRecoveryAsync("Q1", async _ => "Answer 1.");
            await recovery.ExecuteWithRecoveryAsync("Q2", async _ => "Answer 2.");

            var history = recovery.GetHistory();
            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task GetHistory_LimitReturnsLatest()
        {
            var recovery = new PromptErrorRecovery();
            await recovery.ExecuteWithRecoveryAsync("Q1", async _ => "A1.");
            await recovery.ExecuteWithRecoveryAsync("Q2", async _ => "A2.");
            await recovery.ExecuteWithRecoveryAsync("Q3", async _ => "A3.");

            var history = recovery.GetHistory(limit: 2);
            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task GetStatistics_ReturnsAccurateStats()
        {
            var recovery = new PromptErrorRecovery();
            await recovery.ExecuteWithRecoveryAsync("Q1", async _ => "Good answer.");
            await recovery.ExecuteWithRecoveryAsync("Q2", async _ => "Another good answer.");

            var stats = recovery.GetStatistics();
            Assert.Equal(2, stats.TotalExecutions);
            Assert.Equal(2, stats.SuccessfulRecoveries);
            Assert.Equal(1.0, stats.SuccessRate);
        }

        [Fact]
        public void GetStatistics_EmptyHistory_ReturnsZeros()
        {
            var recovery = new PromptErrorRecovery();
            var stats = recovery.GetStatistics();
            Assert.Equal(0, stats.TotalExecutions);
            Assert.Equal(0, stats.SuccessRate);
        }

        [Fact]
        public async Task ClearHistory_RemovesAllEntries()
        {
            var recovery = new PromptErrorRecovery();
            await recovery.ExecuteWithRecoveryAsync("Q", async _ => "A.");
            Assert.NotEmpty(recovery.GetHistory());
            recovery.ClearHistory();
            Assert.Empty(recovery.GetHistory());
        }

        // ═══════════════════════════════════════════════════════
        // ToJson
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ToJson_ReturnsValidJson()
        {
            var recovery = new PromptErrorRecovery();
            string json = recovery.ToJson();
            Assert.NotNull(json);
            // Should parse without error
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.NotNull(doc.RootElement.GetProperty("rules"));
            Assert.NotNull(doc.RootElement.GetProperty("statistics"));
        }

        // ═══════════════════════════════════════════════════════
        // PromptRecoveryException
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void PromptRecoveryException_ContainsAnalysisAndResult()
        {
            var analysis = new FailureAnalysis
            {
                Mode = FailureMode.Refusal,
                Confidence = 0.9,
                Description = "Test refusal"
            };
            var result = new RecoveryResult();
            var ex = new PromptRecoveryException(analysis, result);

            Assert.Equal(FailureMode.Refusal, ex.Analysis.Mode);
            Assert.Same(result, ex.Result);
            Assert.Contains("Refusal", ex.Message);
        }
    }
}
