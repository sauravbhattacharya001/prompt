namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptSupplyChainAuditorTests
    {
        private static PromptSupplyChainAuditor CreateBasicChain()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("sys-prompt", "System Prompt") { Type = SupplierType.SystemInstruction });
            auditor.AddSupplier(new PromptSupplier("few-shot", "Few-Shot Examples") { Type = SupplierType.FewShotExamples });
            auditor.AddSupplier(new PromptSupplier("context-db", "Database Context") { Type = SupplierType.ContextSource });
            auditor.AddSupplier(new PromptSupplier("user-input", "User Query") { Type = SupplierType.UserInput });
            auditor.AddConsumerRelationship("final-prompt", "sys-prompt");
            auditor.AddConsumerRelationship("final-prompt", "few-shot");
            auditor.AddConsumerRelationship("final-prompt", "context-db");
            auditor.AddConsumerRelationship("final-prompt", "user-input");
            return auditor;
        }

        [Fact]
        public void AddSupplier_ValidSupplier_Registers()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "Supplier 1"));
            Assert.Single(auditor.Suppliers);
            Assert.Equal("s1", auditor.Suppliers[0].Id);
        }

        [Fact]
        public void AddSupplier_Duplicate_Throws()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "Supplier 1"));
            Assert.Throws<ArgumentException>(() => auditor.AddSupplier(new PromptSupplier("s1", "Dup")));
        }

        [Fact]
        public void AddSupplier_Null_Throws()
        {
            var auditor = new PromptSupplyChainAuditor();
            Assert.Throws<ArgumentNullException>(() => auditor.AddSupplier(null!));
        }

        [Fact]
        public void PromptSupplier_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PromptSupplier("", "Name"));
        }

        [Fact]
        public void AddConsumerRelationship_Valid_Succeeds()
        {
            var auditor = CreateBasicChain();
            // No exception = success
            Assert.True(auditor.Suppliers.Count >= 4);
        }

        [Fact]
        public void AddConsumerRelationship_EmptyConsumer_Throws()
        {
            var auditor = new PromptSupplyChainAuditor();
            Assert.Throws<ArgumentException>(() => auditor.AddConsumerRelationship("", "s1"));
        }

        [Fact]
        public void AddConsumerRelationship_EmptySupplier_Throws()
        {
            var auditor = new PromptSupplyChainAuditor();
            Assert.Throws<ArgumentException>(() => auditor.AddConsumerRelationship("c1", ""));
        }

        [Fact]
        public void RecordReliability_ValidSupplier_Records()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "S1"));
            auditor.RecordReliability("s1", DateTimeOffset.UtcNow, true, 50.0);
            Assert.Single(auditor.Suppliers[0].ReliabilityHistory);
        }

        [Fact]
        public void RecordReliability_UnknownSupplier_Throws()
        {
            var auditor = new PromptSupplyChainAuditor();
            Assert.Throws<ArgumentException>(() => auditor.RecordReliability("unknown", DateTimeOffset.UtcNow, true));
        }

        [Fact]
        public void Audit_EmptyChain_Returns100Resilience()
        {
            var auditor = new PromptSupplyChainAuditor();
            var report = auditor.Audit();
            Assert.Equal(100, report.ResilienceScore);
        }

        [Fact]
        public void Audit_BasicChain_ProducesReport()
        {
            var auditor = CreateBasicChain();
            var report = auditor.Audit();
            Assert.True(report.TotalSuppliers == 4);
            Assert.True(report.ResilienceScore >= 0 && report.ResilienceScore <= 100);
        }

        [Fact]
        public void Audit_DetectsSoleSourceSuppliers()
        {
            var auditor = CreateBasicChain();
            var report = auditor.Audit();
            // sys-prompt, few-shot, context-db have no alternatives (user-input excluded)
            Assert.True(report.SinglePointsOfFailure.Count >= 2);
        }

        [Fact]
        public void Audit_DetectsFreshnessRisk()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("stale", "Stale Data")
            {
                Type = SupplierType.ContextSource,
                LastUpdated = DateTimeOffset.UtcNow.AddDays(-30),
                FreshnessThresholdHours = 24
            });
            var report = auditor.Audit();
            Assert.Contains(report.Findings, f => f.Category == SupplyRiskCategory.Freshness);
        }

        [Fact]
        public void Audit_NoFreshnessRisk_WhenFresh()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("fresh", "Fresh Data")
            {
                Type = SupplierType.ContextSource,
                LastUpdated = DateTimeOffset.UtcNow.AddHours(-1),
                FreshnessThresholdHours = 168
            });
            var report = auditor.Audit();
            Assert.DoesNotContain(report.Findings, f => f.Category == SupplyRiskCategory.Freshness);
        }

        [Fact]
        public void Audit_DetectsReliabilityRisk()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("unreliable", "Bad API") { Type = SupplierType.ContextSource });
            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 10; i++)
                auditor.RecordReliability("unreliable", now.AddMinutes(-i), i % 3 == 0); // ~33% success

            var report = auditor.Audit();
            Assert.Contains(report.Findings, f => f.Category == SupplyRiskCategory.Reliability);
        }

        [Fact]
        public void Audit_NoReliabilityRisk_WhenReliable()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("reliable", "Good API") { Type = SupplierType.ContextSource });
            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 10; i++)
                auditor.RecordReliability("reliable", now.AddMinutes(-i), true);

            var report = auditor.Audit();
            Assert.DoesNotContain(report.Findings, f => f.Category == SupplyRiskCategory.Reliability);
        }

        [Fact]
        public void Audit_DetectsDeprecation()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("old", "Deprecated Template")
            {
                Type = SupplierType.Template,
                IsDeprecated = true
            });
            var report = auditor.Audit();
            Assert.Contains(report.Findings, f => f.Category == SupplyRiskCategory.Deprecation);
        }

        [Fact]
        public void Audit_DetectsApproachingDeprecation()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("aging", "Aging Template")
            {
                Type = SupplierType.Template,
                DeprecationDate = DateTimeOffset.UtcNow.AddDays(15)
            });
            var report = auditor.Audit();
            Assert.Contains(report.Findings, f => f.Category == SupplyRiskCategory.Deprecation);
        }

        [Fact]
        public void Audit_DetectsVersionDrift()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("t1", "Template V1") { Type = SupplierType.Template, Version = "1.0" });
            auditor.AddSupplier(new PromptSupplier("t2", "Template V2") { Type = SupplierType.Template, Version = "2.3" });
            var report = auditor.Audit();
            Assert.Contains(report.Findings, f => f.Category == SupplyRiskCategory.VersionDrift);
        }

        [Fact]
        public void Audit_DetectsConcentration()
        {
            var auditor = new PromptSupplyChainAuditor();
            for (int i = 0; i < 5; i++)
                auditor.AddSupplier(new PromptSupplier($"ctx-{i}", $"Context {i}") { Type = SupplierType.ContextSource });
            auditor.AddSupplier(new PromptSupplier("sys", "System") { Type = SupplierType.SystemInstruction });

            var report = auditor.Audit();
            Assert.Contains(report.Findings, f => f.Category == SupplyRiskCategory.Concentration);
        }

        [Fact]
        public void Audit_CascadeRiskDetected()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("root", "Root Supplier") { Type = SupplierType.ContextSource });
            auditor.AddSupplier(new PromptSupplier("mid1", "Middle 1") { Type = SupplierType.Template });
            auditor.AddSupplier(new PromptSupplier("mid2", "Middle 2") { Type = SupplierType.Template });
            auditor.AddSupplier(new PromptSupplier("leaf", "Leaf") { Type = SupplierType.PostProcessor });

            auditor.AddConsumerRelationship("mid1", "root");
            auditor.AddConsumerRelationship("mid2", "root");
            auditor.AddConsumerRelationship("leaf", "mid1");

            var report = auditor.Audit();
            // root has blast radius of 3 (mid1, mid2, leaf) and mid1/mid2/leaf are sole-source
            Assert.Contains(report.Findings, f => f.Category == SupplyRiskCategory.CascadeRisk);
        }

        [Fact]
        public void GetSoleSourceSuppliers_ReturnsCorrectList()
        {
            var auditor = new PromptSupplyChainAuditor();
            var s1 = new PromptSupplier("sole", "Sole Source") { Type = SupplierType.Template };
            var s2 = new PromptSupplier("diverse", "Diverse Source") { Type = SupplierType.Template };
            s2.Alternatives.Add("alt1");
            auditor.AddSupplier(s1);
            auditor.AddSupplier(s2);

            var soleSource = auditor.GetSoleSourceSuppliers();
            Assert.Contains(soleSource, s => s.Id == "sole");
        }

        [Fact]
        public void GetStaleSuppliers_ReturnsStaleOnes()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("stale", "Old")
            {
                Type = SupplierType.ContextSource,
                LastUpdated = DateTimeOffset.UtcNow.AddDays(-10),
                FreshnessThresholdHours = 48
            });
            auditor.AddSupplier(new PromptSupplier("fresh", "New")
            {
                Type = SupplierType.ContextSource,
                LastUpdated = DateTimeOffset.UtcNow.AddHours(-1),
                FreshnessThresholdHours = 48
            });

            var stale = auditor.GetStaleSuppliers();
            Assert.Single(stale);
            Assert.Equal("stale", stale[0].Id);
        }

        [Fact]
        public void ComputeBlastRadius_EmptyChain_ReturnsEmpty()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "S1") { Type = SupplierType.Template });
            var radius = auditor.ComputeBlastRadius("s1");
            Assert.Empty(radius);
        }

        [Fact]
        public void ComputeBlastRadius_WithConsumers_ReturnsAll()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("root", "Root") { Type = SupplierType.ContextSource });
            auditor.AddSupplier(new PromptSupplier("c1", "Consumer 1") { Type = SupplierType.Template });
            auditor.AddConsumerRelationship("c1", "root");
            auditor.AddConsumerRelationship("c2", "c1");

            var radius = auditor.ComputeBlastRadius("root");
            Assert.Contains("c1", radius);
            Assert.Contains("c2", radius);
        }

        [Fact]
        public void ComputeSubstitutability_NoAlternatives_LowScore()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("sole", "Sole") { Type = SupplierType.Template, IsSubstitutable = false });
            var score = auditor.ComputeSubstitutability("sole");
            Assert.True(score < 50);
        }

        [Fact]
        public void ComputeSubstitutability_WithAlternatives_HighScore()
        {
            var auditor = new PromptSupplyChainAuditor();
            var s = new PromptSupplier("diverse", "Diverse") { Type = SupplierType.Template, IsSubstitutable = true };
            s.Alternatives.AddRange(new[] { "alt1", "alt2", "alt3" });
            auditor.AddSupplier(s);
            var score = auditor.ComputeSubstitutability("diverse");
            Assert.True(score >= 80);
        }

        [Fact]
        public void ComputeSubstitutability_Unknown_ReturnsZero()
        {
            var auditor = new PromptSupplyChainAuditor();
            var score = auditor.ComputeSubstitutability("nonexistent");
            Assert.Equal(0, score);
        }

        [Fact]
        public void GetSuppliersByTier_FiltersCorrectly()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("t1", "Tier 1") { Tier = SupplyTier.Tier1 });
            auditor.AddSupplier(new PromptSupplier("t2", "Tier 2") { Tier = SupplyTier.Tier2 });
            auditor.AddSupplier(new PromptSupplier("t3", "Tier 3") { Tier = SupplyTier.Tier3Plus });

            Assert.Single(auditor.GetSuppliersByTier(SupplyTier.Tier2));
        }

        [Fact]
        public void Audit_ResilienceScore_HealthyChain_HighScore()
        {
            var auditor = new PromptSupplyChainAuditor();
            var s1 = new PromptSupplier("s1", "Source 1") { Type = SupplierType.Template, IsSubstitutable = true };
            s1.Alternatives.Add("s2");
            var s2 = new PromptSupplier("s2", "Source 2") { Type = SupplierType.ContextSource, IsSubstitutable = true };
            s2.Alternatives.Add("s1");
            auditor.AddSupplier(s1);
            auditor.AddSupplier(s2);

            var report = auditor.Audit();
            Assert.True(report.ResilienceScore >= 70);
        }

        [Fact]
        public void Audit_ResilienceScore_FragileChain_LowScore()
        {
            var auditor = new PromptSupplyChainAuditor();
            for (int i = 0; i < 6; i++)
                auditor.AddSupplier(new PromptSupplier($"sole-{i}", $"Sole {i}") { Type = SupplierType.ContextSource });

            var report = auditor.Audit();
            Assert.True(report.ResilienceScore < 70);
        }

        [Fact]
        public void Audit_Grade_MapsCorrectly()
        {
            var auditor = new PromptSupplyChainAuditor();
            var s = new PromptSupplier("s1", "S1") { Type = SupplierType.Template };
            s.Alternatives.Add("alt");
            auditor.AddSupplier(s);

            var report = auditor.Audit();
            Assert.Contains(report.Grade, new[] { "A", "B", "C", "D", "F" });
        }

        [Fact]
        public void Audit_Recommendations_GeneratedForSPOFs()
        {
            var auditor = CreateBasicChain();
            var report = auditor.Audit();
            Assert.True(report.Recommendations.Count > 0);
        }

        [Fact]
        public void Audit_ReliabilityScores_Computed()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "S1") { Type = SupplierType.Template });
            var now = DateTimeOffset.UtcNow;
            for (int i = 0; i < 5; i++)
                auditor.RecordReliability("s1", now.AddMinutes(-i), true, 100);

            var report = auditor.Audit();
            var score = report.ReliabilityScores.First(r => r.SupplierId == "s1");
            Assert.True(score.Score >= 80);
            Assert.Equal(1.0, score.SuccessRate);
        }

        [Fact]
        public void Audit_ReliabilityScore_DegradingTrend()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "S1") { Type = SupplierType.Template });
            var now = DateTimeOffset.UtcNow;
            // Old events: all success. Recent events: failures
            for (int i = 10; i >= 6; i--)
                auditor.RecordReliability("s1", now.AddMinutes(-i * 10), true);
            for (int i = 5; i >= 1; i--)
                auditor.RecordReliability("s1", now.AddMinutes(-i), false);

            var report = auditor.Audit();
            var score = report.ReliabilityScores.First(r => r.SupplierId == "s1");
            Assert.Equal("degrading", score.Trend);
        }

        [Fact]
        public void Audit_FindingSeverity_HighImpactIsCritical()
        {
            var auditor = new PromptSupplyChainAuditor();
            // A sole-source supplier with large blast radius
            auditor.AddSupplier(new PromptSupplier("critical", "Critical Source") { Type = SupplierType.ContextSource });
            for (int i = 0; i < 5; i++)
                auditor.AddConsumerRelationship($"consumer-{i}", "critical");

            var report = auditor.Audit();
            var finding = report.Findings.FirstOrDefault(f => f.SupplierId == "critical" && f.Category == SupplyRiskCategory.SoleSource);
            Assert.NotNull(finding);
            Assert.True(finding!.Severity == SupplyRiskSeverity.Critical || finding.Severity == SupplyRiskSeverity.High);
        }

        [Fact]
        public void Audit_TextReport_ContainsKey()
        {
            var auditor = CreateBasicChain();
            var report = auditor.Audit();
            var text = report.ToTextReport();
            Assert.Contains("SUPPLY CHAIN AUDIT", text);
            Assert.Contains("Resilience Score", text);
        }

        [Fact]
        public void Audit_JsonReport_ValidJson()
        {
            var auditor = CreateBasicChain();
            var report = auditor.Audit();
            var json = report.ToJson();
            Assert.Contains("ResilienceScore", json);
            Assert.Contains("Findings", json);
            // Should not throw
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.NotNull(doc);
        }

        [Fact]
        public void Audit_UserInputExcludedFromSPOF()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("user", "User Input") { Type = SupplierType.UserInput });
            var report = auditor.Audit();
            Assert.DoesNotContain("user", report.SinglePointsOfFailure);
        }

        [Fact]
        public void Audit_ConcentrationMap_Populated()
        {
            var auditor = CreateBasicChain();
            var report = auditor.Audit();
            Assert.True(report.ConcentrationMap.Count > 0);
            Assert.True(report.ConcentrationMap.Values.Sum() == 4);
        }

        [Fact]
        public void Audit_NoConcentrationRisk_WhenDiverseTypes()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "S1") { Type = SupplierType.Template });
            auditor.AddSupplier(new PromptSupplier("s2", "S2") { Type = SupplierType.ContextSource });
            auditor.AddSupplier(new PromptSupplier("s3", "S3") { Type = SupplierType.FewShotExamples });
            auditor.AddSupplier(new PromptSupplier("s4", "S4") { Type = SupplierType.SystemInstruction });
            auditor.AddSupplier(new PromptSupplier("s5", "S5") { Type = SupplierType.PostProcessor });

            var report = auditor.Audit();
            Assert.DoesNotContain(report.Findings, f => f.Category == SupplyRiskCategory.Concentration);
        }

        [Fact]
        public void SupplierTags_CanBeSet()
        {
            var s = new PromptSupplier("s1", "S1");
            s.Tags["team"] = "platform";
            s.Tags["env"] = "production";
            Assert.Equal("platform", s.Tags["team"]);
        }

        [Fact]
        public void ReliabilityEvent_StoresFailureReason()
        {
            var evt = new ReliabilityEvent(DateTimeOffset.UtcNow, false)
            {
                FailureReason = "Timeout",
                LatencyMs = 30000
            };
            Assert.Equal("Timeout", evt.FailureReason);
            Assert.Equal(30000, evt.LatencyMs);
        }

        [Fact]
        public void Audit_MultipleRuns_FindingCounterIncrements()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("s1", "S1") { Type = SupplierType.Template, IsDeprecated = true });
            auditor.AddSupplier(new PromptSupplier("s2", "S2") { Type = SupplierType.Template });

            var report = auditor.Audit();
            var ids = report.Findings.Select(f => f.FindingId).ToList();
            Assert.True(ids.Distinct().Count() == ids.Count, "Finding IDs should be unique");
        }

        [Fact]
        public void Audit_PropagationPath_Populated()
        {
            var auditor = new PromptSupplyChainAuditor();
            auditor.AddSupplier(new PromptSupplier("root", "Root") { Type = SupplierType.ContextSource });
            auditor.AddConsumerRelationship("c1", "root");
            auditor.AddConsumerRelationship("c2", "root");

            var report = auditor.Audit();
            var spofFinding = report.Findings.FirstOrDefault(f => f.SupplierId == "root" && f.Category == SupplyRiskCategory.SoleSource);
            Assert.NotNull(spofFinding);
            Assert.True(spofFinding!.PropagationPath.Count > 0);
        }

        [Fact]
        public void Audit_WithAlternatives_NoSPOFFinding()
        {
            var auditor = new PromptSupplyChainAuditor();
            var s = new PromptSupplier("s1", "S1") { Type = SupplierType.Template };
            s.Alternatives.Add("s2");
            auditor.AddSupplier(s);
            auditor.AddSupplier(new PromptSupplier("s2", "S2") { Type = SupplierType.Template });

            var report = auditor.Audit();
            Assert.DoesNotContain("s1", report.SinglePointsOfFailure);
        }
    }
}
