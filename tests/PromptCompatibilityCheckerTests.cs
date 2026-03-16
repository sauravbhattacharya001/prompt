namespace Prompt.Tests
{
    using Prompt;
    using Xunit;

    public class PromptCompatibilityCheckerTests
    {
        private readonly PromptCompatibilityChecker _checker = new();

        [Fact]
        public void EmptyPrompt_ReturnsFullCompatibility()
        {
            var report = _checker.Analyze("");
            Assert.Equal(100, report.PortabilityScore);
            Assert.Equal("A", report.Grade);
            Assert.Equal(6, report.CompatibleProviders.Count);
        }

        [Fact]
        public void NullPrompt_ReturnsFullCompatibility()
        {
            var report = _checker.Analyze(null!);
            Assert.Equal(100, report.PortabilityScore);
        }

        [Fact]
        public void GenericPrompt_IsFullyPortable()
        {
            var report = _checker.Analyze("You are a helpful assistant. Answer questions clearly and concisely.");
            Assert.True(report.ErrorCount == 0);
            Assert.True(_checker.IsPortable("You are a helpful assistant."));
        }

        [Fact]
        public void OpenAI_FunctionSyntax_FlagsError()
        {
            var report = _checker.Analyze("Use functions: [ { name: 'get_weather' } ] to call tools.");
            Assert.Contains(report.Findings, f => f.RuleId == "OPENAI_FUNCTION_SYNTAX");
            Assert.True(report.ErrorCount > 0);
        }

        [Fact]
        public void OpenAI_ModelReference_FlagsWarning()
        {
            var report = _checker.Analyze("You are gpt-4o, a large language model by OpenAI.");
            Assert.Contains(report.Findings, f => f.RuleId == "OPENAI_GPT_REFERENCE");
            Assert.Equal(CompatibilitySeverity.Warning,
                report.Findings.First(f => f.RuleId == "OPENAI_GPT_REFERENCE").Severity);
        }

        [Fact]
        public void OpenAI_ResponseFormat_FlagsError()
        {
            var report = _checker.Analyze("Set response_format to json_object for structured output.");
            Assert.Contains(report.Findings, f => f.RuleId == "OPENAI_RESPONSE_FORMAT");
        }

        [Fact]
        public void Anthropic_XmlTags_FlagsWarning()
        {
            var report = _checker.Analyze("Use <thinking> tags to show your reasoning before answering.");
            Assert.Contains(report.Findings, f => f.RuleId == "ANTHROPIC_XML_TAGS");
        }

        [Fact]
        public void Anthropic_ClaudeRef_FlagsWarning()
        {
            var report = _checker.Analyze("You are Claude, an AI assistant made by Anthropic.");
            Assert.Contains(report.Findings, f => f.RuleId == "ANTHROPIC_CLAUDE_REF");
        }

        [Fact]
        public void Google_GeminiRef_FlagsWarning()
        {
            var report = _checker.Analyze("As Gemini, you should provide accurate responses.");
            Assert.Contains(report.Findings, f => f.RuleId == "GOOGLE_GEMINI_REF");
        }

        [Fact]
        public void Google_SafetySettings_FlagsError()
        {
            var report = _checker.Analyze("Configure safety_settings with harm_category for content filtering.");
            Assert.Contains(report.Findings, f => f.RuleId == "GOOGLE_SAFETY_SETTINGS");
        }

        [Fact]
        public void SpecialTokens_FlagsError()
        {
            var report = _checker.Analyze("Start with <|im_start|>system and end with <|im_end|>");
            Assert.Contains(report.Findings, f => f.RuleId == "SPECIAL_TOKENS" || f.RuleId == "CHAT_ML_FORMAT");
            Assert.True(report.ErrorCount > 0);
        }

        [Fact]
        public void ChatML_FlagsError()
        {
            var report = _checker.Analyze("<|im_start|>system\nYou are helpful.\n<|im_start|>user\nHello");
            Assert.Contains(report.Findings, f => f.RuleId == "CHAT_ML_FORMAT");
        }

        [Fact]
        public void TokenLimitAssumption_FlagsWarning()
        {
            var report = _checker.Analyze("This model has a 128k context window for processing documents.");
            Assert.Contains(report.Findings, f => f.RuleId == "TOKEN_LIMIT_ASSUMPTION");
        }

        [Fact]
        public void ToolUseReference_FlagsWarning()
        {
            var report = _checker.Analyze("The assistant can use tool_calls to interact with external systems.");
            Assert.Contains(report.Findings, f => f.RuleId == "TOOL_USE_GENERIC");
        }

        [Fact]
        public void MetaLlamaRef_FlagsWarning()
        {
            var report = _checker.Analyze("Running on llama-3 for text generation.");
            Assert.Contains(report.Findings, f => f.RuleId == "META_LLAMA_REF");
        }

        [Fact]
        public void MistralRef_FlagsWarning()
        {
            var report = _checker.Analyze("Using mixtral for code generation tasks.");
            Assert.Contains(report.Findings, f => f.RuleId == "MISTRAL_REF");
        }

        [Fact]
        public void JsonOutputRequest_FlagsInfo()
        {
            var report = _checker.Analyze("Please respond with valid JSON containing the results.");
            Assert.Contains(report.Findings, f => f.RuleId == "JSON_MODE_REQUEST");
            Assert.Equal(CompatibilitySeverity.Info,
                report.Findings.First(f => f.RuleId == "JSON_MODE_REQUEST").Severity);
        }

        [Fact]
        public void SystemPromptStyle_FlagsInfo()
        {
            var report = _checker.Analyze("You are a coding assistant specialized in Python.");
            Assert.Contains(report.Findings, f => f.RuleId == "SYSTEM_PROMPT_IN_TEXT");
        }

        [Fact]
        public void MarkdownImage_FlagsInfo()
        {
            var report = _checker.Analyze("Analyze this image: ![chart](https://example.com/chart.png)");
            Assert.Contains(report.Findings, f => f.RuleId == "MARKDOWN_IMAGE");
        }

        [Fact]
        public void IsPortable_ReturnsTrueForCleanPrompt()
        {
            Assert.True(_checker.IsPortable("Summarize the following text in three bullet points."));
        }

        [Fact]
        public void IsPortable_ReturnsFalseForProviderSpecific()
        {
            Assert.False(_checker.IsPortable("<|im_start|>system\nYou are helpful."));
        }

        [Fact]
        public void GetCompatibleProviders_ReturnsAllForClean()
        {
            var providers = _checker.GetCompatibleProviders("Tell me a joke.");
            Assert.Equal(6, providers.Count);
        }

        [Fact]
        public void Report_ToSummary_ContainsKey()
        {
            var report = _checker.Analyze("You are gpt-4o. Use <thinking> for reasoning.");
            var summary = report.ToSummary();
            Assert.Contains("Portability:", summary);
            Assert.Contains("Grade", summary);
        }

        [Fact]
        public void Report_HasProviderBreakdown()
        {
            var report = _checker.Analyze("You are a helpful assistant.");
            Assert.Equal(6, report.Providers.Count);
            Assert.All(report.Providers, p => Assert.False(string.IsNullOrEmpty(p.Provider)));
        }

        [Fact]
        public void MultipleIssues_AllDetected()
        {
            var text = "You are gpt-4o by OpenAI. Use <thinking> tags. Set response_format to json_object.";
            var report = _checker.Analyze(text);
            Assert.True(report.Findings.Count >= 3);
        }

        [Fact]
        public void Grade_CalculatedCorrectly()
        {
            // Clean prompt = A
            Assert.Equal("A", _checker.Analyze("Hello world.").Grade);

            // Prompt with errors should have lower grade
            var errorReport = _checker.Analyze("<|im_start|>system\nresponse_format json_object\nsafety_settings harm_category");
            Assert.True(errorReport.PortabilityScore < 95);
        }

        [Fact]
        public void Finding_HasSuggestion()
        {
            var report = _checker.Analyze("Configure response_format to json_object.");
            var finding = report.Findings.FirstOrDefault(f => f.RuleId == "OPENAI_RESPONSE_FORMAT");
            Assert.NotNull(finding);
            Assert.False(string.IsNullOrEmpty(finding!.Suggestion));
        }

        [Fact]
        public void LongPrompt_FlagsWarning()
        {
            // ~4000 tokens ≈ 14000 chars
            var longText = new string('x', 15000);
            var report = _checker.Analyze(longText);
            Assert.Contains(report.Findings, f => f.RuleId == "LONG_PROMPT");
        }

        [Fact]
        public void MultiLanguage_DetectedWhenMixed()
        {
            var report = _checker.Analyze("Translate the following: 你好世界 means hello world");
            Assert.Contains(report.Findings, f => f.RuleId == "MULTI_LANGUAGE_MIX");
        }

        [Fact]
        public void CohereRef_FlagsWarning()
        {
            var report = _checker.Analyze("Using cohere command-r for retrieval augmented generation.");
            Assert.Contains(report.Findings, f => f.RuleId == "COHERE_COMMAND_REF");
        }
    }
}
