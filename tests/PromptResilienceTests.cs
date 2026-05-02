namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class PromptResilienceTests
{
    private const string SamplePrompt =
        "Explain the benefits of microservices. " +
        "List three architectural patterns. " +
        "Always provide concrete examples. " +
        "Never include speculation.";

    // ── Construction ────────────────────────────────────────

    [Fact]
    public void Ctor_DefaultSeed_CreatesInstance()
    {
        var sut = new PromptResilience();
        Assert.NotNull(sut);
    }

    [Fact]
    public void Ctor_ExplicitSeed_CreatesInstance()
    {
        var sut = new PromptResilience(seed: 42);
        Assert.NotNull(sut);
    }

    // ── Analyze – basics ────────────────────────────────────

    [Fact]
    public void Analyze_DefaultConfig_ProducesReport()
    {
        var sut = new PromptResilience(seed: 1);
        var report = sut.Analyze(SamplePrompt);

        Assert.NotNull(report);
        Assert.Equal(SamplePrompt, report.OriginalPrompt);
        Assert.True(report.OverallScore >= 0 && report.OverallScore <= 1.0);
        Assert.True(report.TotalTrials > 0);
        Assert.NotEmpty(report.Dimensions);
        Assert.NotEmpty(report.Trials);
        Assert.NotEmpty(report.KeyTokens);
    }

    [Fact]
    public void Analyze_SameSeed_Deterministic()
    {
        var a = new PromptResilience(seed: 99).Analyze(SamplePrompt);
        var b = new PromptResilience(seed: 99).Analyze(SamplePrompt);

        Assert.Equal(a.OverallScore, b.OverallScore, 6);
        Assert.Equal(a.TotalTrials, b.TotalTrials);
    }

    [Fact]
    public void Analyze_ConfigSeedOverridesCtorSeed()
    {
        var cfg = new ResilienceConfig { Seed = 42, TrialsPerType = 2 };
        var a = new PromptResilience(seed: 1).Analyze(SamplePrompt, cfg);

        cfg = new ResilienceConfig { Seed = 42, TrialsPerType = 2 };
        var b = new PromptResilience(seed: 999).Analyze(SamplePrompt, cfg);

        Assert.Equal(a.OverallScore, b.OverallScore, 6);
    }

    // ── Analyze – all perturbation types present ────────────

    [Fact]
    public void Analyze_Standard_AllPerturbationTypesCovered()
    {
        var sut = new PromptResilience(seed: 7);
        var report = sut.Analyze(SamplePrompt);

        var testedTypes = report.Dimensions.Select(d => d.Type).ToHashSet();
        foreach (var pt in Enum.GetValues<PerturbationType>())
            Assert.Contains(pt, testedTypes);
    }

    // ── Analyze – specific perturbation config ──────────────

    [Fact]
    public void Analyze_TypoOnly_OnlyTypoDimension()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.Typo },
            TrialsPerType = 3,
            Seed = 10
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);

        Assert.Single(report.Dimensions);
        Assert.Equal(PerturbationType.Typo, report.Dimensions[0].Type);
        Assert.Equal(3, report.Dimensions[0].TrialCount);
    }

    [Fact]
    public void Analyze_TruncationTrials_MatchTruncationLevels()
    {
        var levels = new List<double> { 0.10, 0.50, 0.90 };
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.Truncation },
            TruncationLevels = levels,
            Seed = 5
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);

        Assert.Equal(levels.Count, report.Trials.Count);
        Assert.All(report.Trials, t => Assert.Equal(PerturbationType.Truncation, t.Type));
    }

    [Fact]
    public void Analyze_InjectionProbe_MutatedContainsPayload()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.InjectionProbe },
            TrialsPerType = 1,
            Seed = 3
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);

        Assert.Single(report.Trials);
        // Injection adds text, so mutated should be longer
        Assert.True(report.Trials[0].MutatedPrompt.Length > SamplePrompt.Length);
    }

    // ── Key tokens ──────────────────────────────────────────

    [Fact]
    public void Analyze_ExtraKeyTokens_IncludedInReport()
    {
        var cfg = new ResilienceConfig
        {
            ExtraKeyTokens = new() { "foobar123" },
            TrialsPerType = 1,
            Seed = 1
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);

        Assert.Contains("foobar123", report.KeyTokens);
    }

    [Fact]
    public void Analyze_KeyTokens_ExcludeStopWords()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);

        // Common stop words should not appear in key tokens
        var stopWords = new[] { "the", "and", "is", "of", "in" };
        foreach (var sw in stopWords)
            Assert.DoesNotContain(sw, report.KeyTokens);
    }

    // ── Grading ─────────────────────────────────────────────

    [Fact]
    public void Analyze_OverallGrade_MatchesScore()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);

        if (report.OverallScore >= 0.80)
            Assert.Equal(ResilienceGrade.Robust, report.OverallGrade);
        else if (report.OverallScore >= 0.50)
            Assert.Equal(ResilienceGrade.Moderate, report.OverallGrade);
        else
            Assert.Equal(ResilienceGrade.Fragile, report.OverallGrade);
    }

    [Fact]
    public void Analyze_DimensionGrade_MatchesRetention()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);

        foreach (var dim in report.Dimensions)
        {
            if (dim.AverageRetention >= 0.80)
                Assert.Equal(ResilienceGrade.Robust, dim.Grade);
            else if (dim.AverageRetention >= 0.50)
                Assert.Equal(ResilienceGrade.Moderate, dim.Grade);
            else
                Assert.Equal(ResilienceGrade.Fragile, dim.Grade);
        }
    }

    // ── Retention scores are bounded ────────────────────────

    [Fact]
    public void Analyze_AllTrials_RetentionBetween0And1()
    {
        var report = new PromptResilience(seed: 5).Analyze(SamplePrompt);

        foreach (var trial in report.Trials)
        {
            Assert.True(trial.IntentRetention >= 0.0,
                $"{trial.Type}: retention {trial.IntentRetention} < 0");
            Assert.True(trial.IntentRetention <= 1.0,
                $"{trial.Type}: retention {trial.IntentRetention} > 1");
        }
    }

    // ── Hardening ───────────────────────────────────────────

    [Fact]
    public void Analyze_GenerateHardenedTrue_ProducesHardenedPrompt()
    {
        var cfg = new ResilienceConfig { GenerateHardenedVersion = true, Seed = 1 };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);

        Assert.False(string.IsNullOrWhiteSpace(report.HardenedPrompt));
        Assert.NotEmpty(report.HardeningActions);
    }

    [Fact]
    public void Analyze_GenerateHardenedFalse_NoHardenedPrompt()
    {
        var cfg = new ResilienceConfig { GenerateHardenedVersion = false, Seed = 1 };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);

        Assert.Equal("", report.HardenedPrompt);
        Assert.Empty(report.HardeningActions);
    }

    // ── Presets ──────────────────────────────────────────────

    [Fact]
    public void Presets_Quick_LimitedTypes()
    {
        var cfg = ResiliencePresets.Quick;
        Assert.Equal(2, cfg.TrialsPerType);
        Assert.NotNull(cfg.EnabledPerturbations);
        Assert.Equal(4, cfg.EnabledPerturbations!.Count);
    }

    [Fact]
    public void Presets_Standard_AllTypes()
    {
        var cfg = ResiliencePresets.Standard;
        Assert.Equal(5, cfg.TrialsPerType);
        Assert.Null(cfg.EnabledPerturbations); // null = all types
    }

    [Fact]
    public void Presets_Adversarial_FocusedTypes()
    {
        var cfg = ResiliencePresets.Adversarial;
        Assert.Equal(10, cfg.TrialsPerType);
        Assert.NotNull(cfg.EnabledPerturbations);
        Assert.Contains(PerturbationType.InjectionProbe, cfg.EnabledPerturbations!);
    }

    [Fact]
    public void Presets_Comprehensive_HighTrials()
    {
        var cfg = ResiliencePresets.Comprehensive;
        Assert.Equal(8, cfg.TrialsPerType);
        Assert.True(cfg.GenerateHardenedVersion);
    }

    // ── Compare ─────────────────────────────────────────────

    [Fact]
    public void Compare_TwoPrompts_ProducesBothReports()
    {
        var sut = new PromptResilience(seed: 1);
        var (a, b, comparison) = sut.Compare(
            "Explain microservices briefly.",
            "You must always explain microservices in exactly three paragraphs with specific examples. Never deviate.");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.False(string.IsNullOrWhiteSpace(comparison));
        Assert.Contains("RESILIENCE COMPARISON", comparison);
    }

    [Fact]
    public void Compare_SamePrompt_SimilarScores()
    {
        var sut = new PromptResilience(seed: 1);
        var (a, b, _) = sut.Compare(SamplePrompt, SamplePrompt);

        // Same prompt with same seed should give identical scores
        Assert.Equal(a.OverallScore, b.OverallScore, 6);
    }

    // ── Report formatting ───────────────────────────────────

    [Fact]
    public void Report_ToText_ContainsExpectedSections()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);
        var text = report.ToText();

        Assert.Contains("PROMPT RESILIENCE REPORT", text);
        Assert.Contains("Overall Score", text);
        Assert.Contains("Dimension Breakdown", text);
    }

    [Fact]
    public void Report_ToJson_IsValidStructure()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);
        var json = report.ToJson();

        Assert.Contains("\"overallScore\"", json);
        Assert.Contains("\"dimensions\"", json);
        Assert.Contains("\"keyTokens\"", json);
    }

    // ── Perturbation-specific behavior ──────────────────────

    [Fact]
    public void Typo_ShortPrompt_StillProducesTrial()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.Typo },
            TrialsPerType = 1,
            Seed = 1
        };
        var report = new PromptResilience().Analyze("Hi", cfg);
        Assert.Single(report.Trials);
    }

    [Fact]
    public void SynonymSwap_NoSwappableWords_RetentionHigh()
    {
        // All technical gibberish — no synonyms in the dictionary
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.SynonymSwap },
            TrialsPerType = 1,
            Seed = 1
        };
        var report = new PromptResilience().Analyze("Zxqwv plmkj tgnbr", cfg);
        Assert.True(report.Trials[0].IntentRetention >= 0.9);
    }

    [Fact]
    public void InstructionReorder_SingleSentence_NoChange()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.InstructionReorder },
            TrialsPerType = 1,
            Seed = 1
        };
        var report = new PromptResilience().Analyze("Just one sentence here", cfg);
        Assert.Equal(1.0, report.Trials[0].IntentRetention);
        Assert.Contains("one sentence", report.Trials[0].Explanation);
    }

    [Fact]
    public void CaseFlip_MutatedPrompt_DifferentCasing()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.CaseFlip },
            TrialsPerType = 1,
            Seed = 5
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);
        // Mutated should differ (casing changes)
        Assert.NotEqual(SamplePrompt, report.Trials[0].MutatedPrompt);
    }

    [Fact]
    public void WhitespaceNoise_MutatedPrompt_ContainsExtraSpaces()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.WhitespaceNoise },
            TrialsPerType = 1,
            Seed = 3
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);
        // Mutated should be at least as long (only adds whitespace)
        Assert.True(report.Trials[0].MutatedPrompt.Length >= SamplePrompt.Length);
    }

    [Fact]
    public void StopWordRemoval_MutatedPrompt_Shorter()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.StopWordRemoval },
            TrialsPerType = 1,
            Seed = 1
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);
        // Removing stop words should make it shorter
        Assert.True(report.Trials[0].MutatedPrompt.Length <= SamplePrompt.Length);
    }

    [Fact]
    public void Duplication_MutatedPrompt_Longer()
    {
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.Duplication },
            TrialsPerType = 1,
            Seed = 1
        };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);
        // Duplicating a sentence makes it longer
        Assert.True(report.Trials[0].MutatedPrompt.Length > SamplePrompt.Length);
    }

    [Fact]
    public void VaguenessInjection_PromptWithMust_Replaces()
    {
        var prompt = "You must always provide exactly three examples. Never skip steps.";
        var cfg = new ResilienceConfig
        {
            EnabledPerturbations = new() { PerturbationType.VaguenessInjection },
            TrialsPerType = 1,
            Seed = 1
        };
        var report = new PromptResilience().Analyze(prompt, cfg);
        // Should have replaced at least some vague terms
        Assert.NotEqual(prompt, report.Trials[0].MutatedPrompt);
    }

    // ── Timestamp ───────────────────────────────────────────

    [Fact]
    public void Analyze_Timestamp_RecentUtc()
    {
        var before = DateTime.UtcNow;
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt,
            new ResilienceConfig { TrialsPerType = 1, Seed = 1 });
        var after = DateTime.UtcNow;

        Assert.True(report.Timestamp >= before);
        Assert.True(report.Timestamp <= after);
    }

    // ── Edge cases ──────────────────────────────────────────

    [Fact]
    public void Analyze_EmptyPrompt_NoException()
    {
        var cfg = new ResilienceConfig { TrialsPerType = 1, Seed = 1 };
        var report = new PromptResilience().Analyze("", cfg);
        Assert.NotNull(report);
    }

    [Fact]
    public void Analyze_VeryLongPrompt_Completes()
    {
        var longPrompt = string.Join(". ", Enumerable.Range(0, 100).Select(i => $"Instruction number {i} must be followed precisely"));
        var cfg = new ResilienceConfig { TrialsPerType = 1, Seed = 1 };
        var report = new PromptResilience().Analyze(longPrompt, cfg);

        Assert.True(report.TotalTrials > 0);
        Assert.True(report.OverallScore >= 0);
    }

    [Fact]
    public void Analyze_PromptWithSpecialCharacters_Completes()
    {
        var prompt = "Use <xml> tags & \"quotes\" in your response. Don't forget #hashtags and $pecial chars!";
        var cfg = new ResilienceConfig { TrialsPerType = 1, Seed = 1 };
        var report = new PromptResilience().Analyze(prompt, cfg);
        Assert.NotNull(report);
    }

    // ── Dimension recommendations ───────────────────────────

    [Fact]
    public void Analyze_FragileDimensions_HaveRecommendations()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);

        foreach (var dim in report.Dimensions.Where(d => d.Grade == ResilienceGrade.Fragile))
            Assert.False(string.IsNullOrEmpty(dim.Recommendation),
                $"Fragile dimension {dim.Type} should have a recommendation");
    }

    [Fact]
    public void Analyze_RobustDimensions_EmptyRecommendation()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);

        foreach (var dim in report.Dimensions.Where(d => d.Grade == ResilienceGrade.Robust))
            Assert.Equal("", dim.Recommendation);
    }

    // ── Trial explanations ──────────────────────────────────

    [Fact]
    public void Analyze_AllTrials_HaveExplanations()
    {
        var report = new PromptResilience(seed: 1).Analyze(SamplePrompt);

        foreach (var trial in report.Trials)
            Assert.False(string.IsNullOrWhiteSpace(trial.Explanation),
                $"Trial {trial.Type} should have an explanation");
    }

    // ── Hardened prompt contains original content ───────────

    [Fact]
    public void Analyze_HardenedPrompt_ContainsKeyTokens()
    {
        var cfg = new ResilienceConfig { GenerateHardenedVersion = true, Seed = 1 };
        var report = new PromptResilience().Analyze(SamplePrompt, cfg);

        if (!string.IsNullOrWhiteSpace(report.HardenedPrompt))
        {
            var hardenedLower = report.HardenedPrompt.ToLowerInvariant();
            // At least some key tokens should survive hardening
            int survived = report.KeyTokens.Count(k => hardenedLower.Contains(k));
            Assert.True(survived > 0, "Hardened prompt should retain key tokens");
        }
    }
}
