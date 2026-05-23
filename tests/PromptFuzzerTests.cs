namespace Prompt.Tests;

using System.Linq;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptFuzzer"/>.
/// </summary>
public class PromptFuzzerTests
{
    private const string SamplePrompt =
        "Summarize the following article in three concise bullet points.";

    [Fact]
    public void Fuzz_NullPrompt_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => PromptFuzzer.Fuzz(null!));
    }

    [Fact]
    public void Fuzz_EmptyPrompt_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => PromptFuzzer.Fuzz(""));
    }

    [Fact]
    public void Fuzz_WhitespacePrompt_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => PromptFuzzer.Fuzz("   \t\n  "));
    }

    [Fact]
    public void Fuzz_DefaultOptions_ProducesTenVariants()
    {
        var result = PromptFuzzer.Fuzz(SamplePrompt);
        Assert.Equal(10, result.Variants.Count);
        Assert.Equal(SamplePrompt, result.Original);
    }

    [Fact]
    public void Fuzz_CustomCount_RespectsCount()
    {
        var result = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions { Count = 25, Seed = 7 });
        Assert.Equal(25, result.Variants.Count);
    }

    [Fact]
    public void Fuzz_ZeroCount_ProducesNoVariants()
    {
        var result = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions { Count = 0, Seed = 1 });
        Assert.Empty(result.Variants);
        Assert.Equal(100.0, result.AverageSimilarity);
        Assert.Null(result.MostDivergent);
    }

    [Fact]
    public void Fuzz_SameSeed_IsDeterministic()
    {
        var opts = new FuzzOptions { Count = 8, Intensity = 0.4, Seed = 1234 };
        var a = PromptFuzzer.Fuzz(SamplePrompt, opts);
        var b = PromptFuzzer.Fuzz(SamplePrompt, opts);
        Assert.Equal(a.Variants.Count, b.Variants.Count);
        for (int i = 0; i < a.Variants.Count; i++)
            Assert.Equal(a.Variants[i].Text, b.Variants[i].Text);
        Assert.Equal(a.AverageSimilarity, b.AverageSimilarity);
    }

    [Fact]
    public void Fuzz_DifferentSeeds_ProduceDifferentResults()
    {
        var a = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions { Count = 10, Intensity = 0.5, Seed = 1 });
        var b = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions { Count = 10, Intensity = 0.5, Seed = 999 });
        var aTexts = a.Variants.Select(v => v.Text).ToList();
        var bTexts = b.Variants.Select(v => v.Text).ToList();
        // It is astronomically unlikely that two seeds produce identical sequences
        Assert.NotEqual(aTexts, bTexts);
    }

    [Fact]
    public void Fuzz_ComputesEditDistanceAndSimilarity()
    {
        var result = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions
        {
            Count = 20,
            Intensity = 0.5,
            Seed = 42
        });

        foreach (var v in result.Variants)
        {
            Assert.True(v.EditDistance >= 0);
            Assert.InRange(v.SimilarityPercent, 0, 100);

            // Sanity: identical text => 100% similarity; different => <100%
            if (v.Text == SamplePrompt)
                Assert.Equal(100.0, v.SimilarityPercent);
            else
                Assert.True(v.SimilarityPercent < 100.0,
                    $"Expected <100% similarity for mutated variant: '{v.Text}'");
        }
    }

    [Fact]
    public void Fuzz_AverageSimilarity_IsMeanOfVariantSimilarities()
    {
        var result = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions
        {
            Count = 15,
            Intensity = 0.3,
            Seed = 11
        });

        double expected = System.Math.Round(result.Variants.Average(v => v.SimilarityPercent), 1);
        Assert.Equal(expected, result.AverageSimilarity);
    }

    [Fact]
    public void Fuzz_MostDivergent_HasLowestSimilarity()
    {
        var result = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions
        {
            Count = 12,
            Intensity = 0.7,
            Seed = 21
        });

        Assert.NotNull(result.MostDivergent);
        double min = result.Variants.Min(v => v.SimilarityPercent);
        Assert.Equal(min, result.MostDivergent!.SimilarityPercent);
    }

    [Fact]
    public void Fuzz_AppliedMutations_AreSubsetOfRequested()
    {
        var requested = new System.Collections.Generic.List<FuzzMutation>
        {
            FuzzMutation.Typo, FuzzMutation.WordDrop
        };
        var result = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions
        {
            Count = 30,
            Intensity = 0.5,
            Mutations = requested,
            Seed = 5
        });

        foreach (var v in result.Variants)
        {
            foreach (var m in v.AppliedMutations)
                Assert.Contains(m, requested);
        }
    }

    [Fact]
    public void Fuzz_IntensityClamped_DoesNotThrow()
    {
        // Negative and >1 intensities should be clamped silently
        var low = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions { Count = 3, Intensity = -5, Seed = 1 });
        var high = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions { Count = 3, Intensity = 99, Seed = 1 });
        Assert.Equal(3, low.Variants.Count);
        Assert.Equal(3, high.Variants.Count);
    }

    // ── FuzzSingle ───────────────────────────────────────────

    [Fact]
    public void FuzzSingle_NullOrEmpty_Throws()
    {
        Assert.Throws<System.ArgumentException>(
            () => PromptFuzzer.FuzzSingle(null!, FuzzMutation.Typo));
        Assert.Throws<System.ArgumentException>(
            () => PromptFuzzer.FuzzSingle("", FuzzMutation.Typo));
    }

    [Fact]
    public void FuzzSingle_SameSeed_IsDeterministic()
    {
        var a = PromptFuzzer.FuzzSingle(SamplePrompt, FuzzMutation.WordDrop, 0.5, seed: 100);
        var b = PromptFuzzer.FuzzSingle(SamplePrompt, FuzzMutation.WordDrop, 0.5, seed: 100);
        Assert.Equal(a, b);
    }

    [Fact]
    public void FuzzSingle_WordDrop_RemovesAtLeastOneWord()
    {
        var input = "one two three four five six seven eight nine ten";
        var mutated = PromptFuzzer.FuzzSingle(input, FuzzMutation.WordDrop, 0.5, seed: 7);
        var originalCount = input.Split(' ').Length;
        var mutatedCount = mutated.Split(' ').Length;
        Assert.True(mutatedCount < originalCount, $"Expected word drop, got '{mutated}'");
    }

    [Fact]
    public void FuzzSingle_Truncate_ShortensText()
    {
        var input = "This is a moderately long sentence used to verify truncation behaviour.";
        var mutated = PromptFuzzer.FuzzSingle(input, FuzzMutation.Truncate, 0.5, seed: 3);
        Assert.True(mutated.Length < input.Length);
        Assert.True(mutated.Length >= 1);
        Assert.StartsWith(mutated, input);
    }

    [Fact]
    public void FuzzSingle_WordDuplicate_IncreasesWordCount()
    {
        var input = "alpha beta gamma delta epsilon zeta eta theta iota kappa";
        var mutated = PromptFuzzer.FuzzSingle(input, FuzzMutation.WordDuplicate, 0.5, seed: 9);
        Assert.True(mutated.Split(' ').Length > input.Split(' ').Length);
    }

    [Fact]
    public void FuzzSingle_Noise_LeavesTextLongerOrEqual()
    {
        var input = "Translate this sentence to French please.";
        var mutated = PromptFuzzer.FuzzSingle(input, FuzzMutation.Noise, 0.8, seed: 17);
        Assert.True(mutated.Length >= input.Length);
    }

    [Fact]
    public void FuzzSingle_AdjacentSwap_SingleWord_IsNoop()
    {
        var input = "soloword";
        var mutated = PromptFuzzer.FuzzSingle(input, FuzzMutation.AdjacentSwap, 0.5, seed: 1);
        Assert.Equal(input, mutated);
    }

    [Fact]
    public void FuzzSingle_WordDrop_SingleWord_IsNoop()
    {
        var input = "onlyword";
        var mutated = PromptFuzzer.FuzzSingle(input, FuzzMutation.WordDrop, 0.5, seed: 1);
        Assert.Equal(input, mutated);
    }

    [Fact]
    public void FuzzSingle_CaseFlip_ChangesAtLeastOneLetterCase()
    {
        var input = "the quick brown fox jumps over the lazy dog";
        var mutated = PromptFuzzer.FuzzSingle(input, FuzzMutation.CaseFlip, 0.5, seed: 2);
        Assert.NotEqual(input, mutated);
        Assert.Equal(input.Length, mutated.Length);
        Assert.Equal(
            input.ToLowerInvariant(),
            mutated.ToLowerInvariant());
    }

    [Fact]
    public void Fuzz_AllMutationsByDefault_AreExercisedAcrossManyVariants()
    {
        var result = PromptFuzzer.Fuzz(SamplePrompt, new FuzzOptions
        {
            Count = 200,
            Intensity = 0.6,
            Seed = 2026
        });

        var allApplied = result.Variants
            .SelectMany(v => v.AppliedMutations)
            .Distinct()
            .ToList();

        // Across 200 variants with all 8 mutation types eligible, we expect
        // at least 6 distinct ones to surface in practice.
        Assert.True(allApplied.Count >= 6,
            $"Expected diverse mutation coverage, got: {string.Join(", ", allApplied)}");
    }
}
