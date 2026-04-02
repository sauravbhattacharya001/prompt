namespace Prompt.Tests
{
    using Xunit;
    using System.Linq;

    public class PromptDialectTests
    {
        [Fact]
        public void Convert_ChatGPT_IncludesSystemAndRoles()
        {
            var dialect = new PromptDialect();
            dialect.SetSystemPrompt("You are a helper.");
            dialect.AddUserMessage("Hello");
            dialect.AddAssistantMessage("Hi there!");

            var result = dialect.Convert(ModelDialect.ChatGPT);

            Assert.Contains("[System]: You are a helper.", result.FormattedPrompt);
            Assert.Contains("[User]: Hello", result.FormattedPrompt);
            Assert.Contains("[Assistant]: Hi there!", result.FormattedPrompt);
            Assert.Equal(3, result.Messages.Count);
        }

        [Fact]
        public void Convert_Claude_UsesHumanAssistantFormat()
        {
            var dialect = new PromptDialect();
            dialect.SetSystemPrompt("Be helpful.");
            dialect.AddUserMessage("What is 2+2?");

            var result = dialect.Convert(ModelDialect.Claude);

            Assert.Contains("Human: What is 2+2?", result.FormattedPrompt);
            Assert.Contains("Assistant:", result.FormattedPrompt);
        }

        [Fact]
        public void Convert_Gemini_UsesModelRole()
        {
            var dialect = new PromptDialect();
            dialect.AddUserMessage("Test");
            dialect.AddAssistantMessage("Response");

            var result = dialect.Convert(ModelDialect.Gemini);

            Assert.Contains("[user]: Test", result.FormattedPrompt);
            Assert.Contains("[model]: Response", result.FormattedPrompt);
            Assert.Equal("model", result.Messages[1].Role);
        }

        [Fact]
        public void Convert_Llama_UsesSysAndInstTags()
        {
            var dialect = new PromptDialect();
            dialect.SetSystemPrompt("System prompt here");
            dialect.AddUserMessage("Question");

            var result = dialect.Convert(ModelDialect.Llama);

            Assert.Contains("<<SYS>>", result.FormattedPrompt);
            Assert.Contains("[INST]", result.FormattedPrompt);
            Assert.Contains("[/INST]", result.FormattedPrompt);
        }

        [Fact]
        public void Convert_Mistral_WarnsAboutSystemMessage()
        {
            var dialect = new PromptDialect();
            dialect.SetSystemPrompt("System");
            dialect.AddUserMessage("Hello");

            var result = dialect.Convert(ModelDialect.Mistral);

            Assert.Single(result.Warnings);
            Assert.Contains("system", result.Warnings[0], System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConvertAll_ReturnsAllDialects()
        {
            var dialect = new PromptDialect();
            dialect.AddUserMessage("Test");

            var all = dialect.ConvertAll();

            Assert.Equal(6, all.Count);
        }

        [Fact]
        public void Parse_Claude_RoundTrips()
        {
            var original = new PromptDialect();
            original.SetSystemPrompt("Be concise.");
            original.AddUserMessage("Hello");
            original.AddAssistantMessage("Hi");

            var formatted = original.Convert(ModelDialect.Claude).FormattedPrompt;
            var parsed = PromptDialect.Parse(formatted, ModelDialect.Claude);

            Assert.Equal("Be concise.", parsed.GetSystemPrompt());
            Assert.Equal(2, parsed.GetMessages().Count);
        }

        [Fact]
        public void Parse_Llama_ExtractsSystemPrompt()
        {
            var original = new PromptDialect();
            original.SetSystemPrompt("You are helpful.");
            original.AddUserMessage("Hi");
            original.AddAssistantMessage("Hello!");

            var formatted = original.Convert(ModelDialect.Llama).FormattedPrompt;
            var parsed = PromptDialect.Parse(formatted, ModelDialect.Llama);

            Assert.Equal("You are helpful.", parsed.GetSystemPrompt());
        }

        [Fact]
        public void ToApiJson_ChatGPT_ValidJson()
        {
            var dialect = new PromptDialect();
            dialect.SetSystemPrompt("System");
            dialect.AddUserMessage("Hi");

            var json = dialect.ToApiJson(ModelDialect.ChatGPT);

            Assert.Contains("\"messages\"", json);
            Assert.Contains("\"system\"", json);
        }

        [Fact]
        public void ToApiJson_Gemini_UsesModelRole()
        {
            var dialect = new PromptDialect();
            dialect.AddUserMessage("Hi");
            dialect.AddAssistantMessage("Hello");

            var json = dialect.ToApiJson(ModelDialect.Gemini);

            Assert.Contains("\"contents\"", json);
            Assert.Contains("\"model\"", json);
        }

        [Fact]
        public void Compare_ReturnsBothFormats()
        {
            var dialect = new PromptDialect();
            dialect.AddUserMessage("Test");

            var comparison = dialect.Compare(ModelDialect.ChatGPT, ModelDialect.Claude);

            Assert.Contains("ChatGPT", comparison);
            Assert.Contains("Claude", comparison);
            Assert.Contains("tokens", comparison);
        }

        [Fact]
        public void FluentApi_ChainsCorrectly()
        {
            var dialect = new PromptDialect()
                .SetSystemPrompt("System")
                .AddUserMessage("Q1")
                .AddAssistantMessage("A1")
                .AddUserMessage("Q2");

            Assert.Equal(3, dialect.GetMessages().Count);
            Assert.Equal("System", dialect.GetSystemPrompt());
        }
    }
}
