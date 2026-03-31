namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Xunit;

    public class PromptShareFormatterTests
    {
        private ShareablePrompt CreateSamplePrompt() => new()
        {
            SystemMessage = "You are a helpful coding assistant.",
            UserPrompt = "Write a fizzbuzz function in Python.",
            Examples = new List<(string, string)>
            {
                ("What is 2+2?", "4"),
                ("Say hello", "Hello!")
            },
            Metadata = new ShareMetadata
            {
                Title = "FizzBuzz Generator",
                Author = "TestUser",
                Description = "Generates a fizzbuzz implementation",
                Tags = new List<string> { "coding", "python" },
                Model = "gpt-4o",
                Version = "1.0"
            }
        };

        [Fact]
        public void Format_PlainText_ContainsTitle()
        {
            var prompt = CreateSamplePrompt();
            var result = PromptShareFormatter.Format(prompt, ShareFormat.PlainText);
            Assert.Contains("FizzBuzz Generator", result);
            Assert.Contains("You are a helpful coding assistant.", result);
            Assert.Contains("Write a fizzbuzz function in Python.", result);
            Assert.Contains("TestUser", result);
        }

        [Fact]
        public void Format_Markdown_ContainsHeaders()
        {
            var prompt = CreateSamplePrompt();
            var result = PromptShareFormatter.Format(prompt, ShareFormat.Markdown);
            Assert.Contains("# FizzBuzz Generator", result);
            Assert.Contains("## System Message", result);
            Assert.Contains("## User Prompt", result);
            Assert.Contains("## Few-Shot Examples", result);
            Assert.Contains("`gpt-4o`", result);
        }

        [Fact]
        public void Format_Html_IsValidHtml()
        {
            var prompt = CreateSamplePrompt();
            var result = PromptShareFormatter.Format(prompt, ShareFormat.Html);
            Assert.Contains("<!DOCTYPE html>", result);
            Assert.Contains("<title>FizzBuzz Generator</title>", result);
            Assert.Contains("</html>", result);
            Assert.Contains("coding", result);
        }

        [Fact]
        public void Format_Json_RoundTrips()
        {
            var prompt = CreateSamplePrompt();
            var json = PromptShareFormatter.Format(prompt, ShareFormat.Json);
            var imported = PromptShareFormatter.ImportFromJson(json);
            Assert.Equal(prompt.SystemMessage, imported.SystemMessage);
            Assert.Equal(prompt.UserPrompt, imported.UserPrompt);
            Assert.Equal(prompt.Metadata.Title, imported.Metadata.Title);
            Assert.Equal(prompt.Metadata.Author, imported.Metadata.Author);
            Assert.Equal(prompt.Metadata.Model, imported.Metadata.Model);
            Assert.Equal(prompt.Examples.Count, imported.Examples.Count);
        }

        [Fact]
        public void Format_Yaml_ContainsKeys()
        {
            var prompt = CreateSamplePrompt();
            var result = PromptShareFormatter.Format(prompt, ShareFormat.Yaml);
            Assert.Contains("title:", result);
            Assert.Contains("system_message:", result);
            Assert.Contains("user_prompt:", result);
            Assert.Contains("tags:", result);
        }

        [Fact]
        public void Format_NoSystemMessage_OmitsSection()
        {
            var prompt = new ShareablePrompt { UserPrompt = "Hello" };
            var md = PromptShareFormatter.Format(prompt, ShareFormat.Markdown);
            Assert.DoesNotContain("## System Message", md);
        }

        [Fact]
        public void Format_NoExamples_OmitsSection()
        {
            var prompt = new ShareablePrompt { UserPrompt = "Hello" };
            var md = PromptShareFormatter.Format(prompt, ShareFormat.Markdown);
            Assert.DoesNotContain("## Few-Shot Examples", md);
        }

        [Fact]
        public void EstimateTokens_ReturnsPositive()
        {
            var prompt = CreateSamplePrompt();
            var tokens = PromptShareFormatter.EstimateTokens(prompt);
            Assert.True(tokens > 0);
        }

        [Fact]
        public void Format_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PromptShareFormatter.Format(null!, ShareFormat.Markdown));
        }

        [Fact]
        public void ImportFromJson_NullJson_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PromptShareFormatter.ImportFromJson(null!));
        }

        [Fact]
        public void Html_EscapesSpecialChars()
        {
            var prompt = new ShareablePrompt
            {
                UserPrompt = "Use <b>bold</b> & \"quotes\"",
                Metadata = new ShareMetadata { Title = "Test <script>alert('xss')</script>" }
            };
            var html = PromptShareFormatter.Format(prompt, ShareFormat.Html);
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
        }

        [Fact]
        public async Task SaveAsync_WritesFile()
        {
            var prompt = CreateSamplePrompt();
            var path = Path.GetTempFileName();
            try
            {
                await PromptShareFormatter.SaveAsync(prompt, ShareFormat.Markdown, path);
                var content = await File.ReadAllTextAsync(path);
                Assert.Contains("FizzBuzz Generator", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void FromTemplate_CreatesShareable()
        {
            var template = new PromptTemplate("Translate {{text}} to {{language}}");
            var shared = PromptShareFormatter.FromTemplate(template, "Translator", "Alice");
            Assert.Equal("Translator", shared.Metadata.Title);
            Assert.Equal("Alice", shared.Metadata.Author);
            Assert.Contains("text", shared.Metadata.Description!);
            Assert.Contains("{{text}}", shared.UserPrompt);
        }
    }
}
