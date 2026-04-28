using Prompt;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Prompt.Tests
{
    public class PromptEvolutionEngineTests
    {
        private static PromptEvolutionEngine CreateEngine(EvolutionConfig? config = null)
            => new PromptEvolutionEngine(config);

        private const string SampleSeed = "Analyze the dataset and generate a summary report. List the top 10 anomalies. Provide recommendations for each finding.";
        private const string SampleSeed2 = "Review the code changes and identify potential bugs. Suggest improvements for performance and readability.";

        // ── Construction & Config ─────────────────────────────────

        [Fact]
        public void DefaultConfig_CreatesEngine()
        {
            var engine = CreateEngine();
            Assert.NotNull(engine);
        }

        [Fact]
        public void NegativePopulation_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CreateEngine(new EvolutionConfig { PopulationSize = 2 }));
        }

        [Fact]
        public void InvalidMutationRate_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CreateEngine(new EvolutionConfig { MutationRate = -0.1 }));
        }

        [Fact]
        public void InvalidCrossoverRate_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CreateEngine(new EvolutionConfig { CrossoverRate = 1.5 }));
        }

        // ── Evolution basics ──────────────────────────────────────

        [Fact]
        public void Evolve_SingleSeed_ReturnsResult()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 8, MaxGenerations = 5 });
            var result = engine.Evolve(SampleSeed);

            Assert.NotNull(result);
            Assert.NotNull(result.Champion);
            Assert.NotEmpty(result.Champion.Text);
            Assert.True(result.TotalGenerations > 0);
            Assert.True(result.History.Count > 0);
        }

        [Fact]
        public void Evolve_MultiSeed_ReturnsResult()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 8, MaxGenerations = 5 });
            var result = engine.Evolve(new List<string> { SampleSeed, SampleSeed2 });

            Assert.NotNull(result.Champion);
            Assert.True(result.TotalGenerations > 0);
        }

        [Fact]
        public void Evolve_EmptySeed_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.Evolve(""));
        }

        [Fact]
        public void Evolve_NullSeed_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.Evolve((string)null!));
        }

        [Fact]
        public void Evolve_EmptyList_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.Evolve(new List<string>()));
        }

        // ── Custom fitness function ───────────────────────────────

        [Fact]
        public void CustomFitnessFunction_IsUsed()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 6, MaxGenerations = 3 });
            engine.FitnessFunction = text => text.Contains("analyze", StringComparison.OrdinalIgnoreCase) ? 0.9 : 0.1;

            var result = engine.Evolve(SampleSeed);
            Assert.True(result.Champion.Fitness >= 0.0);
            Assert.True(result.Champion.Fitness <= 1.0);
        }

        [Fact]
        public void FitnessFunction_ClampedTo01()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 6, MaxGenerations = 2 });
            engine.FitnessFunction = _ => 5.0; // exceeds 1.0
            var result = engine.Evolve(SampleSeed);
            Assert.True(result.Champion.Fitness <= 1.0);
        }

        // ── Mutation strategies ───────────────────────────────────

        [Theory]
        [InlineData(MutationStrategy.WordShuffle)]
        [InlineData(MutationStrategy.SynonymReplace)]
        [InlineData(MutationStrategy.SentenceReorder)]
        [InlineData(MutationStrategy.ToneShift)]
        [InlineData(MutationStrategy.Compress)]
        [InlineData(MutationStrategy.Expand)]
        [InlineData(MutationStrategy.InstructionRephrase)]
        public void MutationStrategy_ProducesOutput(MutationStrategy strategy)
        {
            string result = PromptEvolutionEngine.ApplyMutation(SampleSeed, strategy);
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void SynonymReplace_ChangesText()
        {
            // "Analyze" has synonyms; run multiple times to hit a replacement
            string input = "Use the tool to find bugs.";
            bool changed = false;
            for (int i = 0; i < 20; i++)
            {
                var result = PromptEvolutionEngine.ApplyMutation(input, MutationStrategy.SynonymReplace);
                if (result != input) { changed = true; break; }
            }
            Assert.True(changed, "SynonymReplace should change text containing synonym-mapped words.");
        }

        [Fact]
        public void Compress_RemovesFillerWords()
        {
            string input = "You should just really analyze the obviously important data.";
            string result = PromptEvolutionEngine.ApplyMutation(input, MutationStrategy.Compress);
            Assert.DoesNotContain("just", result.ToLower().Split(' '));
            Assert.DoesNotContain("really", result.ToLower().Split(' '));
        }

        [Fact]
        public void Expand_AddsContent()
        {
            string result = PromptEvolutionEngine.ApplyMutation(SampleSeed, MutationStrategy.Expand);
            Assert.True(result.Length >= SampleSeed.Length, "Expand should not shorten text.");
        }

        [Fact]
        public void InstructionRephrase_AddsYouShould()
        {
            string input = "Analyze the data carefully.";
            string result = PromptEvolutionEngine.ApplyMutation(input, MutationStrategy.InstructionRephrase);
            Assert.Contains("You should", result);
        }

        // ── Crossover methods ─────────────────────────────────────

        [Fact]
        public void SinglePointCrossover_CombinesParents()
        {
            string result = PromptEvolutionEngine.SinglePointCrossover(SampleSeed, SampleSeed2);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void UniformCrossover_ProducesOutput()
        {
            string result = PromptEvolutionEngine.UniformCrossover(SampleSeed, SampleSeed2);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void SemanticBlendCrossover_ProducesOutput()
        {
            string result = PromptEvolutionEngine.SemanticBlendCrossover(SampleSeed, SampleSeed2);
            Assert.NotEmpty(result);
        }

        // ── Selection methods ─────────────────────────────────────

        [Theory]
        [InlineData(SelectionMethod.Tournament)]
        [InlineData(SelectionMethod.RouletteWheel)]
        [InlineData(SelectionMethod.RankBased)]
        [InlineData(SelectionMethod.Elitism)]
        public void SelectionMethod_ProducesResult(SelectionMethod method)
        {
            var config = new EvolutionConfig
            {
                PopulationSize = 8,
                MaxGenerations = 3,
                Selection = method
            };
            var engine = CreateEngine(config);
            var result = engine.Evolve(SampleSeed);

            Assert.NotNull(result.Champion);
            Assert.True(result.TotalGenerations > 0);
        }

        // ── Adaptive mutation ─────────────────────────────────────

        [Fact]
        public void AdaptiveMutationRate_IncreasesOnLowDiversity()
        {
            double adapted = PromptEvolutionEngine.AdaptMutationRate(0.1, 0.15);
            Assert.True(adapted > 0.15, "Low diversity should increase mutation rate.");
        }

        [Fact]
        public void AdaptiveMutationRate_DecreasesOnHighDiversity()
        {
            double adapted = PromptEvolutionEngine.AdaptMutationRate(0.9, 0.15);
            Assert.True(adapted < 0.15, "High diversity should decrease mutation rate.");
        }

        [Fact]
        public void AdaptiveMutationRate_StableInMidRange()
        {
            double adapted = PromptEvolutionEngine.AdaptMutationRate(0.5, 0.15);
            Assert.Equal(0.15, adapted);
        }

        // ── Elite preservation ────────────────────────────────────

        [Fact]
        public void EliteOrganisms_SurviveGenerations()
        {
            var engine = CreateEngine(new EvolutionConfig
            {
                PopulationSize = 8,
                MaxGenerations = 5,
                EliteCount = 2
            });
            var result = engine.Evolve(SampleSeed);

            // Best fitness should never decrease across consecutive generations
            for (int i = 1; i < result.History.Count; i++)
            {
                Assert.True(result.History[i].BestFitness >= result.History[i - 1].BestFitness - 0.001,
                    $"Elite preservation failed at gen {i}: {result.History[i].BestFitness} < {result.History[i - 1].BestFitness}");
            }
        }

        // ── Fitness improves ──────────────────────────────────────

        [Fact]
        public void Fitness_ImproveOrMaintainOverGenerations()
        {
            var engine = CreateEngine(new EvolutionConfig
            {
                PopulationSize = 12,
                MaxGenerations = 10,
                EliteCount = 2
            });
            var result = engine.Evolve(SampleSeed);

            Assert.True(result.Champion.Fitness >= result.History[0].BestFitness - 0.001,
                "Champion fitness should be >= initial best fitness.");
        }

        // ── Diversity ─────────────────────────────────────────────

        [Fact]
        public void Diversity_IsInZeroOneRange()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 8, MaxGenerations = 5 });
            var result = engine.Evolve(SampleSeed);

            foreach (var stats in result.History)
            {
                Assert.True(stats.Diversity >= 0.0 && stats.Diversity <= 1.0,
                    $"Diversity {stats.Diversity} out of range at gen {stats.Generation}.");
            }
        }

        [Fact]
        public void Diversity_IdenticalPopulation_IsZero()
        {
            var pop = Enumerable.Range(0, 5).Select(_ => new PromptOrganism { Text = "identical text" }).ToList();
            double diversity = PromptEvolutionEngine.CalculateDiversity(pop);
            Assert.Equal(0.0, diversity);
        }

        [Fact]
        public void Diversity_SingleOrganism_IsZero()
        {
            var pop = new List<PromptOrganism> { new() { Text = "solo" } };
            double diversity = PromptEvolutionEngine.CalculateDiversity(pop);
            Assert.Equal(0.0, diversity);
        }

        // ── GenerationStats tracking ──────────────────────────────

        [Fact]
        public void GenerationStats_AreTrackedAccurately()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 8, MaxGenerations = 5 });
            var result = engine.Evolve(SampleSeed);

            for (int i = 0; i < result.History.Count; i++)
            {
                var stats = result.History[i];
                Assert.Equal(i, stats.Generation);
                Assert.True(stats.BestFitness >= stats.AvgFitness);
                Assert.True(stats.AvgFitness >= stats.WorstFitness);
                Assert.Equal(8, stats.PopulationSize);
                Assert.NotEmpty(stats.BestOrganismId);
            }
        }

        // ── Summary ───────────────────────────────────────────────

        [Fact]
        public void Summary_IsNonEmpty()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 6, MaxGenerations = 3 });
            var result = engine.Evolve(SampleSeed);

            Assert.NotEmpty(result.Summary);
            Assert.Contains("Evolution Summary", result.Summary);
            Assert.Contains("Generations:", result.Summary);
            Assert.Contains("Champion:", result.Summary);
        }

        // ── Default fitness heuristic ─────────────────────────────

        [Fact]
        public void DefaultFitness_EmptyText_ReturnsZero()
        {
            double fitness = PromptEvolutionEngine.DefaultFitness("");
            Assert.Equal(0.0, fitness);
        }

        [Fact]
        public void DefaultFitness_GoodPrompt_ScoresHigher()
        {
            string good = "Analyze the top 10 performance metrics from the Q3 2025 dataset.\nList anomalies detected.\n- Include severity ratings.\n- Provide recommendations.";
            string poor = "do stuff";

            double goodScore = PromptEvolutionEngine.DefaultFitness(good);
            double poorScore = PromptEvolutionEngine.DefaultFitness(poor);

            Assert.True(goodScore > poorScore, $"Good prompt ({goodScore:F3}) should score higher than poor ({poorScore:F3}).");
        }

        // ── Early stopping ────────────────────────────────────────

        [Fact]
        public void EarlyStop_WhenFitnessTargetReached()
        {
            var engine = CreateEngine(new EvolutionConfig
            {
                PopulationSize = 6,
                MaxGenerations = 100,
                FitnessTargetThreshold = 0.01 // very low threshold, should stop early
            });
            var result = engine.Evolve(SampleSeed);

            Assert.True(result.ReachedTarget, "Should reach low fitness target.");
            Assert.True(result.TotalGenerations < 100, "Should stop before max generations.");
        }

        // ── Elapsed time ──────────────────────────────────────────

        [Fact]
        public void Elapsed_IsPositive()
        {
            var engine = CreateEngine(new EvolutionConfig { PopulationSize = 6, MaxGenerations = 3 });
            var result = engine.Evolve(SampleSeed);
            Assert.True(result.Elapsed.TotalMilliseconds > 0);
        }
    }
}
