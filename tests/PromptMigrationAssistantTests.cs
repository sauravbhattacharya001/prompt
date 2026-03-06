namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptMigrationAssistantTests
    {
        private readonly PromptMigrationAssistant _assistant = new();

        // ── Provider Detection ──

        [Fact]
        public void DetectProvider_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() => _assistant.DetectProvider(null!));
        }

        [Fact]
        public void DetectProvider_EmptyPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() => _assistant.DetectProvider("  "));
        }

        [Fact]
        public void DetectProvider_GenericPrompt_ReturnsGeneric()
        {
            var result = _assistant.DetectProvider("Summarize this text for me.");
            Assert.Equal(LlmProvider.Generic, result.Provider);
            Assert.Equal(0.0, result.Confidence);
        }

        [Fact]
        public void DetectProvider_OpenAI_ChatGPT()
        {
            var result = _assistant.DetectProvider("You are ChatGPT, a helpful assistant.");
            Assert.Equal(LlmProvider.OpenAI, result.Provider);
            Assert.True(result.Confidence > 0);
            Assert.Contains(result.Signals, s => s.Contains("ChatGPT"));
        }

        [Fact]
        public void DetectProvider_OpenAI_GPTModel()
        {
            var result = _assistant.DetectProvider("As GPT-4, analyze this code.");
            Assert.Equal(LlmProvider.OpenAI, result.Provider);
            Assert.Contains(result.Signals, s => s.Contains("GPT"));
        }

        [Fact]
        public void DetectProvider_Anthropic_Claude()
        {
            var result = _assistant.DetectProvider("You are Claude, made by Anthropic.");
            Assert.Equal(LlmProvider.Anthropic, result.Provider);
            Assert.True(result.Confidence > 0.3);
        }

        [Fact]
        public void DetectProvider_Anthropic_HumanMarker()
        {
            var result = _assistant.DetectProvider("Human: What is the meaning of life?\nAssistant: ");
            Assert.Equal(LlmProvider.Anthropic, result.Provider);
        }

        [Fact]
        public void DetectProvider_Anthropic_XmlTags()
        {
            var result = _assistant.DetectProvider(
                "<instructions>Analyze this</instructions>\n<context>Some data</context>");
            Assert.Equal(LlmProvider.Anthropic, result.Provider);
        }

        [Fact]
        public void DetectProvider_Google_Gemini()
        {
            var result = _assistant.DetectProvider("You are Gemini, a Google AI assistant.");
            Assert.Equal(LlmProvider.Google, result.Provider);
        }

        [Fact]
        public void DetectProvider_Meta_InstMarkers()
        {
            var result = _assistant.DetectProvider("[INST] Summarize this text [/INST]");
            Assert.Equal(LlmProvider.Meta, result.Provider);
        }

        [Fact]
        public void DetectProvider_Meta_SysMarkers()
        {
            var result = _assistant.DetectProvider("<<SYS>>\nYou are a helpful assistant.\n<</SYS>>");
            Assert.Equal(LlmProvider.Meta, result.Provider);
            Assert.True(result.Confidence >= 0.4);
        }

        [Fact]
        public void DetectProvider_Mistral()
        {
            var result = _assistant.DetectProvider("Using Mistral for code generation.");
            Assert.Equal(LlmProvider.Mistral, result.Provider);
        }

        [Fact]
        public void DetectProvider_MultipleSignals_HigherConfidence()
        {
            var single = _assistant.DetectProvider("You are ChatGPT.");
            var multi = _assistant.DetectProvider("You are ChatGPT, built by OpenAI, running GPT-4.");
            Assert.True(multi.Confidence >= single.Confidence);
            Assert.True(multi.Signals.Count > single.Signals.Count);
        }

        // ── Provider Profiles ──

        [Theory]
        [InlineData(LlmProvider.OpenAI)]
        [InlineData(LlmProvider.Anthropic)]
        [InlineData(LlmProvider.Google)]
        [InlineData(LlmProvider.Meta)]
        [InlineData(LlmProvider.Mistral)]
        [InlineData(LlmProvider.Generic)]
        public void GetProfile_AllProviders_ReturnValid(LlmProvider provider)
        {
            var profile = _assistant.GetProfile(provider);
            Assert.Equal(provider, profile.Provider);
            Assert.True(profile.MaxContextTokens > 0);
            Assert.NotEmpty(profile.PreferredRoleStyle);
            Assert.NotEmpty(profile.NotableConventions);
        }

        [Fact]
        public void GetProfile_Google_LargestContext()
        {
            var google = _assistant.GetProfile(LlmProvider.Google);
            var openai = _assistant.GetProfile(LlmProvider.OpenAI);
            Assert.True(google.MaxContextTokens > openai.MaxContextTokens);
        }

        // ── Analysis: Same Provider ──

        [Fact]
        public void Analyze_SameProvider_PerfectScore()
        {
            var report = _assistant.Analyze("Test prompt", LlmProvider.OpenAI, LlmProvider.OpenAI);
            Assert.Equal(100, report.CompatibilityScore);
            Assert.Equal("A+", report.Grade);
            Assert.Empty(report.Issues);
            Assert.Equal("Test prompt", report.MigratedPrompt);
        }

        [Fact]
        public void Analyze_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _assistant.Analyze(null!, LlmProvider.OpenAI, LlmProvider.Anthropic));
        }

        // ── Analysis: OpenAI → Anthropic ──

        [Fact]
        public void Analyze_OpenAI_To_Anthropic_DetectsChatGPT()
        {
            var report = _assistant.Analyze(
                "You are ChatGPT. Help the user.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(report.Issues.Any(i => i.Description.Contains("ChatGPT")));
            Assert.True(report.CompatibilityScore < 100);
        }

        [Fact]
        public void Analyze_OpenAI_To_Anthropic_DetectsOpenAIRef()
        {
            var report = _assistant.Analyze(
                "You are built by OpenAI.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(report.Issues.Any(i =>
                i.Description.Contains("OpenAI", StringComparison.OrdinalIgnoreCase)));
        }

        // ── Analysis: Anthropic → OpenAI ──

        [Fact]
        public void Analyze_Anthropic_To_OpenAI_DetectsXmlTags()
        {
            var report = _assistant.Analyze(
                "<instructions>Do X</instructions>\n<context>Y</context>\n<output>Z</output>",
                LlmProvider.Anthropic, LlmProvider.OpenAI);
            Assert.True(report.Issues.Any(i => i.Description.Contains("XML")));
        }

        [Fact]
        public void Analyze_Anthropic_To_OpenAI_DetectsThinkingBlocks()
        {
            var report = _assistant.Analyze(
                "Use <thinking> tags to reason before answering.",
                LlmProvider.Anthropic, LlmProvider.OpenAI);
            Assert.True(report.Issues.Any(i => i.Description.Contains("thinking")));
        }

        // ── Analysis: Meta → OpenAI ──

        [Fact]
        public void Analyze_Meta_To_OpenAI_DetectsInstMarkers()
        {
            var report = _assistant.Analyze(
                "[INST] Summarize this text [/INST]",
                LlmProvider.Meta, LlmProvider.OpenAI);
            Assert.True(report.Issues.Any(i => i.Description.Contains("[INST]")));
        }

        [Fact]
        public void Analyze_Meta_To_OpenAI_DetectsSysMarkers()
        {
            var report = _assistant.Analyze(
                "<<SYS>>\nYou are a helpful assistant.\n<</SYS>>\n[INST] Hello [/INST]",
                LlmProvider.Meta, LlmProvider.OpenAI);
            Assert.True(report.Issues.Any(i => i.Description.Contains("<<SYS>>")));
        }

        // ── Feature Compatibility ──

        [Fact]
        public void Analyze_JsonModeToUnsupported_Error()
        {
            var report = _assistant.Analyze(
                "Set response_format to json_object.",
                LlmProvider.OpenAI, LlmProvider.Meta);
            var jsonIssues = report.Issues.Where(i =>
                i.Category == MigrationCategory.UnsupportedFeature &&
                i.Description.Contains("JSON")).ToList();
            Assert.NotEmpty(jsonIssues);
            Assert.Contains(jsonIssues, i => i.Severity == MigrationSeverity.Error);
        }

        [Fact]
        public void Analyze_ToolCallingToUnsupported_Error()
        {
            var report = _assistant.Analyze(
                "Use function_call to invoke the search tool.",
                LlmProvider.OpenAI, LlmProvider.Generic);
            Assert.True(report.Issues.Any(i =>
                i.Category == MigrationCategory.ToolCalling &&
                i.Severity == MigrationSeverity.Error));
        }

        [Fact]
        public void Analyze_ImageToUnsupported_Error()
        {
            var report = _assistant.Analyze(
                "Analyze the image_url provided.",
                LlmProvider.OpenAI, LlmProvider.Generic);
            Assert.True(report.Issues.Any(i =>
                i.Category == MigrationCategory.UnsupportedFeature &&
                i.Description.Contains("image")));
        }

        // ── Token Limits ──

        [Fact]
        public void Analyze_VeryLongPrompt_TokenWarning()
        {
            // Generic has 8000 token limit
            var longPrompt = string.Join(" ", Enumerable.Repeat("word", 8000));
            var report = _assistant.Analyze(longPrompt, LlmProvider.OpenAI, LlmProvider.Generic);
            Assert.True(report.Issues.Any(i => i.Category == MigrationCategory.TokenLimit));
        }

        [Fact]
        public void Analyze_ShortPrompt_NoTokenWarning()
        {
            var report = _assistant.Analyze(
                "Summarize this text.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.False(report.Issues.Any(i => i.Category == MigrationCategory.TokenLimit));
        }

        // ── Scoring ──

        [Fact]
        public void Analyze_CleanPrompt_HighScore()
        {
            var report = _assistant.Analyze(
                "Summarize the following text.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(report.CompatibilityScore >= 90);
        }

        [Fact]
        public void Analyze_ProblematicPrompt_LowScore()
        {
            var prompt = "You are ChatGPT by OpenAI. Use response_format json_object. " +
                "Use function_call for tools. Analyze the image_url.";
            var report = _assistant.Analyze(prompt, LlmProvider.OpenAI, LlmProvider.Generic);
            Assert.True(report.CompatibilityScore < 60);
        }

        [Fact]
        public void Analyze_Grade_MatchesScore()
        {
            var report = _assistant.Analyze(
                "Summarize.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            if (report.CompatibilityScore >= 95) Assert.Equal("A+", report.Grade);
            else if (report.CompatibilityScore >= 90) Assert.Equal("A", report.Grade);
        }

        // ── Migration ──

        [Fact]
        public void Migrate_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _assistant.Migrate(null!, LlmProvider.OpenAI, LlmProvider.Anthropic));
        }

        [Fact]
        public void Migrate_SameProvider_NoChange()
        {
            var report = _assistant.Migrate(
                "Test prompt", LlmProvider.OpenAI, LlmProvider.OpenAI);
            Assert.Equal("Test prompt", report.MigratedPrompt);
            Assert.Equal(100, report.CompatibilityScore);
        }

        [Fact]
        public void Migrate_OpenAI_To_Anthropic_ReplacesChatGPT()
        {
            var report = _assistant.Migrate(
                "You are ChatGPT. Help the user.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Contains("Claude", report.MigratedPrompt);
            Assert.DoesNotContain("ChatGPT", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_OpenAI_To_Anthropic_ReplacesOpenAI()
        {
            var report = _assistant.Migrate(
                "Built by OpenAI.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Contains("Anthropic", report.MigratedPrompt);
            Assert.DoesNotContain("OpenAI", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_Anthropic_To_OpenAI_ReplacesClaude()
        {
            var report = _assistant.Migrate(
                "You are Claude. Be helpful.", LlmProvider.Anthropic, LlmProvider.OpenAI);
            Assert.Contains("ChatGPT", report.MigratedPrompt);
            Assert.DoesNotContain("Claude", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_Anthropic_To_Google_ReplacesClaudeWithGemini()
        {
            var report = _assistant.Migrate(
                "I am Claude.", LlmProvider.Anthropic, LlmProvider.Google);
            Assert.Contains("Gemini", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_Google_To_OpenAI_ReplacesGemini()
        {
            var report = _assistant.Migrate(
                "Powered by Gemini.", LlmProvider.Google, LlmProvider.OpenAI);
            Assert.Contains("ChatGPT", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_Meta_To_OpenAI_RemovesInstMarkers()
        {
            var report = _assistant.Migrate(
                "[INST] Summarize this text [/INST]", LlmProvider.Meta, LlmProvider.OpenAI);
            Assert.DoesNotContain("[INST]", report.MigratedPrompt);
            Assert.DoesNotContain("[/INST]", report.MigratedPrompt);
            Assert.Contains("Summarize", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_Meta_To_Anthropic_RemovesSysAndInst()
        {
            var prompt = "<<SYS>>\nYou are helpful.\n<</SYS>>\n[INST] Hello [/INST]";
            var report = _assistant.Migrate(prompt, LlmProvider.Meta, LlmProvider.Anthropic);
            Assert.DoesNotContain("<<SYS>>", report.MigratedPrompt);
            Assert.DoesNotContain("[INST]", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_Anthropic_To_OpenAI_ConvertsHumanToUser()
        {
            var report = _assistant.Migrate(
                "Human: What is AI?\nAssistant: AI is...",
                LlmProvider.Anthropic, LlmProvider.OpenAI);
            Assert.Contains("User:", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_HasAutoFixedIssues()
        {
            var report = _assistant.Migrate(
                "You are ChatGPT.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(report.AutoFixCount > 0);
        }

        [Fact]
        public void Migrate_ScoreImproves()
        {
            var prompt = "You are ChatGPT by OpenAI. Help users.";
            var analyze = _assistant.Analyze(prompt, LlmProvider.OpenAI, LlmProvider.Anthropic);
            var migrate = _assistant.Migrate(prompt, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(migrate.CompatibilityScore >= analyze.CompatibilityScore);
        }

        [Fact]
        public void Migrate_PreservesOriginal()
        {
            var prompt = "You are ChatGPT.";
            var report = _assistant.Migrate(prompt, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Equal(prompt, report.OriginalPrompt);
            Assert.NotEqual(prompt, report.MigratedPrompt);
        }

        // ── Auto-Migration ──

        [Fact]
        public void AutoMigrate_DetectsAndMigrates()
        {
            var report = _assistant.AutoMigrate(
                "You are ChatGPT, a helpful assistant by OpenAI.", LlmProvider.Anthropic);
            Assert.Equal(LlmProvider.OpenAI, report.SourceProvider);
            Assert.Equal(LlmProvider.Anthropic, report.TargetProvider);
            Assert.Contains("Claude", report.MigratedPrompt);
        }

        [Fact]
        public void AutoMigrate_GenericSource_StillWorks()
        {
            var report = _assistant.AutoMigrate(
                "Summarize this text for me.", LlmProvider.OpenAI);
            Assert.Equal(LlmProvider.Generic, report.SourceProvider);
            Assert.Equal(LlmProvider.OpenAI, report.TargetProvider);
        }

        // ── Batch Analysis ──

        [Fact]
        public void BatchAnalyze_NullPrompts_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _assistant.BatchAnalyze(null!, LlmProvider.OpenAI, LlmProvider.Anthropic));
        }

        [Fact]
        public void BatchAnalyze_EmptyList_ReturnsEmpty()
        {
            var batch = _assistant.BatchAnalyze(
                new List<string>(), LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Equal(0, batch.TotalPrompts);
        }

        [Fact]
        public void BatchAnalyze_MultiplePrompts()
        {
            var prompts = new[]
            {
                "You are ChatGPT. Help me.",
                "Summarize this text.",
                "Built by OpenAI. Use GPT-4."
            };
            var batch = _assistant.BatchAnalyze(
                prompts, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Equal(3, batch.TotalPrompts);
            Assert.True(batch.AverageScore > 0);
        }

        [Fact]
        public void BatchAnalyze_SkipsEmptyPrompts()
        {
            var prompts = new[] { "Valid prompt.", "", "  ", "Another valid." };
            var batch = _assistant.BatchAnalyze(
                prompts, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Equal(2, batch.TotalPrompts);
        }

        [Fact]
        public void BatchAnalyze_CategoryBreakdown()
        {
            var prompts = new[]
            {
                "You are ChatGPT by OpenAI.",
                "[INST] Test [/INST]"
            };
            var batch = _assistant.BatchAnalyze(
                prompts, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(batch.CategoryBreakdown.Count > 0);
        }

        [Fact]
        public void BatchAnalyze_ReadyNeedsWorkProblematic()
        {
            var prompts = new[]
            {
                "Summarize this text.",                        // Should be ready
                "You are ChatGPT by OpenAI using GPT-4.",     // Needs some work
            };
            var batch = _assistant.BatchAnalyze(
                prompts, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(batch.ReadyCount + batch.NeedsWorkCount + batch.ProblematicCount
                == batch.TotalPrompts);
        }

        // ── Compare Targets ──

        [Fact]
        public void CompareTargets_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _assistant.CompareTargets(null!, LlmProvider.OpenAI));
        }

        [Fact]
        public void CompareTargets_AllProviders()
        {
            var results = _assistant.CompareTargets(
                "You are ChatGPT.", LlmProvider.OpenAI);
            Assert.True(results.Count >= 4); // Anthropic, Google, Meta, Mistral
            Assert.False(results.ContainsKey(LlmProvider.OpenAI)); // Excluded source
            Assert.False(results.ContainsKey(LlmProvider.Generic)); // Excluded generic
        }

        [Fact]
        public void CompareTargets_SpecificTargets()
        {
            var results = _assistant.CompareTargets(
                "Test prompt.", LlmProvider.OpenAI,
                new[] { LlmProvider.Anthropic, LlmProvider.Google });
            Assert.Equal(2, results.Count);
            Assert.True(results.ContainsKey(LlmProvider.Anthropic));
            Assert.True(results.ContainsKey(LlmProvider.Google));
        }

        [Fact]
        public void CompareTargets_ScoresVary()
        {
            var results = _assistant.CompareTargets(
                "You are ChatGPT by OpenAI. Use function_call and image_url.",
                LlmProvider.OpenAI);
            // Generic should score lowest (no tools, no images)
            if (results.ContainsKey(LlmProvider.Generic))
            {
                var genericScore = results[LlmProvider.Generic].CompatibilityScore;
                Assert.True(results.Values.Any(r => r.CompatibilityScore > genericScore));
            }
        }

        // ── Text Report ──

        [Fact]
        public void ToTextReport_ContainsEssentials()
        {
            var report = _assistant.Analyze(
                "You are ChatGPT.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            var text = report.ToTextReport();
            Assert.Contains("Migration Report", text);
            Assert.Contains("OpenAI", text);
            Assert.Contains("Anthropic", text);
            Assert.Contains("Compatibility:", text);
            Assert.Contains("Issues:", text);
        }

        [Fact]
        public void ToTextReport_ShowsMigratedWhenDifferent()
        {
            var report = _assistant.Migrate(
                "You are ChatGPT.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            var text = report.ToTextReport();
            Assert.Contains("Migrated Prompt:", text);
        }

        // ── Report Properties ──

        [Fact]
        public void Report_TokenEstimate_Positive()
        {
            var report = _assistant.Analyze(
                "Hello world test.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(report.TokenEstimate > 0);
        }

        [Fact]
        public void Report_IncludesProfiles()
        {
            var report = _assistant.Analyze(
                "Test.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.NotNull(report.SourceProfile);
            Assert.NotNull(report.TargetProfile);
            Assert.Equal(LlmProvider.OpenAI, report.SourceProfile!.Provider);
            Assert.Equal(LlmProvider.Anthropic, report.TargetProfile!.Provider);
        }

        [Fact]
        public void Report_IssueCountProperties()
        {
            var report = _assistant.Analyze(
                "You are ChatGPT by OpenAI.", LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Equal(report.Issues.Count, report.IssueCount);
            Assert.Equal(report.Issues.Count(i => i.Severity == MigrationSeverity.Error),
                report.ErrorCount);
            Assert.Equal(report.Issues.Count(i => i.Severity == MigrationSeverity.Warning),
                report.WarningCount);
        }

        // ── Edge Cases ──

        [Fact]
        public void Migrate_PreservesNonProviderContent()
        {
            var prompt = "You are ChatGPT. Calculate 2 + 2. Return the result.";
            var report = _assistant.Migrate(prompt, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.Contains("Calculate 2 + 2", report.MigratedPrompt);
            Assert.Contains("Return the result", report.MigratedPrompt);
        }

        [Fact]
        public void Migrate_MultipleReplacements()
        {
            var prompt = "ChatGPT is built by OpenAI. ChatGPT uses GPT-4.";
            var report = _assistant.Migrate(prompt, LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.DoesNotContain("ChatGPT", report.MigratedPrompt);
            Assert.DoesNotContain("OpenAI", report.MigratedPrompt);
            Assert.Contains("Claude", report.MigratedPrompt);
            Assert.Contains("Anthropic", report.MigratedPrompt);
        }

        [Fact]
        public void Analyze_GenericPrompt_HighCompatibility()
        {
            var report = _assistant.Analyze(
                "Please summarize the following article in three bullet points.",
                LlmProvider.OpenAI, LlmProvider.Anthropic);
            Assert.True(report.CompatibilityScore >= 90);
        }

        [Fact]
        public void Migrate_CleanupExtraWhitespace()
        {
            var prompt = "[INST]\n\n\n\nHello\n\n\n\n[/INST]";
            var report = _assistant.Migrate(prompt, LlmProvider.Meta, LlmProvider.OpenAI);
            Assert.DoesNotContain("\n\n\n", report.MigratedPrompt);
        }

        [Fact]
        public void DetectProvider_CaseInsensitive()
        {
            var result = _assistant.DetectProvider("You are CHATGPT.");
            Assert.Equal(LlmProvider.OpenAI, result.Provider);
        }

        [Fact]
        public void Analyze_Google_SystemInstruction_ToOthers()
        {
            var report = _assistant.Analyze(
                "Use system_instruction for the prompt.",
                LlmProvider.Google, LlmProvider.OpenAI);
            Assert.True(report.Issues.Any(i =>
                i.Category == MigrationCategory.ApiSyntax));
        }

        [Fact]
        public void Migrate_Meta_To_Google_RemovesAllMarkers()
        {
            var prompt = "<<SYS>>\nSystem prompt\n<</SYS>>\n[INST] Question [/INST]";
            var report = _assistant.Migrate(prompt, LlmProvider.Meta, LlmProvider.Google);
            Assert.DoesNotContain("<<SYS>>", report.MigratedPrompt);
            Assert.DoesNotContain("[INST]", report.MigratedPrompt);
            Assert.Contains("System prompt", report.MigratedPrompt);
            Assert.Contains("Question", report.MigratedPrompt);
        }
    }
}
