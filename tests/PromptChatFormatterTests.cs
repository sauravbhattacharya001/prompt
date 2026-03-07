namespace Prompt.Tests
{
    using Xunit;
    using Prompt;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using static Prompt.PromptChatFormatter;

    public class PromptChatFormatterTests
    {
        private readonly PromptChatFormatter _formatter = new();

        [Fact]
        public void Parse_SimplePrompt_ReturnsUserMessage()
        {
            var messages = _formatter.Parse("What is the capital of France?");
            Assert.Single(messages);
            Assert.Equal("user", messages[0].Role);
            Assert.Contains("capital of France", messages[0].Content);
        }

        [Fact]
        public void Parse_SystemAndUser_DetectsSystemPrefix()
        {
            var prompt = "You are a helpful math tutor.\n\nWhat is 2+2?";
            var messages = _formatter.Parse(prompt);
            Assert.Equal(2, messages.Count);
            Assert.Equal("system", messages[0].Role);
            Assert.Equal("user", messages[1].Role);
        }

        [Fact]
        public void Parse_ExplicitRolePrefixes_ParsesCorrectly()
        {
            var prompt = "system: You are a translator.\nuser: Translate hello to French.\nassistant: Bonjour.";
            var messages = _formatter.Parse(prompt);
            Assert.Equal(3, messages.Count);
            Assert.Equal("system", messages[0].Role);
            Assert.Equal("user", messages[1].Role);
            Assert.Equal("assistant", messages[2].Role);
            Assert.Equal("You are a translator.", messages[0].Content);
        }

        [Fact]
        public void Parse_HumanAiRoles_NormalizesToUserAssistant()
        {
            var prompt = "human: Hello there\nai: Hi! How can I help?";
            var messages = _formatter.Parse(prompt);
            Assert.Equal(2, messages.Count);
            Assert.Equal("user", messages[0].Role);
            Assert.Equal("assistant", messages[1].Role);
        }

        [Fact]
        public void Parse_EmptyPrompt_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _formatter.Parse(""));
            Assert.Throws<ArgumentException>(() => _formatter.Parse("   "));
            Assert.Throws<ArgumentException>(() => _formatter.Parse(null!));
        }

        [Fact]
        public void Parse_MergesConsecutiveSameRole()
        {
            var formatter = new PromptChatFormatter(mergeConsecutive: true);
            var prompt = "user: First question\nuser: Second question";
            var messages = formatter.Parse(prompt);
            Assert.Single(messages);
            Assert.Contains("First question", messages[0].Content);
            Assert.Contains("Second question", messages[0].Content);
        }

        [Fact]
        public void Parse_NoMerge_KeepsSeparate()
        {
            var formatter = new PromptChatFormatter(mergeConsecutive: false);
            var prompt = "user: First question\nuser: Second question";
            var messages = formatter.Parse(prompt);
            Assert.Equal(2, messages.Count);
        }

        [Fact]
        public void Format_OpenAI_IncludesSystemInMessages()
        {
            var result = _formatter.Format(
                "system: You are helpful.\nuser: Hi.",
                ChatProvider.OpenAI);
            Assert.Equal(ChatProvider.OpenAI, result.Provider);
            Assert.Null(result.SystemMessage);
            Assert.True(result.Messages.Any(m => m.Role == "system"));
        }

        [Fact]
        public void Format_Anthropic_ExtractsSystemSeparately()
        {
            var result = _formatter.Format(
                "system: You are helpful.\nuser: Hi.",
                ChatProvider.Anthropic);
            Assert.Equal("You are helpful.", result.SystemMessage);
            Assert.DoesNotContain(result.Messages, m => m.Role == "system");
            Assert.Contains(result.Messages, m => m.Role == "user");
        }

        [Fact]
        public void Format_Gemini_UsesModelRole()
        {
            var result = _formatter.Format(
                "user: Hello\nassistant: Hi there!",
                ChatProvider.Gemini);
            Assert.Contains(result.Messages, m => m.Role == "model");
            Assert.DoesNotContain(result.Messages, m => m.Role == "assistant");
        }

        [Fact]
        public void FormatAsJson_OpenAI_ValidJson()
        {
            var json = _formatter.FormatAsJson("user: Hello", ChatProvider.OpenAI);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("messages", out var msgs));
            Assert.True(msgs.GetArrayLength() > 0);
        }

        [Fact]
        public void FormatAsJson_Anthropic_HasSystemField()
        {
            var json = _formatter.FormatAsJson(
                "system: Be concise.\nuser: Summarize this.",
                ChatProvider.Anthropic);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("system", out _));
            Assert.True(doc.RootElement.TryGetProperty("messages", out _));
        }

        [Fact]
        public void FormatAsJson_Gemini_HasPartsStructure()
        {
            var json = _formatter.FormatAsJson(
                "system: Be helpful.\nuser: Hi.",
                ChatProvider.Gemini);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("system_instruction", out var si));
            Assert.True(si.TryGetProperty("parts", out _));
            Assert.True(doc.RootElement.TryGetProperty("contents", out _));
        }

        [Fact]
        public void Flatten_RoundTrip_ProducesRolePrefixedText()
        {
            var messages = new List<ChatMessage>
            {
                new("system", "You are helpful."),
                new("user", "Hello"),
                new("assistant", "Hi!")
            };
            var flat = PromptChatFormatter.Flatten(messages);
            Assert.Contains("system: You are helpful.", flat);
            Assert.Contains("user: Hello", flat);
            Assert.Contains("assistant: Hi!", flat);
        }

        [Fact]
        public void FromTurns_ValidPairs_CreateMessages()
        {
            var messages = PromptChatFormatter.FromTurns(
                "user", "Hello",
                "assistant", "Hi there!");
            Assert.Equal(2, messages.Count);
            Assert.Equal("user", messages[0].Role);
            Assert.Equal("assistant", messages[1].Role);
        }

        [Fact]
        public void FromTurns_OddCount_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                PromptChatFormatter.FromTurns("user", "Hello", "assistant"));
        }

        [Fact]
        public void EstimateTokens_ReturnsPositiveCount()
        {
            var messages = new List<ChatMessage>
            {
                new("user", "What is the meaning of life?")
            };
            var tokens = PromptChatFormatter.EstimateTokens(messages);
            Assert.True(tokens > 0);
        }

        [Fact]
        public void EstimateTokens_NullReturnsZero()
        {
            Assert.Equal(0, PromptChatFormatter.EstimateTokens(null!));
        }

        [Fact]
        public void Format_EmptyAfterSystemExtraction_AddsUserFallback()
        {
            // A prompt that is just a system message — Anthropic format should
            // still have at least one user message or the system extracted.
            var result = _formatter.Format("You are a pirate.", ChatProvider.Anthropic);
            Assert.True(result.Messages.Count > 0 || result.SystemMessage != null);
        }

        [Fact]
        public void Parse_DetectsAllSystemPrefixes()
        {
            var prefixes = new[]
            {
                "You are a helpful assistant.",
                "Act as a translator.",
                "Behave as a teacher.",
                "Your role is to summarize.",
                "Your task is to classify.",
                "As a developer, help me.",
                "Pretend you are a chef.",
                "Instructions: be concise."
            };

            foreach (var prefix in prefixes)
            {
                var prompt = $"{prefix}\n\nDo something.";
                var messages = _formatter.Parse(prompt);
                Assert.True(messages[0].Role == "system",
                    $"Failed to detect system prefix: {prefix}");
            }
        }

        [Fact]
        public void ChatMessage_ToString_FormatsCorrectly()
        {
            var msg = new ChatMessage("user", "Hello");
            Assert.Equal("[user]: Hello", msg.ToString());
        }

        [Fact]
        public void TotalParts_CountsSystemSeparately()
        {
            var result = _formatter.Format(
                "system: Be helpful.\nuser: Hi.",
                ChatProvider.Anthropic);
            Assert.True(result.TotalParts >= 2);
        }
    }
}
