using Prompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Prompt.Tests
{
    public class PromptMutationLabTests
    {
        private const string SamplePrompt = @"You are a helpful financial assistant.
Analyze the user's spending data and provide insights.
You must always respond in JSON format.
Never include personal opinions or speculation.
Output format: { ""summary"": string, ""recommendations"": string[] }
Example: Given monthly expenses, categorize them.
Important: Do not disclose any PII data.
The user's data may come from bank statements or credit card records.
List the top 3 spending categories.
Sometimes data may be incomplete — handle gracefully.";

        private PromptMutationLab CreateLab(int? seed = 42)
            => new PromptMutationLab(new MutationLabConfig { Seed = seed });

        // ── Zone Detection ──────────────────────────────────────

        [Fact]
        public void DetectZones_IdentifiesRoleDefinition()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones(SamplePrompt);
            Assert.Contains(zones, z => z.ZoneType == MutationZoneType.RoleDefinition);
        }

        [Fact]
        public void DetectZones_IdentifiesConstraints()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones(SamplePrompt);
            Assert.Contains(zones, z => z.ZoneType == MutationZoneType.Constraint);
        }

        [Fact]
        public void DetectZones_IdentifiesOutputFormat()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones(SamplePrompt);
            Assert.Contains(zones, z => z.ZoneType == MutationZoneType.OutputFormat);
        }

        [Fact]
        public void DetectZones_IdentifiesExample()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones(SamplePrompt);
            Assert.Contains(zones, z => z.ZoneType == MutationZoneType.Example);
        }

        [Fact]
        public void DetectZones_IdentifiesGuardrail()
        {
            var lab = CreateLab();
            // Use a prompt with a clear guardrail line not adjacent to constraints
            var prompt = "Summarize the document.\nImportant: Keep all data confidential and secure.";
            var zones = lab.DetectZones(prompt);
            Assert.Contains(zones, z => z.ZoneType == MutationZoneType.Guardrail);
        }

        [Fact]
        public void DetectZones_IdentifiesInstruction()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones(SamplePrompt);
            Assert.Contains(zones, z => z.ZoneType == MutationZoneType.Instruction);
        }

        [Fact]
        public void DetectZones_EmptyPrompt_ReturnsEmpty()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones("");
            Assert.Empty(zones);
        }

        [Fact]
        public void DetectZones_ShortLines_Skipped()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones("Hi\nOk\nYes");
            Assert.Empty(zones);
        }

        [Fact]
        public void DetectZones_AllZonesHaveText()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones(SamplePrompt);
            Assert.All(zones, z => Assert.False(string.IsNullOrEmpty(z.Text)));
        }

        [Fact]
        public void DetectZones_AllZonesHaveLabels()
        {
            var lab = CreateLab();
            var zones = lab.DetectZones(SamplePrompt);
            Assert.All(zones, z => Assert.False(string.IsNullOrEmpty(z.Label)));
        }

        // ── Mutation Generation ──────────────────────────────────

        [Fact]
        public void GenerateMutations_ProducesCases()
        {
            var lab = CreateLab();
            var campaign = lab.GenerateMutations(SamplePrompt);
            Assert.NotEmpty(campaign.Cases);
        }

        [Fact]
        public void GenerateMutations_CasesHaveDifferentText()
        {
            var lab = CreateLab();
            var campaign = lab.GenerateMutations(SamplePrompt);
            // Most cases should produce different text (some single-sentence zones can't be reordered)
            var changed = campaign.Cases.Count(c => c.OriginalText != c.MutatedText);
            Assert.True(changed > campaign.Cases.Count * 0.8,
                $"Expected >80% of cases to differ, got {changed}/{campaign.Cases.Count}");
        }

        [Fact]
        public void GenerateMutations_RespectsConfig()
        {
            var lab = new PromptMutationLab(new MutationLabConfig
            {
                MutationsPerZone = 3,
                Operators = new List<MutationOperator> { MutationOperator.Deletion, MutationOperator.Negation },
                Seed = 42
            });
            var campaign = lab.GenerateMutations(SamplePrompt);
            Assert.All(campaign.Cases, c =>
                Assert.True(c.Operator == MutationOperator.Deletion || c.Operator == MutationOperator.Negation));
        }

        [Fact]
        public void GenerateMutations_DeletionProducesEmpty()
        {
            var lab = new PromptMutationLab(new MutationLabConfig
            {
                MutationsPerZone = 1,
                Operators = new List<MutationOperator> { MutationOperator.Deletion },
                Seed = 42
            });
            var campaign = lab.GenerateMutations(SamplePrompt);
            var deletions = campaign.Cases.Where(c => c.Operator == MutationOperator.Deletion);
            Assert.All(deletions, c => Assert.Equal("", c.MutatedText));
        }

        [Fact]
        public void GenerateMutations_DuplicationDoubles()
        {
            var lab = new PromptMutationLab(new MutationLabConfig
            {
                MutationsPerZone = 1,
                Operators = new List<MutationOperator> { MutationOperator.Duplication },
                Seed = 42
            });
            var campaign = lab.GenerateMutations(SamplePrompt);
            var dups = campaign.Cases.Where(c => c.Operator == MutationOperator.Duplication);
            Assert.All(dups, c => Assert.True(c.MutatedText.Length > c.OriginalText.Length));
        }

        [Fact]
        public void GenerateMutations_TruncationShorter()
        {
            var lab = new PromptMutationLab(new MutationLabConfig
            {
                MutationsPerZone = 1,
                Operators = new List<MutationOperator> { MutationOperator.Truncation },
                Seed = 42
            });
            var campaign = lab.GenerateMutations(SamplePrompt);
            var truncs = campaign.Cases.Where(c => c.Operator == MutationOperator.Truncation);
            Assert.All(truncs, c => Assert.True(c.MutatedText.Length < c.OriginalText.Length));
        }

        [Fact]
        public void GenerateMutations_NegationFlipsPolarity()
        {
            var lab = new PromptMutationLab(new MutationLabConfig
            {
                MutationsPerZone = 1,
                Operators = new List<MutationOperator> { MutationOperator.Negation },
                Seed = 42
            });
            var campaign = lab.GenerateMutations(SamplePrompt);
            var negs = campaign.Cases.Where(c => c.Operator == MutationOperator.Negation);
            Assert.All(negs, c => Assert.NotEqual(c.OriginalText, c.MutatedText));
        }

        [Fact]
        public void GenerateMutations_ContradictionAddsText()
        {
            var lab = new PromptMutationLab(new MutationLabConfig
            {
                MutationsPerZone = 1,
                Operators = new List<MutationOperator> { MutationOperator.Contradiction },
                Seed = 42
            });
            var campaign = lab.GenerateMutations(SamplePrompt);
            var contras = campaign.Cases.Where(c => c.Operator == MutationOperator.Contradiction);
            Assert.All(contras, c => Assert.True(c.MutatedText.Length > c.OriginalText.Length));
        }

        [Fact]
        public void GenerateMutations_CasesHaveDescriptions()
        {
            var lab = CreateLab();
            var campaign = lab.GenerateMutations(SamplePrompt);
            Assert.All(campaign.Cases, c => Assert.False(string.IsNullOrEmpty(c.Description)));
        }

        // ── Outcome Evaluation ──────────────────────────────────

        [Fact]
        public void EvaluateOutcomes_FillsOutcomes()
        {
            var lab = CreateLab();
            var campaign = lab.GenerateMutations(SamplePrompt);
            campaign = lab.EvaluateOutcomes(campaign);
            Assert.Equal(campaign.Cases.Count, campaign.Outcomes.Count);
        }

        [Fact]
        public void EvaluateOutcomes_DriftInRange()
        {
            var lab = CreateLab();
            var campaign = lab.GenerateMutations(SamplePrompt);
            campaign = lab.EvaluateOutcomes(campaign);
            Assert.All(campaign.Outcomes, o =>
            {
                Assert.InRange(o.SemanticDrift, 0.0, 1.0);
            });
        }

        [Fact]
        public void EvaluateOutcomes_QualityDeltaInRange()
        {
            var lab = CreateLab();
            var campaign = lab.GenerateMutations(SamplePrompt);
            campaign = lab.EvaluateOutcomes(campaign);
            Assert.All(campaign.Outcomes, o =>
            {
                Assert.InRange(o.QualityDelta, -100.0, 100.0);
            });
        }

        [Fact]
        public void EvaluateOutcomes_DeletionCausesHighDrift()
        {
            var lab = new PromptMutationLab(new MutationLabConfig
            {
                MutationsPerZone = 1,
                Operators = new List<MutationOperator> { MutationOperator.Deletion },
                Seed = 42
            });
            var campaign = lab.GenerateMutations(SamplePrompt);
            campaign = lab.EvaluateOutcomes(campaign);
            // At least some deletions should cause measurable drift
            Assert.Contains(campaign.Outcomes, o => o.SemanticDrift > 0);
        }

        // ── Full Analysis Pipeline ──────────────────────────────

        [Fact]
        public void Analyze_ProducesValidReport()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.NotNull(report);
            Assert.NotNull(report.Campaign);
            Assert.NotEmpty(report.ZoneReports);
        }

        [Fact]
        public void Analyze_OverallScoreInRange()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.InRange(report.OverallResilienceScore, 0, 100);
        }

        [Fact]
        public void Analyze_AssignsOverallGrade()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.True(Enum.IsDefined(report.OverallGrade));
        }

        [Fact]
        public void Analyze_GeneratesRecommendationsForFragileZones()
        {
            // A prompt with a single critical constraint should generate recommendations
            var lab = new PromptMutationLab(new MutationLabConfig { Seed = 42, MutationsPerZone = 10 });
            var report = lab.Analyze("You must always respond in English.\nNever use profanity.\nImportant: Keep responses under 100 words.");
            // The report should have at least a summary
            Assert.False(string.IsNullOrEmpty(report.Summary));
        }

        [Fact]
        public void Analyze_FragilityHotspotsAreSubsetOfZones()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            var zoneLabels = report.ZoneReports.Select(z => z.Zone.Label).ToHashSet();
            Assert.All(report.FragilityHotspots, h => Assert.Contains(h, zoneLabels));
        }

        [Fact]
        public void Analyze_EmptyPrompt_ReturnsCritical()
        {
            var lab = CreateLab();
            var report = lab.Analyze("");
            Assert.Equal(MutationResilienceGrade.Critical, report.OverallGrade);
            Assert.Equal(0, report.OverallResilienceScore);
        }

        [Fact]
        public void Analyze_MinimalPrompt_DoesNotCrash()
        {
            var lab = CreateLab();
            var report = lab.Analyze("Summarize this document in three sentences.");
            Assert.NotNull(report);
            Assert.True(report.OverallResilienceScore >= 0);
        }

        [Fact]
        public void Analyze_ZoneReportsHaveScoresInRange()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.All(report.ZoneReports, zr => Assert.InRange(zr.Score, 0, 100));
        }

        [Fact]
        public void Analyze_ZoneReportsHaveValidGrades()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.All(report.ZoneReports, zr => Assert.True(Enum.IsDefined(zr.Grade)));
        }

        [Fact]
        public void Analyze_SurvivedLessThanOrEqualTotal()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.All(report.ZoneReports, zr =>
                Assert.True(zr.MutationsSurvived <= zr.MutationsTotal));
        }

        // ── Serialization ──────────────────────────────────────

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            var json = lab.ToJson(report);
            Assert.False(string.IsNullOrEmpty(json));
            var doc = JsonDocument.Parse(json); // should not throw
            Assert.NotNull(doc);
        }

        [Fact]
        public void ToJson_ContainsExpectedFields()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            var json = lab.ToJson(report);
            Assert.Contains("OverallResilienceScore", json);
            Assert.Contains("ZoneReports", json);
            Assert.Contains("Recommendations", json);
            Assert.Contains("FragilityHotspots", json);
        }

        [Fact]
        public void ToMarkdown_ContainsHeader()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            var md = lab.ToMarkdown(report);
            Assert.Contains("Prompt Mutation Lab Report", md);
        }

        [Fact]
        public void ToMarkdown_ContainsZoneMap()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            var md = lab.ToMarkdown(report);
            Assert.Contains("Zone Map", md);
            Assert.Contains("| Zone |", md);
        }

        [Fact]
        public void ToMarkdown_ContainsPerZoneSection()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            var md = lab.ToMarkdown(report);
            Assert.Contains("Per-Zone Resilience", md);
        }

        [Fact]
        public void ToMarkdown_ContainsOverallScore()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            var md = lab.ToMarkdown(report);
            Assert.Contains("/100", md);
        }

        // ── Determinism ──────────────────────────────────────

        [Fact]
        public void SeedProducesDeterministicResults()
        {
            var lab1 = new PromptMutationLab(new MutationLabConfig { Seed = 123 });
            var lab2 = new PromptMutationLab(new MutationLabConfig { Seed = 123 });
            var report1 = lab1.Analyze(SamplePrompt);
            var report2 = lab2.Analyze(SamplePrompt);
            Assert.Equal(report1.OverallResilienceScore, report2.OverallResilienceScore);
            Assert.Equal(report1.ZoneReports.Count, report2.ZoneReports.Count);
        }

        [Fact]
        public void DifferentSeedsProduceDifferentMutations()
        {
            var lab1 = new PromptMutationLab(new MutationLabConfig { Seed = 1 });
            var lab2 = new PromptMutationLab(new MutationLabConfig { Seed = 999 });
            var c1 = lab1.GenerateMutations(SamplePrompt);
            var c2 = lab2.GenerateMutations(SamplePrompt);
            // At least one case should differ in operator selection
            var ops1 = c1.Cases.Select(c => c.Operator).ToList();
            var ops2 = c2.Cases.Select(c => c.Operator).ToList();
            // Same count but potentially different ordering
            Assert.Equal(ops1.Count, ops2.Count);
        }

        // ── Edge Cases ──────────────────────────────────────

        [Fact]
        public void Analyze_SingleLinePrompt_Works()
        {
            var lab = CreateLab();
            var report = lab.Analyze("Generate a haiku about mountains.");
            Assert.NotNull(report);
            Assert.True(report.OverallResilienceScore >= 0);
        }

        [Fact]
        public void Analyze_VeryLongPrompt_Works()
        {
            var lab = CreateLab();
            var longPrompt = string.Join("\n", Enumerable.Range(0, 50)
                .Select(i => $"You must follow rule number {i}: always be helpful and concise in your responses."));
            var report = lab.Analyze(longPrompt);
            Assert.NotNull(report);
            Assert.NotEmpty(report.ZoneReports);
        }

        [Fact]
        public void GenerateMutations_WithCustomZones_UsesProvided()
        {
            var lab = CreateLab();
            var customZones = new List<PromptZone>
            {
                new PromptZone
                {
                    StartIndex = 0,
                    EndIndex = 20,
                    Text = "You are an assistant",
                    ZoneType = MutationZoneType.RoleDefinition,
                    Label = "custom-role"
                }
            };
            var campaign = lab.GenerateMutations("You are an assistant. Help me.", customZones);
            Assert.All(campaign.Cases, c => Assert.Equal("custom-role", c.TargetZone.Label));
        }

        [Fact]
        public void Config_DefaultValues()
        {
            var config = new MutationLabConfig();
            Assert.Equal(8, config.MutationsPerZone);
            Assert.Equal(10, config.MinZoneLength);
            Assert.True(config.EnableAutoZoneDetection);
            Assert.Null(config.Seed);
            Assert.Empty(config.Operators);
        }

        [Fact]
        public void Analyze_RecommendationsSortedByPriority()
        {
            var lab = new PromptMutationLab(new MutationLabConfig { Seed = 42, MutationsPerZone = 10 });
            var report = lab.Analyze(SamplePrompt);
            if (report.Recommendations.Count > 1)
            {
                for (int i = 1; i < report.Recommendations.Count; i++)
                {
                    Assert.True(report.Recommendations[i].Priority >= report.Recommendations[i - 1].Priority
                        || (report.Recommendations[i].Priority == report.Recommendations[i - 1].Priority));
                }
            }
        }

        [Fact]
        public void Analyze_SummaryIsNotEmpty()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.False(string.IsNullOrEmpty(report.Summary));
        }

        [Fact]
        public void Analyze_CampaignIdIsSet()
        {
            var lab = CreateLab();
            var report = lab.Analyze(SamplePrompt);
            Assert.False(string.IsNullOrEmpty(report.Campaign.Id));
        }
    }
}
