namespace Prompt.Tests
{
    using Xunit;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PromptVariantGeneratorTests
    {
        private readonly PromptVariantGenerator _gen = new(seed: 42);

        // ── Constructor ──────────────────────────────────────

        [Fact]
        public void Constructor_Default_DoesNotThrow()
        {
            var gen = new PromptVariantGenerator();
            Assert.NotNull(gen);
        }

        [Fact]
        public void Constructor_WithSeed_DoesNotThrow()
        {
            var gen = new PromptVariantGenerator(123);
            Assert.NotNull(gen);
        }

        // ── Generate — validation ────────────────────────────

        [Fact]
        public void Generate_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.Generate(null!, VariantConfig.Quick()));
        }

        [Fact]
        public void Generate_EmptyPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.Generate("", VariantConfig.Quick()));
        }

        [Fact]
        public void Generate_WhitespacePrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.Generate("   ", VariantConfig.Quick()));
        }

        [Fact]
        public void Generate_TooLongPrompt_Throws()
        {
            string huge = new string('x', PromptVariantGenerator.MaxPromptLength + 1);
            Assert.Throws<ArgumentException>(() =>
                _gen.Generate(huge, VariantConfig.Quick()));
        }

        [Fact]
        public void Generate_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _gen.Generate("Hello", null!));
        }

        [Fact]
        public void Generate_EmptyConfig_ReturnsNoVariants()
        {
            var result = _gen.Generate("Hello world", new VariantConfig());
            Assert.Empty(result.Variants);
            Assert.Equal("Hello world", result.Original);
        }

        // ── Tone transforms ─────────────────────────────────

        [Fact]
        public void ApplyTone_Formal_TransformsText()
        {
            string result = _gen.ApplyTone("You are a helpful assistant. Help the user find stuff.", PromptTone.Formal);
            Assert.Contains("You shall serve as", result);
            Assert.Contains("assist the user", result);
            Assert.Contains("material", result);
        }

        [Fact]
        public void ApplyTone_Casual_TransformsText()
        {
            string result = _gen.ApplyTone("Ensure the user can utilize the feature.", PromptTone.Casual);
            Assert.Contains("make sure", result);
            Assert.Contains("use", result);
        }

        [Fact]
        public void ApplyTone_Expert_TransformsText()
        {
            string result = _gen.ApplyTone("Summarize the problem and check for errors.", PromptTone.Expert);
            Assert.Contains("synthesize", result.ToLower());
            Assert.Contains("validate", result.ToLower());
        }

        [Fact]
        public void ApplyTone_Friendly_TransformsText()
        {
            string result = _gen.ApplyTone("You are a code reviewer. Do not miss errors.", PromptTone.Friendly);
            Assert.Contains("Hey", result);
            Assert.Contains("try not to", result);
        }

        [Fact]
        public void ApplyTone_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.ApplyTone(null!, PromptTone.Formal));
        }

        [Fact]
        public void ApplyTone_EmptyPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.ApplyTone("", PromptTone.Casual));
        }

        [Fact]
        public void ApplyTone_NoMatchingWords_ReturnsOriginal()
        {
            string prompt = "xyz abc 123";
            string result = _gen.ApplyTone(prompt, PromptTone.Formal);
            Assert.Equal(prompt, result);
        }

        // ── Verbosity transforms ─────────────────────────────

        [Fact]
        public void ApplyVerbosity_Concise_RemovesFiller()
        {
            string prompt = "You should basically just explain the concept. It is obviously very simple.";
            string result = _gen.ApplyVerbosity(prompt, VerbosityLevel.Concise);
            Assert.DoesNotContain("basically", result);
            Assert.DoesNotContain("just", result);
            Assert.DoesNotContain("obviously", result);
            Assert.DoesNotContain("very", result);
        }

        [Fact]
        public void ApplyVerbosity_Normal_ReturnsOriginal()
        {
            string prompt = "Explain the concept.";
            string result = _gen.ApplyVerbosity(prompt, VerbosityLevel.Normal);
            Assert.Equal(prompt, result);
        }

        [Fact]
        public void ApplyVerbosity_Detailed_AddsElaboration()
        {
            string prompt = "Explain the concept.";
            string result = _gen.ApplyVerbosity(prompt, VerbosityLevel.Detailed);
            Assert.StartsWith("Explain the concept.", result);
            Assert.True(result.Length > prompt.Length);
        }

        [Fact]
        public void ApplyVerbosity_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.ApplyVerbosity(null!, VerbosityLevel.Concise));
        }

        [Fact]
        public void ApplyVerbosity_Concise_CollapsesSpaces()
        {
            string prompt = "Hello   really   very   good";
            string result = _gen.ApplyVerbosity(prompt, VerbosityLevel.Concise);
            Assert.DoesNotContain("  ", result);
        }

        // ── Instruction style transforms ─────────────────────

        [Fact]
        public void ApplyInstructionStyle_QuestionToImperative()
        {
            string prompt = "Can you summarize the document?";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Imperative);
            Assert.DoesNotContain("Can you", result);
            Assert.EndsWith(".", result);
        }

        [Fact]
        public void ApplyInstructionStyle_ImperativeToQuestion()
        {
            string prompt = "Summarize the document.";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Question);
            Assert.StartsWith("Can you", result);
            Assert.EndsWith("?", result);
        }

        [Fact]
        public void ApplyInstructionStyle_ImperativeToDescriptive()
        {
            string prompt = "Summarize the document.";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Descriptive);
            Assert.StartsWith("Your task is to", result);
        }

        [Fact]
        public void ApplyInstructionStyle_DescriptiveToImperative()
        {
            string prompt = "Your task is to summarize the document.";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Imperative);
            Assert.DoesNotContain("Your task is to", result);
        }

        [Fact]
        public void ApplyInstructionStyle_DescriptiveToQuestion()
        {
            string prompt = "Your task is to summarize the document.";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Question);
            Assert.Contains("Can you", result);
            Assert.EndsWith("?", result);
        }

        [Fact]
        public void ApplyInstructionStyle_AlreadyImperative_StaysImperative()
        {
            string prompt = "Generate a summary of the text.";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Imperative);
            Assert.Equal(prompt, result);
        }

        [Fact]
        public void ApplyInstructionStyle_AlreadyQuestion_StaysQuestion()
        {
            string prompt = "What is the meaning of life?";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Question);
            Assert.Equal(prompt, result);
        }

        [Fact]
        public void ApplyInstructionStyle_AlreadyDescriptive_StaysDescriptive()
        {
            string prompt = "Your task is to explain quantum computing.";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Descriptive);
            Assert.Equal(prompt, result);
        }

        [Fact]
        public void ApplyInstructionStyle_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.ApplyInstructionStyle(null!, InstructionStyle.Imperative));
        }

        // ── Strategy transforms ──────────────────────────────

        [Fact]
        public void ApplyStrategy_ChainOfThought_PrependsCOT()
        {
            string prompt = "Explain this code.";
            string result = _gen.ApplyStrategy(prompt, ReasoningStrategy.ChainOfThought);
            Assert.StartsWith("Let's think step by step.", result);
            Assert.Contains("Explain this code.", result);
        }

        [Fact]
        public void ApplyStrategy_Structured_PrependsStructured()
        {
            string prompt = "Solve the equation.";
            string result = _gen.ApplyStrategy(prompt, ReasoningStrategy.Structured);
            Assert.StartsWith("First, list what you know.", result);
        }

        [Fact]
        public void ApplyStrategy_Exploratory_PrependsExploratory()
        {
            string prompt = "Design a system.";
            string result = _gen.ApplyStrategy(prompt, ReasoningStrategy.Exploratory);
            Assert.StartsWith("Consider multiple approaches", result);
        }

        [Fact]
        public void ApplyStrategy_Calm_PrependsCalm()
        {
            string prompt = "Debug this issue.";
            string result = _gen.ApplyStrategy(prompt, ReasoningStrategy.Calm);
            Assert.StartsWith("Take a deep breath", result);
        }

        [Fact]
        public void ApplyStrategy_None_ReturnsOriginal()
        {
            string prompt = "Hello world.";
            string result = _gen.ApplyStrategy(prompt, ReasoningStrategy.None);
            Assert.Equal(prompt, result);
        }

        [Fact]
        public void ApplyStrategy_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _gen.ApplyStrategy(null!, ReasoningStrategy.ChainOfThought));
        }

        // ── Generate with configs ────────────────────────────

        [Fact]
        public void Generate_QuickConfig_ProducesVariants()
        {
            var result = _gen.Generate(
                "You are a helpful assistant. Help the user with coding.",
                VariantConfig.Quick());
            Assert.True(result.Variants.Count > 0);
            Assert.All(result.Variants, v => Assert.NotEmpty(v.Label));
            Assert.All(result.Variants, v => Assert.NotEmpty(v.Text));
            Assert.All(result.Variants, v => Assert.True(v.EstimatedTokens > 0));
        }

        [Fact]
        public void Generate_AllAxes_ProducesManyVariants()
        {
            var result = _gen.Generate(
                "You are a helpful assistant. Summarize the document. Make sure to find stuff.",
                VariantConfig.AllAxes());
            // Some transforms may produce identical text and get skipped,
            // but with enough matchable words we should get a good spread
            Assert.True(result.Variants.Count >= 5,
                $"Expected >= 5 variants but got {result.Variants.Count}");
        }

        [Fact]
        public void Generate_ToneOnly_CorrectCount()
        {
            var config = new VariantConfig
            {
                Tones = new List<PromptTone> { PromptTone.Formal, PromptTone.Casual }
            };
            var result = _gen.Generate("Help the user fix stuff.", config);
            Assert.True(result.Variants.Count > 0);
        }

        [Fact]
        public void Generate_CustomPrefix_PrependedToPrompt()
        {
            var config = new VariantConfig
            {
                CustomPrefixes = new List<string> { "SYSTEM: You are an expert." }
            };
            var result = _gen.Generate("Summarize the article.", config);
            Assert.Single(result.Variants);
            Assert.StartsWith("SYSTEM: You are an expert.", result.Variants[0].Text);
            Assert.Contains("Summarize the article.", result.Variants[0].Text);
        }

        [Fact]
        public void Generate_CustomSuffix_AppendedToPrompt()
        {
            var config = new VariantConfig
            {
                CustomSuffixes = new List<string> { "Reply in JSON format." }
            };
            var result = _gen.Generate("List the top 5 items.", config);
            Assert.Single(result.Variants);
            Assert.EndsWith("Reply in JSON format.", result.Variants[0].Text);
        }

        [Fact]
        public void Generate_EmptyPrefix_Skipped()
        {
            var config = new VariantConfig
            {
                CustomPrefixes = new List<string> { "", "Real prefix" }
            };
            var result = _gen.Generate("Test prompt.", config);
            Assert.Single(result.Variants);
            Assert.Contains("Real prefix", result.Variants[0].Text);
        }

        // ── Combinatorial mode ───────────────────────────────

        [Fact]
        public void Generate_Combinatorial_ProducesCartesianProduct()
        {
            var config = new VariantConfig
            {
                Tones = new List<PromptTone> { PromptTone.Formal },
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought },
                Combinatorial = true
            };
            var result = _gen.Generate(
                "You are a helpful assistant. Explain the code.", config);
            Assert.True(result.Variants.Count >= 1);
            var combined = result.Variants.FirstOrDefault(v =>
                v.Transforms.Any(t => t.Contains("tone")) &&
                v.Transforms.Any(t => t.Contains("strategy")));
            Assert.NotNull(combined);
        }

        [Fact]
        public void Generate_Combinatorial_MultipleAxes()
        {
            var config = new VariantConfig
            {
                Tones = new List<PromptTone> { PromptTone.Formal, PromptTone.Casual },
                Verbosities = new List<VerbosityLevel> { VerbosityLevel.Concise, VerbosityLevel.Detailed },
                Combinatorial = true
            };
            var result = _gen.Generate("You are a helpful assistant. Really just explain things clearly.", config);
            Assert.True(result.Variants.Count >= 2);
        }

        [Fact]
        public void Generate_Combinatorial_MaxVariantsEnforced()
        {
            var config = new VariantConfig
            {
                Tones = new List<PromptTone>
                    { PromptTone.Formal, PromptTone.Casual, PromptTone.Expert, PromptTone.Friendly },
                Styles = new List<InstructionStyle>
                    { InstructionStyle.Imperative, InstructionStyle.Question, InstructionStyle.Descriptive },
                Strategies = new List<ReasoningStrategy>
                    { ReasoningStrategy.ChainOfThought, ReasoningStrategy.Structured,
                      ReasoningStrategy.Exploratory, ReasoningStrategy.Calm },
                Verbosities = new List<VerbosityLevel>
                    { VerbosityLevel.Concise, VerbosityLevel.Detailed },
                Combinatorial = true,
                MaxVariants = 5
            };
            var result = _gen.Generate("You are a helpful assistant. Summarize the text.", config);
            Assert.True(result.Variants.Count <= 5);
        }

        // ── MaxVariants limit ────────────────────────────────

        [Fact]
        public void Generate_MaxVariants_Respected()
        {
            var config = VariantConfig.AllAxes();
            config.MaxVariants = 3;
            var result = _gen.Generate("You are a helpful assistant. Summarize things.", config);
            Assert.True(result.Variants.Count <= 3);
        }

        // ── Result properties ────────────────────────────────

        [Fact]
        public void Result_OriginalTokens_Calculated()
        {
            var result = _gen.Generate("Hello world", new VariantConfig
            {
                Tones = new List<PromptTone> { PromptTone.Formal }
            });
            // "Hello world" = 11 chars → ceil(11/4) = 3 tokens
            Assert.Equal(3, result.OriginalTokens);
        }

        [Fact]
        public void Result_ShortestAndLongest_Set()
        {
            var result = _gen.Generate(
                "You are a helpful assistant. Really just basically explain the concept very clearly.",
                VariantConfig.AllAxes());
            Assert.NotNull(result.Shortest);
            Assert.NotNull(result.Longest);
            Assert.True(result.Shortest!.EstimatedTokens <= result.Longest!.EstimatedTokens);
        }

        [Fact]
        public void Result_LengthDelta_Correct()
        {
            string prompt = "Explain the concept.";
            var config = new VariantConfig
            {
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought }
            };
            var result = _gen.Generate(prompt, config);
            Assert.Single(result.Variants);
            int expected = result.Variants[0].Text.Length - prompt.Length;
            Assert.Equal(expected, result.Variants[0].LengthDelta);
        }

        [Fact]
        public void GeneratedVariant_ToString_ContainsLabel()
        {
            var result = _gen.Generate("Summarize the document.",
                new VariantConfig { Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought } });
            Assert.Single(result.Variants);
            string str = result.Variants[0].ToString();
            Assert.Contains("chainofthought", str);
            Assert.Contains("tokens", str);
        }

        // ── GetSummary ───────────────────────────────────────

        [Fact]
        public void GetSummary_ContainsOverview()
        {
            var result = _gen.Generate(
                "You are a helpful assistant. Summarize things.",
                VariantConfig.Quick());
            string summary = result.GetSummary();
            Assert.Contains("Variant Generation Summary", summary);
            Assert.Contains("Variants generated:", summary);
        }

        // ── ToJson ───────────────────────────────────────────

        [Fact]
        public void ToJson_ValidJson()
        {
            var result = _gen.Generate(
                "You are a helpful assistant. Really explain this clearly.",
                VariantConfig.Quick());
            string json = result.ToJson();
            Assert.NotEmpty(json);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("variants", out _));
            Assert.True(doc.RootElement.GetProperty("variantCount").GetInt32() > 0);
        }

        [Fact]
        public void ToJson_Compact_NotIndented()
        {
            var result = _gen.Generate("Explain this.",
                new VariantConfig { Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought } });
            string json = result.ToJson(indented: false);
            Assert.DoesNotContain("\n", json);
        }

        // ── Identical transforms skipped ─────────────────────

        [Fact]
        public void Generate_IdenticalTransform_SkipsVariant()
        {
            var config = new VariantConfig
            {
                Tones = new List<PromptTone> { PromptTone.Formal }
            };
            var result = _gen.Generate("xyz abc 123", config);
            Assert.Empty(result.Variants);
        }

        // ── Edge cases ───────────────────────────────────────

        [Fact]
        public void ApplyTone_CaseInsensitive()
        {
            string result = _gen.ApplyTone("HELP THE USER find stuff", PromptTone.Formal);
            Assert.DoesNotContain("stuff", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ApplyInstructionStyle_CouldYou_ToImperative()
        {
            string prompt = "Could you review this code?";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Imperative);
            Assert.DoesNotContain("Could you", result);
            Assert.EndsWith(".", result);
        }

        [Fact]
        public void ApplyInstructionStyle_WouldYou_ToImperative()
        {
            string prompt = "Would you explain the algorithm?";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Imperative);
            Assert.DoesNotContain("Would you", result);
        }

        [Fact]
        public void ApplyInstructionStyle_TheGoalIsTo_ToImperative()
        {
            string prompt = "The goal is to fix the bug.";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Imperative);
            Assert.DoesNotContain("The goal is to", result);
        }

        [Fact]
        public void ApplyInstructionStyle_Question_LongPrompt_FallsBack()
        {
            string prompt = new string('a', 250);
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Question);
            Assert.Equal(prompt, result);
        }

        [Fact]
        public void Generate_NoNullVariantTexts()
        {
            var result = _gen.Generate(
                "You are a helpful assistant. Summarize the article. Make sure to find the key things.",
                VariantConfig.AllAxes());
            Assert.All(result.Variants, v =>
            {
                Assert.NotNull(v.Text);
                Assert.NotEmpty(v.Text);
            });
        }

        [Fact]
        public void Generate_AllTransformsHaveLabels()
        {
            var result = _gen.Generate(
                "You are a helpful assistant. Really just explain things.",
                VariantConfig.AllAxes());
            Assert.All(result.Variants, v =>
            {
                Assert.NotNull(v.Label);
                Assert.NotEmpty(v.Label);
                Assert.NotNull(v.Transforms);
                Assert.NotEmpty(v.Transforms);
            });
        }

        [Fact]
        public void ApplyVerbosity_Concise_CollapsesNewlines()
        {
            string prompt = "Line1\n\n\n\n\nLine2";
            string result = _gen.ApplyVerbosity(prompt, VerbosityLevel.Concise);
            Assert.DoesNotContain("\n\n\n", result);
        }

        [Fact]
        public void Generate_MultipleCustomSuffixes()
        {
            var config = new VariantConfig
            {
                CustomSuffixes = new List<string> { "Reply in JSON.", "Be concise." }
            };
            var result = _gen.Generate("Explain X.", config);
            Assert.Equal(2, result.Variants.Count);
        }

        [Fact]
        public void Generate_MixedAxes_IndependentMode()
        {
            var config = new VariantConfig
            {
                Tones = new List<PromptTone> { PromptTone.Expert },
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought },
                CustomPrefixes = new List<string> { "SYSTEM: Be precise." }
            };
            var result = _gen.Generate("Summarize the findings and check for errors.", config);
            Assert.True(result.Variants.Count >= 2);
        }

        [Fact]
        public void VariantConfig_AllAxes_HasAllEnumValues()
        {
            var config = VariantConfig.AllAxes();
            Assert.Equal(4, config.Tones.Count);
            Assert.Equal(3, config.Styles.Count);
            Assert.Equal(4, config.Strategies.Count);
            Assert.Equal(2, config.Verbosities.Count);
        }

        [Fact]
        public void VariantConfig_Quick_HasSubset()
        {
            var config = VariantConfig.Quick();
            Assert.Equal(2, config.Tones.Count);
            Assert.Equal(2, config.Verbosities.Count);
            Assert.Empty(config.Styles);
            Assert.Empty(config.Strategies);
        }

        // ── Deterministic with seed ──────────────────────────

        [Fact]
        public void Generate_SameSeed_SameResults()
        {
            var gen1 = new PromptVariantGenerator(99);
            var gen2 = new PromptVariantGenerator(99);
            string prompt = "You are a helpful assistant. Really just explain the concept very clearly.";
            var r1 = gen1.Generate(prompt, VariantConfig.AllAxes());
            var r2 = gen2.Generate(prompt, VariantConfig.AllAxes());
            Assert.Equal(r1.Variants.Count, r2.Variants.Count);
            for (int i = 0; i < r1.Variants.Count; i++)
            {
                Assert.Equal(r1.Variants[i].Text, r2.Variants[i].Text);
                Assert.Equal(r1.Variants[i].Label, r2.Variants[i].Label);
            }
        }

        // ── Result with no variants ──────────────────────────

        [Fact]
        public void Result_NoVariants_ShortestAndLongestNull()
        {
            var result = _gen.Generate("xyz", new VariantConfig());
            Assert.Null(result.Shortest);
            Assert.Null(result.Longest);
        }

        [Fact]
        public void GetSummary_NoVariants_StillWorks()
        {
            var result = _gen.Generate("xyz", new VariantConfig());
            string summary = result.GetSummary();
            Assert.Contains("Variants generated: 0", summary);
        }

        // ── Strategy.None skipped in independent mode ────────

        [Fact]
        public void Generate_StrategyNone_Skipped()
        {
            var config = new VariantConfig
            {
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.None }
            };
            var result = _gen.Generate("Test prompt.", config);
            Assert.Empty(result.Variants);
        }

        [Fact]
        public void Generate_VerbosityNormal_Skipped()
        {
            var config = new VariantConfig
            {
                Verbosities = new List<VerbosityLevel> { VerbosityLevel.Normal }
            };
            var result = _gen.Generate("Test prompt.", config);
            Assert.Empty(result.Variants);
        }

        [Fact]
        public void Generate_Combinatorial_SkipsAllNull()
        {
            var config = new VariantConfig { Combinatorial = true };
            var result = _gen.Generate("Test.", config);
            Assert.Empty(result.Variants);
        }

        // ── Real-world prompt ────────────────────────────────

        [Fact]
        public void Generate_RealWorldPrompt_ProducesVariants()
        {
            string prompt = @"You are a senior software engineer. Review the following code for security vulnerabilities.
Focus on OWASP Top 10 issues. Be concise but thorough. List each finding with severity.";

            var result = _gen.Generate(prompt, VariantConfig.AllAxes());
            Assert.True(result.Variants.Count >= 5);
            Assert.NotNull(result.Shortest);
            Assert.NotNull(result.Longest);
            Assert.True(result.Shortest!.EstimatedTokens < result.Longest!.EstimatedTokens);
        }

        [Fact]
        public void Generate_MultiLinePrompt_PreservesStructure()
        {
            string prompt = "Step 1: Read the file.\nStep 2: Analyze the code.\nStep 3: Write a summary.";
            var config = new VariantConfig
            {
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought }
            };
            var result = _gen.Generate(prompt, config);
            Assert.Single(result.Variants);
            Assert.Contains("Step 1:", result.Variants[0].Text);
            Assert.Contains("Step 2:", result.Variants[0].Text);
            Assert.Contains("Step 3:", result.Variants[0].Text);
        }

        // ── Additional edge cases ────────────────────────────

        [Fact]
        public void ApplyTone_Formal_MultipleReplacements()
        {
            string result = _gen.ApplyTone("Look at the big things and find out stuff.", PromptTone.Formal);
            Assert.Contains("examine", result.ToLower());
            Assert.Contains("substantial", result.ToLower());
            Assert.Contains("items", result.ToLower());
        }

        [Fact]
        public void ApplyTone_Casual_MultipleReplacements()
        {
            string result = _gen.ApplyTone("Furthermore, provide the substantial information.", PromptTone.Casual);
            Assert.Contains("plus", result.ToLower());
            Assert.Contains("give", result.ToLower());
            Assert.Contains("big", result.ToLower());
        }

        [Fact]
        public void Generate_CustomPrefix_ContainsLabel()
        {
            var config = new VariantConfig
            {
                CustomPrefixes = new List<string> { "PREFIX" }
            };
            var result = _gen.Generate("Prompt text.", config);
            Assert.Single(result.Variants);
            Assert.Equal("prefix-1", result.Variants[0].Label);
        }

        [Fact]
        public void Generate_CustomSuffix_ContainsLabel()
        {
            var config = new VariantConfig
            {
                CustomSuffixes = new List<string> { "SUFFIX" }
            };
            var result = _gen.Generate("Prompt text.", config);
            Assert.Single(result.Variants);
            Assert.Equal("suffix-1", result.Variants[0].Label);
        }

        [Fact]
        public void ApplyInstructionStyle_Descriptive_QuestionWithCouldYou()
        {
            string prompt = "Could you review the code?";
            string result = _gen.ApplyInstructionStyle(prompt, InstructionStyle.Descriptive);
            Assert.StartsWith("Your task is to", result);
        }

        [Fact]
        public void Generate_Combinatorial_SingleAxis_Works()
        {
            var config = new VariantConfig
            {
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought, ReasoningStrategy.Calm },
                Combinatorial = true
            };
            var result = _gen.Generate("Explain this.", config);
            Assert.Equal(2, result.Variants.Count);
        }

        [Fact]
        public void Result_Original_PreservedExactly()
        {
            string prompt = "  Some   weird   spacing  ";
            var result = _gen.Generate(prompt, new VariantConfig
            {
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought }
            });
            Assert.Equal(prompt, result.Original);
        }

        [Fact]
        public void Generate_MaxPromptLength_Accepted()
        {
            string prompt = new string('a', PromptVariantGenerator.MaxPromptLength);
            var result = _gen.Generate(prompt, new VariantConfig
            {
                Strategies = new List<ReasoningStrategy> { ReasoningStrategy.ChainOfThought }
            });
            Assert.Single(result.Variants);
        }
    }
}
