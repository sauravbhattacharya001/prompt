namespace Prompt.Tests
{
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="PromptOptions"/> — the configurable model
    /// parameter class introduced to fix issue #8.
    /// </summary>
    [TestClass]
    public class PromptOptionsTests
    {
        // ──────────────── Defaults ────────────────

        [TestMethod]
        public void Defaults_MatchLegacyHardcodedValues()
        {
            var opts = new PromptOptions();
            Assert.AreEqual(0.7f, opts.Temperature);
            Assert.AreEqual(800, opts.MaxTokens);
            Assert.AreEqual(0.95f, opts.TopP);
            Assert.AreEqual(0f, opts.FrequencyPenalty);
            Assert.AreEqual(0f, opts.PresencePenalty);
        }

        // ──────────────── Temperature validation ────────────────

        [TestMethod]
        public void Temperature_ZeroIsValid()
        {
            var opts = new PromptOptions { Temperature = 0f };
            Assert.AreEqual(0f, opts.Temperature);
        }

        [TestMethod]
        public void Temperature_TwoIsValid()
        {
            var opts = new PromptOptions { Temperature = 2f };
            Assert.AreEqual(2f, opts.Temperature);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Temperature_NegativeThrows()
        {
            _ = new PromptOptions { Temperature = -0.1f };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Temperature_AboveTwoThrows()
        {
            _ = new PromptOptions { Temperature = 2.01f };
        }

        // ──────────────── MaxTokens validation ────────────────

        [TestMethod]
        public void MaxTokens_OneIsValid()
        {
            var opts = new PromptOptions { MaxTokens = 1 };
            Assert.AreEqual(1, opts.MaxTokens);
        }

        [TestMethod]
        public void MaxTokens_LargeValueIsValid()
        {
            var opts = new PromptOptions { MaxTokens = 128000 };
            Assert.AreEqual(128000, opts.MaxTokens);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void MaxTokens_ZeroThrows()
        {
            _ = new PromptOptions { MaxTokens = 0 };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void MaxTokens_NegativeThrows()
        {
            _ = new PromptOptions { MaxTokens = -1 };
        }

        // ──────────────── TopP validation ────────────────

        [TestMethod]
        public void TopP_ZeroIsValid()
        {
            var opts = new PromptOptions { TopP = 0f };
            Assert.AreEqual(0f, opts.TopP);
        }

        [TestMethod]
        public void TopP_OneIsValid()
        {
            var opts = new PromptOptions { TopP = 1f };
            Assert.AreEqual(1f, opts.TopP);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TopP_NegativeThrows()
        {
            _ = new PromptOptions { TopP = -0.01f };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TopP_AboveOneThrows()
        {
            _ = new PromptOptions { TopP = 1.01f };
        }

        // ──────────────── FrequencyPenalty validation ────────────────

        [TestMethod]
        public void FrequencyPenalty_NegativeTwoIsValid()
        {
            var opts = new PromptOptions { FrequencyPenalty = -2f };
            Assert.AreEqual(-2f, opts.FrequencyPenalty);
        }

        [TestMethod]
        public void FrequencyPenalty_TwoIsValid()
        {
            var opts = new PromptOptions { FrequencyPenalty = 2f };
            Assert.AreEqual(2f, opts.FrequencyPenalty);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void FrequencyPenalty_BelowNegativeTwoThrows()
        {
            _ = new PromptOptions { FrequencyPenalty = -2.01f };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void FrequencyPenalty_AboveTwoThrows()
        {
            _ = new PromptOptions { FrequencyPenalty = 2.01f };
        }

        // ──────────────── PresencePenalty validation ────────────────

        [TestMethod]
        public void PresencePenalty_NegativeTwoIsValid()
        {
            var opts = new PromptOptions { PresencePenalty = -2f };
            Assert.AreEqual(-2f, opts.PresencePenalty);
        }

        [TestMethod]
        public void PresencePenalty_TwoIsValid()
        {
            var opts = new PromptOptions { PresencePenalty = 2f };
            Assert.AreEqual(2f, opts.PresencePenalty);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PresencePenalty_BelowNegativeTwoThrows()
        {
            _ = new PromptOptions { PresencePenalty = -2.01f };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PresencePenalty_AboveTwoThrows()
        {
            _ = new PromptOptions { PresencePenalty = 2.01f };
        }

        // ──────────────── Factory presets ────────────────

        [TestMethod]
        public void ForCodeGeneration_HasLowTempHighTokens()
        {
            var opts = PromptOptions.ForCodeGeneration();
            Assert.AreEqual(0.1f, opts.Temperature);
            Assert.AreEqual(4000, opts.MaxTokens);
            Assert.AreEqual(0.95f, opts.TopP);
            Assert.AreEqual(0f, opts.FrequencyPenalty);
            Assert.AreEqual(0f, opts.PresencePenalty);
        }

        [TestMethod]
        public void ForCreativeWriting_HasHighTempMedTokens()
        {
            var opts = PromptOptions.ForCreativeWriting();
            Assert.AreEqual(0.9f, opts.Temperature);
            Assert.AreEqual(2000, opts.MaxTokens);
            Assert.AreEqual(0.9f, opts.TopP);
        }

        [TestMethod]
        public void ForDataExtraction_HasZeroTemp()
        {
            var opts = PromptOptions.ForDataExtraction();
            Assert.AreEqual(0f, opts.Temperature);
            Assert.AreEqual(2000, opts.MaxTokens);
            Assert.AreEqual(1f, opts.TopP);
        }

        [TestMethod]
        public void ForSummarization_HasLowTemp()
        {
            var opts = PromptOptions.ForSummarization();
            Assert.AreEqual(0.3f, opts.Temperature);
            Assert.AreEqual(1000, opts.MaxTokens);
            Assert.AreEqual(0.9f, opts.TopP);
        }

        // ──────────────── JSON serialization ────────────────

        [TestMethod]
        public void PromptOptions_RoundTripsViaJson()
        {
            var original = new PromptOptions
            {
                Temperature = 0.3f,
                MaxTokens = 4000,
                TopP = 0.85f,
                FrequencyPenalty = 0.5f,
                PresencePenalty = -0.5f
            };

            string json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<PromptOptions>(json)!;

            Assert.AreEqual(original.Temperature, restored.Temperature);
            Assert.AreEqual(original.MaxTokens, restored.MaxTokens);
            Assert.AreEqual(original.TopP, restored.TopP);
            Assert.AreEqual(original.FrequencyPenalty, restored.FrequencyPenalty);
            Assert.AreEqual(original.PresencePenalty, restored.PresencePenalty);
        }

        // ──────────────── Conversation integration ────────────────

        [TestMethod]
        public void Conversation_AcceptsPromptOptions()
        {
            var opts = PromptOptions.ForCodeGeneration();
            var conv = new Conversation("You are a coder.", opts);

            Assert.AreEqual(0.1f, conv.Temperature);
            Assert.AreEqual(4000, conv.MaxTokens);
            Assert.AreEqual(0.95f, conv.TopP);
            Assert.AreEqual(0f, conv.FrequencyPenalty);
            Assert.AreEqual(0f, conv.PresencePenalty);
            Assert.AreEqual(1, conv.MessageCount); // system prompt
        }

        [TestMethod]
        public void Conversation_PromptOptionsWithNullSystemPrompt()
        {
            var opts = new PromptOptions { Temperature = 1.5f, MaxTokens = 100 };
            var conv = new Conversation(null, opts);

            Assert.AreEqual(1.5f, conv.Temperature);
            Assert.AreEqual(100, conv.MaxTokens);
            Assert.AreEqual(0, conv.MessageCount); // no system prompt
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Conversation_NullOptionsThrows()
        {
            _ = new Conversation("test", (PromptOptions)null!);
        }

        // ──────────────── PromptChain integration ────────────────

        [TestMethod]
        public void PromptChain_WithOptions_SerializesRoundTrip()
        {
            var chain = new PromptChain()
                .WithSystemPrompt("test system")
                .WithOptions(new PromptOptions
                {
                    Temperature = 0.2f,
                    MaxTokens = 4000,
                    TopP = 0.8f,
                    FrequencyPenalty = 0.3f,
                    PresencePenalty = 0.1f
                })
                .AddStep("step1",
                    new PromptTemplate("Do something with {{input}}"),
                    "output");

            string json = chain.ToJson();
            var restored = PromptChain.FromJson(json);

            // Verify it round-trips by serializing again
            string json2 = restored.ToJson();

            // Parse both and compare options
            var doc1 = JsonDocument.Parse(json);
            var doc2 = JsonDocument.Parse(json2);

            var opts1 = doc1.RootElement.GetProperty("options");
            var opts2 = doc2.RootElement.GetProperty("options");

            Assert.AreEqual(
                opts1.GetProperty("temperature").GetSingle(),
                opts2.GetProperty("temperature").GetSingle());
            Assert.AreEqual(
                opts1.GetProperty("maxTokens").GetInt32(),
                opts2.GetProperty("maxTokens").GetInt32());
            Assert.AreEqual(
                opts1.GetProperty("topP").GetSingle(),
                opts2.GetProperty("topP").GetSingle());
            Assert.AreEqual(
                opts1.GetProperty("frequencyPenalty").GetSingle(),
                opts2.GetProperty("frequencyPenalty").GetSingle());
            Assert.AreEqual(
                opts1.GetProperty("presencePenalty").GetSingle(),
                opts2.GetProperty("presencePenalty").GetSingle());
        }

        [TestMethod]
        public void PromptChain_WithoutOptions_OmitsFromJson()
        {
            var chain = new PromptChain()
                .AddStep("step1",
                    new PromptTemplate("Do {{thing}}"),
                    "result");

            string json = chain.ToJson();
            var doc = JsonDocument.Parse(json);

            // Options should not be present when null
            Assert.IsFalse(doc.RootElement.TryGetProperty("options", out _));
        }

        [TestMethod]
        public void PromptChain_WithOptions_FluentApiReturnsThis()
        {
            var chain = new PromptChain();
            var result = chain.WithOptions(new PromptOptions());
            Assert.AreSame(chain, result);
        }

        [TestMethod]
        public void PromptChain_WithNullOptions_ClearsOptions()
        {
            var chain = new PromptChain()
                .WithOptions(new PromptOptions { Temperature = 0.1f })
                .WithOptions(null) // revert to defaults
                .AddStep("s", new PromptTemplate("test"), "out");

            string json = chain.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.IsFalse(doc.RootElement.TryGetProperty("options", out _));
        }

        // ──────────────── Custom values override defaults ────────────────

        [TestMethod]
        public void CustomValues_AllSettable()
        {
            var opts = new PromptOptions
            {
                Temperature = 1.8f,
                MaxTokens = 32000,
                TopP = 0.1f,
                FrequencyPenalty = 1.5f,
                PresencePenalty = -1.5f
            };

            Assert.AreEqual(1.8f, opts.Temperature);
            Assert.AreEqual(32000, opts.MaxTokens);
            Assert.AreEqual(0.1f, opts.TopP);
            Assert.AreEqual(1.5f, opts.FrequencyPenalty);
            Assert.AreEqual(-1.5f, opts.PresencePenalty);
        }
    }
}
