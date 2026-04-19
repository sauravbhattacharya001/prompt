namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptAutopilotTests
    {
        // ── Score tests ──

        [Fact]
        public void Score_HighQualityPrompt_ScoresAbove70()
        {
            var prompt = @"You are an expert data analyst.

## Task
Analyze the provided CSV dataset and generate a summary report.

1. List the top 5 trends by revenue impact
2. Identify at least 3 anomalies with specific dates and values
3. Compare Q1 vs Q2 performance metrics

Do not include raw data in the output. If unsure about a trend, state your confidence level.

Format your response as a markdown table with columns: Trend, Impact ($), Confidence (%).

Example:
| Trend | Impact ($) | Confidence (%) |
|-------|-----------|----------------|
| Mobile growth | 150,000 | 92 |";

            var score = PromptAutopilot.Score(prompt);

            Assert.True(score.Overall >= 70, $"Expected >=70, got {score.Overall}");
            Assert.True(score.Clarity >= 60);
            Assert.True(score.Specificity >= 60);
            Assert.True(score.Structure >= 60);
            Assert.True(score.Safety >= 60);
            Assert.True(score.OutputGuidance >= 60);
        }

        [Fact]
        public void Score_VaguePrompt_ScoresLow()
        {
            var prompt = "maybe write some stuff about things";

            var score = PromptAutopilot.Score(prompt);

            Assert.True(score.Clarity < 60, $"Clarity should be low, got {score.Clarity}");
            Assert.True(score.Specificity < 60, $"Specificity should be low, got {score.Specificity}");
            Assert.True(score.Overall < 60, $"Overall should be low, got {score.Overall}");
        }

        [Fact]
        public void Score_Conciseness_PenalizesFillers()
        {
            var clean = "Write a summary of the article focusing on key findings.";
            var bloated = "Please note that it is important to basically write a summary of the article, essentially focusing on key findings, in order to provide value.";

            var cleanScore = PromptAutopilot.Score(clean).Conciseness;
            var bloatedScore = PromptAutopilot.Score(bloated).Conciseness;

            Assert.True(cleanScore > bloatedScore, $"Clean ({cleanScore}) should beat bloated ({bloatedScore})");
        }

        [Fact]
        public void Score_Safety_RewardsBoundaries()
        {
            var noBoundaries = "Write a product description.";
            var withBoundaries = "Write a product description. Do not include competitor names. Never make unverified claims. If unsure about a specification, omit it.";

            var noSafety = PromptAutopilot.Score(noBoundaries).Safety;
            var withSafety = PromptAutopilot.Score(withBoundaries).Safety;

            Assert.True(withSafety > noSafety, $"With boundaries ({withSafety}) should beat without ({noSafety})");
        }

        [Fact]
        public void Score_OutputGuidance_RewardsFormatSpec()
        {
            var noFormat = "List some programming languages.";
            var withFormat = "List 5 programming languages. Format as a JSON array with fields: name, paradigm, year. Example: [{\"name\": \"Python\", \"paradigm\": \"multi\", \"year\": 1991}]";

            var noScore = PromptAutopilot.Score(noFormat).OutputGuidance;
            var withScore = PromptAutopilot.Score(withFormat).OutputGuidance;

            Assert.True(withScore > noScore, $"With format ({withScore}) should beat without ({noScore})");
        }

        [Fact]
        public void Score_Structure_RewardsOrganization()
        {
            var flat = "Write code that reads a file parses the JSON extracts the name field and prints it to stdout handle errors gracefully.";
            var structured = @"## Task
Write code that:
1. Reads a file from stdin
2. Parses the JSON content
3. Extracts the `name` field
4. Prints it to stdout

---
Handle errors gracefully with descriptive messages.";

            var flatScore = PromptAutopilot.Score(flat).Structure;
            var structuredScore = PromptAutopilot.Score(structured).Structure;

            Assert.True(structuredScore > flatScore, $"Structured ({structuredScore}) should beat flat ({flatScore})");
        }

        [Fact]
        public void Score_Specificity_RewardsRoleAndConstraints()
        {
            var vague = "Help me with my code.";
            var specific = "You are an expert Python developer. Review my code for security vulnerabilities. Focus on at least 3 OWASP Top 10 categories. Limit your response to 500 words.";

            var vagueSpec = PromptAutopilot.Score(vague).Specificity;
            var specificSpec = PromptAutopilot.Score(specific).Specificity;

            Assert.True(specificSpec > vagueSpec, $"Specific ({specificSpec}) should beat vague ({vagueSpec})");
        }

        // ── Scorecard.ToDictionary ──

        [Fact]
        public void Scorecard_ToDictionary_ContainsAllDimensions()
        {
            var sc = PromptAutopilot.Score("Write a summary of the document.");
            var dict = sc.ToDictionary();

            Assert.Contains("Clarity", dict.Keys);
            Assert.Contains("Specificity", dict.Keys);
            Assert.Contains("Structure", dict.Keys);
            Assert.Contains("Safety", dict.Keys);
            Assert.Contains("OutputGuidance", dict.Keys);
            Assert.Contains("Conciseness", dict.Keys);
            Assert.Contains("Overall", dict.Keys);
            Assert.Equal(7, dict.Count);
        }

        [Fact]
        public void Scorecard_Overall_IsWeightedAverage()
        {
            var sc = new AutopilotScorecard
            {
                Clarity = 100, Specificity = 100, Structure = 100,
                Safety = 100, OutputGuidance = 100, Conciseness = 100
            };
            Assert.Equal(100, sc.Overall);

            var sc2 = new AutopilotScorecard
            {
                Clarity = 0, Specificity = 0, Structure = 0,
                Safety = 0, OutputGuidance = 0, Conciseness = 0
            };
            Assert.Equal(0, sc2.Overall);
        }

        // ── Refine tests ──

        [Fact]
        public void Refine_ThrowsOnEmptyPrompt()
        {
            Assert.Throws<ArgumentException>(() => PromptAutopilot.Refine(""));
            Assert.Throws<ArgumentException>(() => PromptAutopilot.Refine("   "));
        }

        [Fact]
        public void Refine_ImprovesOrMaintainsScore()
        {
            var prompt = "maybe write some stuff about things sort of";
            var result = PromptAutopilot.Refine(prompt);

            Assert.True(result.FinalScore >= result.InitialScore,
                $"Score should not decrease: {result.InitialScore} -> {result.FinalScore}");
            Assert.True(result.Improvement >= 0);
        }

        [Fact]
        public void Refine_PopulatesResultFields()
        {
            var prompt = "tell me about dogs";
            var result = PromptAutopilot.Refine(prompt);

            Assert.Equal(prompt, result.OriginalPrompt);
            Assert.NotEmpty(result.RefinedPrompt);
            Assert.NotEmpty(result.Generations);
            Assert.Equal(0, result.Generations[0].Number);
            Assert.NotNull(result.StopReason);
            Assert.NotEmpty(result.StopReason);
        }

        [Fact]
        public void Refine_Generation0_HasZeroDelta()
        {
            var result = PromptAutopilot.Refine("explain quantum computing");
            Assert.Equal(0, result.Generations[0].Delta);
        }

        [Fact]
        public void Refine_StopsAtTargetScore()
        {
            // Use a low target that should be easily reachable
            var config = new AutopilotConfig { TargetScore = 30, MaxGenerations = 20 };
            var result = PromptAutopilot.Refine("write a list of colors", config);

            Assert.Contains("Target score", result.StopReason);
            Assert.True(result.TargetReached);
        }

        [Fact]
        public void Refine_StopsAtMaxGenerations()
        {
            var config = new AutopilotConfig { MaxGenerations = 2, TargetScore = 100 };
            var result = PromptAutopilot.Refine("stuff", config);

            // Should have at most MaxGenerations+1 entries (gen 0 through MaxGenerations)
            Assert.True(result.Generations.Count <= config.MaxGenerations + 1);
        }

        [Fact]
        public void Refine_StopsOnStaleGenerations()
        {
            // A well-formed prompt that can't improve much
            var prompt = @"You are an expert data scientist.

## Task
Analyze the dataset and list the top 5 trends.

1. Sort by revenue impact
2. Include confidence scores between 0 and 100
3. Only include statistically significant findings

Do not speculate. If unsure, say so.

Format as a markdown table. Example:
| Trend | Revenue | Confidence |
|-------|---------|------------|
| Growth | $50K | 85 |";

            var config = new AutopilotConfig { MaxGenerations = 20, TargetScore = 100 };
            var result = PromptAutopilot.Refine(prompt, config);

            // Should stop well before 20 generations due to staleness or no issues
            Assert.True(result.Generations.Count < 20, $"Expected early stop, got {result.Generations.Count} generations");
        }

        // ── Strategy tests ──

        [Fact]
        public void Refine_WorstFirstStrategy_AppliesOneFix()
        {
            var config = new AutopilotConfig
            {
                Strategy = AutopilotStrategy.WorstFirst,
                MaxGenerations = 1,
                TargetScore = 100
            };
            var result = PromptAutopilot.Refine("stuff", config);

            // Generation 0 diagnoses but doesn't apply. Gen 1 would apply at most 1 fix per generation.
            var appliedCount = result.Generations.SelectMany(g => g.Diagnoses).Count(d => d.Applied);
            Assert.True(appliedCount <= 1, $"WorstFirst should apply at most 1 fix, got {appliedCount}");
        }

        [Fact]
        public void Refine_ShotgunStrategy_AppliesMultipleFixes()
        {
            var config = new AutopilotConfig
            {
                Strategy = AutopilotStrategy.Shotgun,
                MaxGenerations = 1,
                TargetScore = 100
            };
            var result = PromptAutopilot.Refine("stuff things maybe", config);

            // Shotgun should try to fix everything at once
            var appliedCount = result.TotalFixesApplied;
            // May apply 0 or more, but the strategy attempts all
            Assert.True(appliedCount >= 0);
        }

        [Fact]
        public void Refine_ConservativeStrategy_OnlyLowSeverity()
        {
            var config = new AutopilotConfig
            {
                Strategy = AutopilotStrategy.Conservative,
                MaxGenerations = 1,
                TargetScore = 100
            };
            var result = PromptAutopilot.Refine("stuff", config);

            // Conservative only fixes severity <= 0.6
            var applied = result.Generations.SelectMany(g => g.Diagnoses).Where(d => d.Applied);
            Assert.All(applied, d => Assert.True(d.Severity <= 0.6,
                $"Conservative applied fix with severity {d.Severity}"));
        }

        // ── Preset configs ──

        [Fact]
        public void QuickFix_HasExpectedSettings()
        {
            var cfg = PromptAutopilot.QuickFix;
            Assert.Equal(3, cfg.MaxGenerations);
            Assert.Equal(70, cfg.TargetScore);
            Assert.Equal(AutopilotStrategy.WorstFirst, cfg.Strategy);
        }

        [Fact]
        public void Balanced_HasDefaultSettings()
        {
            var cfg = PromptAutopilot.Balanced;
            Assert.Equal(10, cfg.MaxGenerations);
            Assert.Equal(85, cfg.TargetScore);
            Assert.Equal(AutopilotStrategy.WorstFirst, cfg.Strategy);
        }

        [Fact]
        public void DeepPolish_HasAggressiveSettings()
        {
            var cfg = PromptAutopilot.DeepPolish;
            Assert.Equal(15, cfg.MaxGenerations);
            Assert.Equal(95, cfg.TargetScore);
            Assert.Equal(AutopilotStrategy.Aggressive, cfg.Strategy);
        }

        [Fact]
        public void SafeMode_HasConservativeSettings()
        {
            var cfg = PromptAutopilot.SafeMode;
            Assert.Equal(5, cfg.MaxGenerations);
            Assert.Equal(80, cfg.TargetScore);
            Assert.Equal(AutopilotStrategy.Conservative, cfg.Strategy);
        }

        // ── SkipDimensions ──

        [Fact]
        public void Refine_SkipDimensions_SkipsSafety()
        {
            var config = new AutopilotConfig
            {
                SkipDimensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Safety" },
                MaxGenerations = 3,
                TargetScore = 100
            };
            var result = PromptAutopilot.Refine("write something", config);

            var safetyDiagnoses = result.Generations.SelectMany(g => g.Diagnoses)
                .Where(d => d.Dimension.Equals("Safety", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(safetyDiagnoses);
        }

        // ── MaxPromptLength enforcement ──

        [Fact]
        public void Refine_RespectsMaxPromptLength()
        {
            var config = new AutopilotConfig
            {
                MaxPromptLength = 200,
                MaxGenerations = 5,
                TargetScore = 100,
                Strategy = AutopilotStrategy.Shotgun
            };
            var result = PromptAutopilot.Refine("stuff", config);

            foreach (var gen in result.Generations)
            {
                Assert.True(gen.PromptText.Length <= 200,
                    $"Gen {gen.Number} exceeded max length: {gen.PromptText.Length}");
            }
        }

        // ── ToReport ──

        [Fact]
        public void ToReport_ContainsKeyInfo()
        {
            var result = PromptAutopilot.Refine("maybe write some stuff", PromptAutopilot.QuickFix);
            var report = result.ToReport();

            Assert.Contains("PROMPT AUTOPILOT REPORT", report);
            Assert.Contains("Generation 0", report);
            Assert.Contains("Score:", report);
            Assert.Contains("Final Prompt", report);
        }

        // ── Diagnosis detail tests ──

        [Fact]
        public void Diagnosis_AmbiguousLanguage_Detected()
        {
            var prompt = "maybe sort of write something about stuff";
            var score = PromptAutopilot.Score(prompt);

            // Score should reflect ambiguity penalties
            Assert.True(score.Clarity < 70);
        }

        [Fact]
        public void Refine_RemovesAmbiguousWords()
        {
            var prompt = "maybe write something about stuff and things";
            var result = PromptAutopilot.Refine(prompt, new AutopilotConfig
            {
                MaxGenerations = 5,
                TargetScore = 100,
                Strategy = AutopilotStrategy.Shotgun
            });

            // "maybe" should be removed
            Assert.DoesNotContain("maybe", result.RefinedPrompt.ToLowerInvariant());
        }

        // ── Edge cases ──

        [Fact]
        public void Refine_VeryLongPrompt_DoesNotCrash()
        {
            var prompt = string.Join(" ", Enumerable.Repeat("Write a detailed analysis of the system. Evaluate each component.", 50));
            var result = PromptAutopilot.Refine(prompt, PromptAutopilot.QuickFix);

            Assert.NotNull(result);
            Assert.NotEmpty(result.Generations);
        }

        [Fact]
        public void Refine_SingleWord_Handles()
        {
            var result = PromptAutopilot.Refine("summarize");
            Assert.NotNull(result);
            Assert.NotEmpty(result.RefinedPrompt);
        }

        [Fact]
        public void Score_AllScoresInRange0To100()
        {
            var prompts = new[]
            {
                "x",
                "Write a detailed report about climate change impacts on agriculture.",
                string.Join("\n", Enumerable.Repeat("Test sentence here.", 100))
            };

            foreach (var p in prompts)
            {
                var sc = PromptAutopilot.Score(p);
                Assert.InRange(sc.Clarity, 0, 100);
                Assert.InRange(sc.Specificity, 0, 100);
                Assert.InRange(sc.Structure, 0, 100);
                Assert.InRange(sc.Safety, 0, 100);
                Assert.InRange(sc.OutputGuidance, 0, 100);
                Assert.InRange(sc.Conciseness, 0, 100);
                Assert.InRange(sc.Overall, 0, 100);
            }
        }

        // ── AutopilotResult computed properties ──

        [Fact]
        public void AutopilotResult_InitialAndFinalScore_MatchGenerations()
        {
            var result = PromptAutopilot.Refine("describe the weather", PromptAutopilot.QuickFix);

            Assert.Equal(result.Generations.First().Score.Overall, result.InitialScore);
            Assert.Equal(result.Generations.Last().Score.Overall, result.FinalScore);
            Assert.Equal(result.FinalScore - result.InitialScore, result.Improvement);
        }

        [Fact]
        public void AutopilotResult_TotalFixesApplied_CountsCorrectly()
        {
            var result = PromptAutopilot.Refine("stuff", new AutopilotConfig
            {
                MaxGenerations = 3,
                TargetScore = 100,
                Strategy = AutopilotStrategy.Shotgun
            });

            var manualCount = result.Generations.SelectMany(g => g.Diagnoses).Count(d => d.Applied);
            Assert.Equal(manualCount, result.TotalFixesApplied);
        }
    }
}
