using System.Text.Json;
using Xunit;

namespace Prompt.Tests
{
    public class PromptSamplerConfigTests
    {
        // ── Fluent Builder ─────────────────────────────────────────────

        [Fact]
        public void FluentBuilder_SetsAllParameters()
        {
            var config = new PromptSamplerConfig()
                .WithTemperature(0.8)
                .WithTopP(0.9)
                .WithTopK(50)
                .WithMaxTokens(2048)
                .WithFrequencyPenalty(0.5)
                .WithPresencePenalty(0.3)
                .WithStopSequences("END", "---")
                .WithSeed(123)
                .WithLabel("TestConfig");

            Assert.Equal(0.8, config.Temperature);
            Assert.Equal(0.9, config.TopP);
            Assert.Equal(50, config.TopK);
            Assert.Equal(2048, config.MaxTokens);
            Assert.Equal(0.5, config.FrequencyPenalty);
            Assert.Equal(0.3, config.PresencePenalty);
            Assert.Equal(new[] { "END", "---" }, config.StopSequences);
            Assert.Equal(123, config.Seed);
            Assert.Equal("TestConfig", config.Label);
        }

        [Fact]
        public void FluentBuilder_ReturnsSameInstance()
        {
            var config = new PromptSamplerConfig();
            var returned = config.WithTemperature(0.5);
            Assert.Same(config, returned);
        }

        [Fact]
        public void DefaultConfig_HasNoParametersSet()
        {
            var config = new PromptSamplerConfig();
            Assert.Null(config.Temperature);
            Assert.Null(config.TopP);
            Assert.Null(config.TopK);
            Assert.Null(config.MaxTokens);
            Assert.Null(config.FrequencyPenalty);
            Assert.Null(config.PresencePenalty);
            Assert.Null(config.StopSequences);
            Assert.Null(config.Seed);
            Assert.Equal(0, config.SetParameterCount());
        }

        // ── Presets ────────────────────────────────────────────────────

        [Fact]
        public void Preset_Balanced()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Balanced);
            Assert.Equal(0.7, config.Temperature);
            Assert.Equal(1.0, config.TopP);
            Assert.Equal(2048, config.MaxTokens);
            Assert.Equal("Balanced", config.Label);
        }

        [Fact]
        public void Preset_Creative()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Creative);
            Assert.Equal(1.2, config.Temperature);
            Assert.True(config.PresencePenalty > 0);
            Assert.True(config.FrequencyPenalty > 0);
        }

        [Fact]
        public void Preset_Deterministic()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Deterministic);
            Assert.Equal(0.0, config.Temperature);
            Assert.Equal(42, config.Seed);
        }

        [Fact]
        public void Preset_Code()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Code);
            Assert.Equal(0.2, config.Temperature);
            Assert.NotNull(config.StopSequences);
            Assert.Contains("```", config.StopSequences);
        }

        [Fact]
        public void Preset_Precise()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Precise);
            Assert.Equal(0.1, config.Temperature);
            Assert.Equal(40, config.TopK);
        }

        [Fact]
        public void Preset_Brainstorm()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Brainstorm);
            Assert.Equal(0.9, config.Temperature);
            Assert.Equal(1.0, config.PresencePenalty);
        }

        [Fact]
        public void ListPresets_ReturnsAllSix()
        {
            var presets = PromptSamplerConfig.ListPresets();
            Assert.Equal(6, presets.Count);
            Assert.Contains(presets, p => p.Preset == SamplerPreset.Balanced);
            Assert.Contains(presets, p => p.Preset == SamplerPreset.Creative);
            Assert.Contains(presets, p => p.Preset == SamplerPreset.Deterministic);
            Assert.Contains(presets, p => p.Preset == SamplerPreset.Code);
            Assert.Contains(presets, p => p.Preset == SamplerPreset.Precise);
            Assert.Contains(presets, p => p.Preset == SamplerPreset.Brainstorm);
        }

        // ── Validation — Generic ───────────────────────────────────────

        [Fact]
        public void Validate_ValidConfig_NoIssues()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Balanced);
            var result = config.Validate();
            Assert.True(result.IsValid);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public void Validate_NegativeTemperature_Error()
        {
            var config = new PromptSamplerConfig().WithTemperature(-0.5);
            var result = config.Validate();
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Parameter == "temperature" && i.IsError);
        }

        [Fact]
        public void Validate_ExcessiveTemperature_Error()
        {
            var config = new PromptSamplerConfig().WithTemperature(2.5);
            var result = config.Validate();
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_TopPOutOfRange_Error()
        {
            var config = new PromptSamplerConfig().WithTopP(1.5);
            var result = config.Validate();
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Parameter == "top_p" && i.IsError);
        }

        [Fact]
        public void Validate_TopKBelowOne_Error()
        {
            var config = new PromptSamplerConfig().WithTopK(0);
            var result = config.Validate();
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_MaxTokensBelowOne_Error()
        {
            var config = new PromptSamplerConfig().WithMaxTokens(0);
            var result = config.Validate();
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_PenaltyOutOfRange_Error()
        {
            var config = new PromptSamplerConfig().WithFrequencyPenalty(3.0);
            var result = config.Validate();
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_EmptyStopSequence_Error()
        {
            var config = new PromptSamplerConfig().WithStopSequences("valid", "");
            var result = config.Validate();
            Assert.Contains(result.Issues, i => i.Parameter == "stop" && i.IsError);
        }

        [Fact]
        public void Validate_ZeroTempWithLowTopP_Warning()
        {
            var config = new PromptSamplerConfig().WithTemperature(0).WithTopP(0.5);
            var result = config.Validate();
            Assert.True(result.IsValid);
            Assert.Contains(result.Issues, i => i.Parameter == "temperature+top_p" && !i.IsError);
        }

        // ── Validation — OpenAI ────────────────────────────────────────

        [Fact]
        public void Validate_OpenAI_TopKWarning()
        {
            var config = new PromptSamplerConfig().WithTopK(50);
            var result = config.Validate(SamplerProvider.OpenAI);
            Assert.True(result.IsValid);
            Assert.Contains(result.Issues, i => i.Parameter == "top_k" && !i.IsError);
        }

        [Fact]
        public void Validate_OpenAI_TooManyStops_Error()
        {
            var config = new PromptSamplerConfig().WithStopSequences("a", "b", "c", "d", "e");
            var result = config.Validate(SamplerProvider.OpenAI);
            Assert.Contains(result.Issues, i => i.Parameter == "stop" && i.IsError);
        }

        // ── Validation — Anthropic ─────────────────────────────────────

        [Fact]
        public void Validate_Anthropic_TempAboveOne_Error()
        {
            var config = new PromptSamplerConfig().WithTemperature(1.5);
            var result = config.Validate(SamplerProvider.Anthropic);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Anthropic_PenaltiesWarning()
        {
            var config = new PromptSamplerConfig()
                .WithFrequencyPenalty(0.5)
                .WithPresencePenalty(0.5);
            var result = config.Validate(SamplerProvider.Anthropic);
            Assert.True(result.IsValid);
            var warnings = result.Warnings.ToList();
            Assert.True(warnings.Count >= 2);
        }

        // ── Validation — Gemini ────────────────────────────────────────

        [Fact]
        public void Validate_Gemini_SeedWarning()
        {
            var config = new PromptSamplerConfig().WithSeed(42);
            var result = config.Validate(SamplerProvider.Gemini);
            Assert.True(result.IsValid);
            Assert.Contains(result.Issues, i => i.Parameter == "seed" && !i.IsError);
        }

        [Fact]
        public void Validate_Gemini_TooManyStops_Error()
        {
            var config = new PromptSamplerConfig().WithStopSequences("a", "b", "c", "d", "e", "f");
            var result = config.Validate(SamplerProvider.Gemini);
            Assert.Contains(result.Issues, i => i.Parameter == "stop" && i.IsError);
        }

        // ── JSON Export ────────────────────────────────────────────────

        [Fact]
        public void ToJson_OpenAI_Format()
        {
            var config = new PromptSamplerConfig()
                .WithTemperature(0.7)
                .WithMaxTokens(1024)
                .WithStopSequences("END");
            var json = config.ToJson(SamplerProvider.OpenAI);
            var doc = JsonDocument.Parse(json);
            Assert.Equal(0.7, doc.RootElement.GetProperty("temperature").GetDouble());
            Assert.Equal(1024, doc.RootElement.GetProperty("max_tokens").GetInt32());
            Assert.True(doc.RootElement.TryGetProperty("stop", out _));
        }

        [Fact]
        public void ToJson_Anthropic_UsesStopSequences()
        {
            var config = new PromptSamplerConfig()
                .WithTemperature(0.5)
                .WithTopK(40)
                .WithMaxTokens(512)
                .WithStopSequences("END");
            var json = config.ToJson(SamplerProvider.Anthropic);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("stop_sequences", out _));
            Assert.True(doc.RootElement.TryGetProperty("top_k", out _));
            // Anthropic doesn't include penalties
            Assert.False(doc.RootElement.TryGetProperty("frequency_penalty", out _));
        }

        [Fact]
        public void ToJson_Gemini_CamelCase()
        {
            var config = new PromptSamplerConfig()
                .WithTemperature(0.5)
                .WithTopP(0.9)
                .WithTopK(40)
                .WithMaxTokens(1024)
                .WithStopSequences("END");
            var json = config.ToJson(SamplerProvider.Gemini);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("topP", out _));
            Assert.True(doc.RootElement.TryGetProperty("topK", out _));
            Assert.True(doc.RootElement.TryGetProperty("maxOutputTokens", out _));
            Assert.True(doc.RootElement.TryGetProperty("stopSequences", out _));
        }

        [Fact]
        public void ToJson_OmitsUnsetFields()
        {
            var config = new PromptSamplerConfig().WithTemperature(0.5);
            var json = config.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("temperature", out _));
            Assert.False(doc.RootElement.TryGetProperty("top_p", out _));
            Assert.False(doc.RootElement.TryGetProperty("max_tokens", out _));
        }

        [Fact]
        public void ToJson_Compact_NoIndent()
        {
            var config = new PromptSamplerConfig().WithTemperature(0.5);
            var json = config.ToJson(indented: false);
            Assert.DoesNotContain("\n", json);
        }

        // ── ToDictionary ───────────────────────────────────────────────

        [Fact]
        public void ToDictionary_OnlySetParams()
        {
            var config = new PromptSamplerConfig().WithTemperature(0.5).WithMaxTokens(100);
            var dict = config.ToDictionary();
            Assert.Equal(2, dict.Count);
            Assert.Equal(0.5, dict["temperature"]);
            Assert.Equal(100, dict["max_tokens"]);
        }

        // ── Clone ──────────────────────────────────────────────────────

        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var original = PromptSamplerConfig.FromPreset(SamplerPreset.Code);
            var clone = original.Clone();

            Assert.Equal(original.Temperature, clone.Temperature);
            Assert.Equal(original.TopP, clone.TopP);
            Assert.Equal(original.StopSequences, clone.StopSequences);
            Assert.Equal(original.Label, clone.Label);

            // Modify clone doesn't affect original
            clone.Temperature = 1.0;
            Assert.NotEqual(original.Temperature, clone.Temperature);
        }

        [Fact]
        public void Clone_DeepCopiesStopSequences()
        {
            var original = new PromptSamplerConfig().WithStopSequences("a", "b");
            var clone = original.Clone();
            clone.StopSequences!.Add("c");
            Assert.Equal(2, original.StopSequences!.Count);
            Assert.Equal(3, clone.StopSequences.Count);
        }

        // ── Diff ───────────────────────────────────────────────────────

        [Fact]
        public void Diff_IdenticalConfigs_NoDiffs()
        {
            var a = PromptSamplerConfig.FromPreset(SamplerPreset.Balanced);
            var b = PromptSamplerConfig.FromPreset(SamplerPreset.Balanced);
            var diffs = a.Diff(b);
            Assert.Empty(diffs);
        }

        [Fact]
        public void Diff_DifferentTemperature_Detected()
        {
            var a = new PromptSamplerConfig().WithTemperature(0.5);
            var b = new PromptSamplerConfig().WithTemperature(0.9);
            var diffs = a.Diff(b);
            Assert.Contains("temperature", diffs.Keys);
            Assert.Equal(0.5, diffs["temperature"].ThisValue);
            Assert.Equal(0.9, diffs["temperature"].OtherValue);
        }

        [Fact]
        public void Diff_NullVsSet_Detected()
        {
            var a = new PromptSamplerConfig();
            var b = new PromptSamplerConfig().WithTemperature(0.5);
            var diffs = a.Diff(b);
            Assert.Contains("temperature", diffs.Keys);
        }

        [Fact]
        public void Diff_DifferentStops_Detected()
        {
            var a = new PromptSamplerConfig().WithStopSequences("a");
            var b = new PromptSamplerConfig().WithStopSequences("a", "b");
            var diffs = a.Diff(b);
            Assert.Contains("stop", diffs.Keys);
        }

        [Fact]
        public void Diff_NullOther_Throws()
        {
            var config = new PromptSamplerConfig();
            Assert.Throws<ArgumentNullException>(() => config.Diff(null!));
        }

        // ── Merge ──────────────────────────────────────────────────────

        [Fact]
        public void Merge_OverwritesSetValues()
        {
            var baseConfig = PromptSamplerConfig.FromPreset(SamplerPreset.Balanced);
            var overlay = new PromptSamplerConfig().WithTemperature(0.3).WithMaxTokens(512);
            baseConfig.Merge(overlay);
            Assert.Equal(0.3, baseConfig.Temperature);
            Assert.Equal(512, baseConfig.MaxTokens);
            Assert.Equal(1.0, baseConfig.TopP); // kept from base
        }

        [Fact]
        public void Merge_ReturnsSameInstance()
        {
            var config = new PromptSamplerConfig();
            var result = config.Merge(new PromptSamplerConfig().WithTemperature(0.5));
            Assert.Same(config, result);
        }

        [Fact]
        public void Merge_NullOther_Throws()
        {
            var config = new PromptSamplerConfig();
            Assert.Throws<ArgumentNullException>(() => config.Merge(null!));
        }

        // ── Summary ────────────────────────────────────────────────────

        [Fact]
        public void ToSummary_IncludesLabel()
        {
            var config = PromptSamplerConfig.FromPreset(SamplerPreset.Creative);
            var summary = config.ToSummary();
            Assert.Contains("[Creative]", summary);
            Assert.Contains("temp=1.2", summary);
        }

        [Fact]
        public void ToSummary_EmptyConfig_ReturnsPlaceholder()
        {
            var config = new PromptSamplerConfig();
            Assert.Equal("(empty configuration)", config.ToSummary());
        }

        [Fact]
        public void ToSummary_IncludesStopCount()
        {
            var config = new PromptSamplerConfig().WithStopSequences("a", "b", "c");
            Assert.Contains("stops=3", config.ToSummary());
        }

        // ── SetParameterCount ──────────────────────────────────────────

        [Fact]
        public void SetParameterCount_CountsOnlySetParams()
        {
            var config = new PromptSamplerConfig()
                .WithTemperature(0.5)
                .WithMaxTokens(100);
            Assert.Equal(2, config.SetParameterCount());
        }

        [Fact]
        public void SetParameterCount_AllParams()
        {
            var config = new PromptSamplerConfig()
                .WithTemperature(0.5)
                .WithTopP(0.9)
                .WithTopK(40)
                .WithMaxTokens(1024)
                .WithFrequencyPenalty(0.1)
                .WithPresencePenalty(0.2)
                .WithStopSequences("x")
                .WithSeed(1);
            Assert.Equal(8, config.SetParameterCount());
        }

        // ── ValidationResult Properties ────────────────────────────────

        [Fact]
        public void ValidationResult_Errors_FiltersCorrectly()
        {
            var config = new PromptSamplerConfig()
                .WithTemperature(-1.0)
                .WithTopK(50);
            var result = config.Validate(SamplerProvider.OpenAI);
            Assert.True(result.Errors.Any());
            Assert.True(result.Warnings.Any());
        }

        [Fact]
        public void ValidationIssue_ToString_FormatsCorrectly()
        {
            var issue = new SamplerValidationIssue
            {
                Parameter = "temperature",
                Message = "Too high",
                IsError = true
            };
            Assert.Equal("[ERROR] temperature: Too high", issue.ToString());

            issue.IsError = false;
            Assert.Equal("[WARN] temperature: Too high", issue.ToString());
        }
    }
}
