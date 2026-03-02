namespace Prompt.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptDebuggerTests
    {
        // ── Basic analysis ───────────────────────────────────────

        [Fact]
        public void Analyze_EmptyPrompt_ReturnsErrorAndZeroScore()
        {
            var report = PromptDebugger.Analyze("");
            Assert.Equal(0, report.ClarityScore);
            Assert.Contains(report.Issues, i => i.Id == "EMPTY");
        }

        [Fact]
        public void Analyze_NullPrompt_ReturnsErrorAndZeroScore()
        {
            var report = PromptDebugger.Analyze(null!);
            Assert.Equal(0, report.ClarityScore);
            Assert.Contains(report.Issues, i => i.Id == "EMPTY");
        }

        [Fact]
        public void Analyze_WhitespacePrompt_ReturnsError()
        {
            var report = PromptDebugger.Analyze("   \t\n  ");
            Assert.Contains(report.Issues, i => i.Id == "EMPTY");
        }

        [Fact]
        public void Analyze_SimplePrompt_ReturnsReport()
        {
            var report = PromptDebugger.Analyze("Explain quantum computing");
            Assert.True(report.ClarityScore > 0);
            Assert.True(report.WordCount > 0);
            Assert.True(report.TokenEstimate > 0);
        }

        // ── Component detection ──────────────────────────────────

        [Fact]
        public void Analyze_DetectsPersona()
        {
            var report = PromptDebugger.Analyze("You are a senior developer. Write clean code.");
            Assert.Contains("Persona/Role", report.Components);
        }

        [Fact]
        public void Analyze_DetectsContext()
        {
            var report = PromptDebugger.Analyze("Context: We are building a REST API. Summarize the requirements.");
            Assert.Contains("Context", report.Components);
        }

        [Fact]
        public void Analyze_DetectsTask()
        {
            var report = PromptDebugger.Analyze("Task: Summarize the article in 3 bullet points.");
            Assert.Contains("Task", report.Components);
        }

        [Fact]
        public void Analyze_DetectsOutputFormat()
        {
            var report = PromptDebugger.Analyze("List the top 5 languages. Respond in JSON format.");
            Assert.Contains("Output Format", report.Components);
        }

        [Fact]
        public void Analyze_DetectsExamples()
        {
            var report = PromptDebugger.Analyze("Classify the sentiment. Example: 'Great product!' → positive.");
            Assert.Contains("Examples", report.Components);
        }

        [Fact]
        public void Analyze_DetectsReasoningStrategy()
        {
            var report = PromptDebugger.Analyze("Think step by step about how to solve this equation.");
            Assert.Contains("Reasoning Strategy", report.Components);
        }

        [Fact]
        public void Analyze_DetectsConstraints()
        {
            var report = PromptDebugger.Analyze("You must keep the response under 100 words. Never use jargon.");
            Assert.Contains("Constraints", report.Components);
        }

        [Fact]
        public void Analyze_DetectsToneStyle()
        {
            var report = PromptDebugger.Analyze("Write in a formal academic tone about climate change.");
            Assert.Contains("Tone/Style", report.Components);
        }

        [Fact]
        public void Analyze_WellStructuredPrompt_HighScore()
        {
            var prompt = @"You are a senior code reviewer.
Context: The user submitted a Python REST API.
Task: Review the code for security vulnerabilities.
You must focus on OWASP Top 10.
Example: SQL injection in query parameters → flag as critical.
Respond in a numbered list format.";
            var report = PromptDebugger.Analyze(prompt);
            Assert.True(report.ClarityScore >= 75, $"Expected >= 75, got {report.ClarityScore}");
            Assert.True(report.Components.Count >= 4, $"Expected >= 4 components, got {report.Components.Count}");
        }

        // ── Anti-pattern detection ───────────────────────────────

        [Fact]
        public void Analyze_DetectsVagueQuality_AP002()
        {
            var report = PromptDebugger.Analyze("Write an essay and make it good.");
            Assert.Contains(report.Issues, i => i.Id == "AP002");
        }

        [Fact]
        public void Analyze_DetectsFillerWords_AP005()
        {
            var report = PromptDebugger.Analyze("Just simply explain how databases obviously work.");
            Assert.Contains(report.Issues, i => i.Id == "AP005");
        }

        [Fact]
        public void Analyze_DetectsUnnecessaryMetaReference_AP006()
        {
            var report = PromptDebugger.Analyze("As an AI assistant, tell me about history.");
            Assert.Contains(report.Issues, i => i.Id == "AP006");
        }

        [Fact]
        public void Analyze_DetectsDuplicatedText_AP010()
        {
            // Exact contiguous duplication — the regex requires the repeated block immediately follows
            var chunk = "Describe the detailed history of quantum computing research ";
            var duplicated = chunk + chunk; // back-to-back identical 60-char blocks
            var report = PromptDebugger.Analyze(duplicated);
            Assert.Contains(report.Issues, i => i.Id == "AP010");
        }

        // ── Structural analysis ──────────────────────────────────

        [Fact]
        public void Analyze_ShortPrompt_WarnsAboutLength()
        {
            var report = PromptDebugger.Analyze("Dogs?");
            Assert.Contains(report.Issues, i => i.Id == "STRUCT002");
        }

        [Fact]
        public void Analyze_UnfilledPlaceholders_DetectsError()
        {
            var report = PromptDebugger.Analyze("Summarize {{article}} in {{language}}.");
            Assert.Contains(report.Issues, i => i.Id == "STRUCT004");
            Assert.Contains(report.Issues, i => i.Severity == IssueSeverity.Error);
        }

        [Fact]
        public void Analyze_NoActionVerb_WarnsAboutAmbiguity()
        {
            var report = PromptDebugger.Analyze("The history of computing is very interesting and complex.");
            Assert.Contains(report.Issues, i => i.Id == "STRUCT003");
        }

        [Fact]
        public void Analyze_HasActionVerb_NoSTRUCT003()
        {
            var report = PromptDebugger.Analyze("Explain the history of computing.");
            Assert.DoesNotContain(report.Issues, i => i.Id == "STRUCT003");
        }

        [Fact]
        public void Analyze_HasQuestionMark_NoSTRUCT003()
        {
            var report = PromptDebugger.Analyze("What is the history of computing?");
            Assert.DoesNotContain(report.Issues, i => i.Id == "STRUCT003");
        }

        [Fact]
        public void Analyze_WordAndSentenceCounts()
        {
            var report = PromptDebugger.Analyze("Explain quantum computing. Keep it simple.");
            Assert.True(report.WordCount > 0);
            Assert.True(report.SentenceCount > 0);
            Assert.True(report.LineCount >= 1);
        }

        // ── Clarity scoring ──────────────────────────────────────

        [Fact]
        public void ClarityScore_IsWithinRange()
        {
            var report = PromptDebugger.Analyze("Tell me something.");
            Assert.InRange(report.ClarityScore, 0, 100);
        }

        [Fact]
        public void ClarityGrade_MapsCorrectly()
        {
            var report = PromptDebugger.Analyze("Explain quantum computing.");
            var grade = report.ClarityGrade;
            Assert.True(new[] { "A", "B", "C", "D", "F" }.Contains(grade));
        }

        [Fact]
        public void ClarityScore_BetterPromptScoresHigher()
        {
            var vague = PromptDebugger.Analyze("Tell me about stuff.");
            var clear = PromptDebugger.Analyze(
                "Task: Summarize the key features of .NET 8. Respond in a numbered list. Keep it under 200 words.");
            Assert.True(clear.ClarityScore >= vague.ClarityScore,
                $"Clear ({clear.ClarityScore}) should score >= vague ({vague.ClarityScore})");
        }

        // ── Suggestions ──────────────────────────────────────────

        [Fact]
        public void Analyze_GeneratesSuggestions()
        {
            var report = PromptDebugger.Analyze("Tell me about dogs.");
            Assert.NotEmpty(report.SuggestedFixes);
        }

        [Fact]
        public void Analyze_SuggestionsAreCapped()
        {
            var report = PromptDebugger.Analyze("Tell me stuff.");
            Assert.True(report.SuggestedFixes.Count <= 5);
        }

        // ── Conversation analysis ────────────────────────────────

        [Fact]
        public void AnalyzeConversation_EmptyMessages_ReturnsError()
        {
            var report = PromptDebugger.AnalyzeConversation(new List<DebugChatMessage>());
            Assert.Contains(report.Issues, i => i.Id == "NO_MSGS");
        }

        [Fact]
        public void AnalyzeConversation_NullMessages_ReturnsError()
        {
            var report = PromptDebugger.AnalyzeConversation(null!);
            Assert.Contains(report.Issues, i => i.Id == "NO_MSGS");
        }

        [Fact]
        public void AnalyzeConversation_NoSystemMessage_SuggestsAdding()
        {
            var messages = new[]
            {
                new DebugChatMessage("user", "What is the capital of France?")
            };
            var report = PromptDebugger.AnalyzeConversation(messages);
            Assert.Contains(report.Issues, i => i.Id == "CONV001");
        }

        [Fact]
        public void AnalyzeConversation_WithSystemMessage_NoCONV001()
        {
            var messages = new[]
            {
                new DebugChatMessage("system", "You are a geography expert."),
                new DebugChatMessage("user", "What is the capital of France?")
            };
            var report = PromptDebugger.AnalyzeConversation(messages);
            Assert.DoesNotContain(report.Issues, i => i.Id == "CONV001");
        }

        [Fact]
        public void AnalyzeConversation_InvalidRole_DetectsWarning()
        {
            var messages = new[]
            {
                new DebugChatMessage("admin", "Do something special."),
                new DebugChatMessage("user", "Hello")
            };
            var report = PromptDebugger.AnalyzeConversation(messages);
            Assert.Contains(report.Issues, i => i.Id == "CONV002");
        }

        [Fact]
        public void AnalyzeConversation_EmptyMessage_DetectsWarning()
        {
            var messages = new[]
            {
                new DebugChatMessage("user", "Hello"),
                new DebugChatMessage("assistant", ""),
                new DebugChatMessage("user", "Are you there?")
            };
            var report = PromptDebugger.AnalyzeConversation(messages);
            Assert.Contains(report.Issues, i => i.Id == "CONV003");
        }

        [Fact]
        public void AnalyzeConversation_ConsecutiveSameRole_DetectsInfo()
        {
            var messages = new[]
            {
                new DebugChatMessage("user", "First message."),
                new DebugChatMessage("user", "Second message.")
            };
            var report = PromptDebugger.AnalyzeConversation(messages);
            Assert.Contains(report.Issues, i => i.Id == "CONV004");
        }

        [Fact]
        public void AnalyzeConversation_SetsMessageCount()
        {
            var messages = new[]
            {
                new DebugChatMessage("system", "You are helpful."),
                new DebugChatMessage("user", "Hello"),
                new DebugChatMessage("assistant", "Hi there!")
            };
            var report = PromptDebugger.AnalyzeConversation(messages);
            Assert.Equal(3, report.MessageCount);
        }

        // ── Comparison ───────────────────────────────────────────

        [Fact]
        public void Compare_ReturnsRecommendation()
        {
            var result = PromptDebugger.Compare(
                "Tell me about dogs.",
                "Task: List the top 5 dog breeds for families. Respond in JSON format.");
            Assert.False(string.IsNullOrEmpty(result.Recommendation));
            Assert.NotNull(result.ReportA);
            Assert.NotNull(result.ReportB);
        }

        [Fact]
        public void Compare_BetterPromptRecommended()
        {
            var result = PromptDebugger.Compare(
                "stuff about things",
                "Task: Summarize quantum computing in 3 bullet points. Use simple language.");
            // Prompt B should score higher
            Assert.True(result.ReportB.ClarityScore >= result.ReportA.ClarityScore);
        }

        [Fact]
        public void Compare_ShowsComponentDifferences()
        {
            var result = PromptDebugger.Compare(
                "Tell me about dogs.",
                "You are a vet. Explain common dog illnesses. Respond in a table format.");
            // B has persona + output format, A doesn't
            Assert.True(result.ComponentsOnlyInB.Count > 0 || result.ComponentsOnlyInA.Count > 0,
                "Should detect component differences");
        }

        [Fact]
        public void Compare_NullPrompts_HandlesGracefully()
        {
            var result = PromptDebugger.Compare(null!, null!);
            Assert.NotNull(result);
            Assert.NotNull(result.ReportA);
            Assert.NotNull(result.ReportB);
        }

        [Fact]
        public void Compare_ShowsTokenDifference()
        {
            var result = PromptDebugger.Compare(
                "Short prompt.",
                "This is a significantly longer prompt that contains many more words and tokens than the first one.");
            Assert.NotEqual(0, result.TokenDifference);
        }

        // ── Serialization ────────────────────────────────────────

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var report = PromptDebugger.Analyze("Explain quantum computing in simple terms.");
            var json = PromptDebugger.ToJson(report);
            Assert.False(string.IsNullOrEmpty(json));
            Assert.Contains("clarityScore", json);
            Assert.Contains("components", json);
        }

        [Fact]
        public void FromJson_RoundTrips()
        {
            var original = PromptDebugger.Analyze("List 5 programming languages. Use bullet points.");
            var json = PromptDebugger.ToJson(original);
            var restored = PromptDebugger.FromJson(json);
            Assert.NotNull(restored);
            Assert.Equal(original.ClarityScore, restored!.ClarityScore);
            Assert.Equal(original.WordCount, restored.WordCount);
        }

        // ── ToString ─────────────────────────────────────────────

        [Fact]
        public void Report_ToString_ContainsKeyInfo()
        {
            var report = PromptDebugger.Analyze("Explain databases.");
            var str = report.ToString();
            Assert.Contains("Clarity:", str);
            Assert.Contains("Components:", str);
            Assert.Contains("tokens", str);
        }

        // ── Edge cases ───────────────────────────────────────────

        [Fact]
        public void Analyze_VeryLongPrompt_HandlesGracefully()
        {
            var longPrompt = string.Join(" ", Enumerable.Repeat("Explain this concept in detail.", 200));
            var report = PromptDebugger.Analyze(longPrompt);
            Assert.True(report.ClarityScore >= 0 && report.ClarityScore <= 100);
            Assert.True(report.TokenEstimate > 500);
        }

        [Fact]
        public void Analyze_UnicodePrompt_HandlesGracefully()
        {
            var report = PromptDebugger.Analyze("Explain 量子計算 in English. 请用简单的语言.");
            Assert.True(report.ClarityScore > 0);
            Assert.True(report.WordCount > 0);
        }

        [Fact]
        public void Analyze_SpecialCharacters_HandlesGracefully()
        {
            var report = PromptDebugger.Analyze("What is 2 + 2? Use <html> tags & \"quotes\".");
            Assert.True(report.ClarityScore > 0);
        }
    }
}
