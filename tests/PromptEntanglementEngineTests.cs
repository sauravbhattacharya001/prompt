namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptEntanglementEngineTests
    {
        private readonly PromptEntanglementEngine _engine;

        public PromptEntanglementEngineTests()
        {
            _engine = new PromptEntanglementEngine(new EntanglementConfig
            {
                VariableOverlapThreshold = 0.3,
                SemanticSimilarityThreshold = 0.4,
                CascadeDepthLimit = 5,
                MinStrengthToReport = 20,
                MaxClustersToReport = 10,
                MinOutcomesForCorrelation = 5,
                CorrelationThreshold = 0.5
            });
        }

        // ─── Helpers ──────────────────────────────────

        private static PromptRegistration MakePrompt(string id, string content = "",
            List<string>? vars = null, List<string>? templates = null,
            List<string>? deps = null, List<string>? tags = null)
        {
            return new PromptRegistration
            {
                PromptId = id,
                Content = content,
                Variables = vars ?? new(),
                TemplateRefs = templates ?? new(),
                Dependencies = deps ?? new(),
                Tags = tags ?? new()
            };
        }

        private void RegisterBasicFleet()
        {
            _engine.RegisterPrompt(MakePrompt("p1", "Summarize the document about AI safety",
                vars: new() { "document", "topic", "length" },
                templates: new() { "summary-v1", "format-md" }));

            _engine.RegisterPrompt(MakePrompt("p2", "Translate the document to French language",
                vars: new() { "document", "language", "tone" },
                templates: new() { "summary-v1", "translate-v1" }));

            _engine.RegisterPrompt(MakePrompt("p3", "Generate a quiz about the document topic",
                vars: new() { "document", "topic", "difficulty" },
                templates: new() { "quiz-v1" }));

            _engine.RegisterPrompt(MakePrompt("p4", "Write a completely unrelated poem about nature",
                vars: new() { "style", "wordCount" },
                templates: new() { "creative-v1" }));
        }

        // ─── Registration Tests ───────────────────────

        [Fact]
        public void RegisterPrompt_ValidPrompt_Registers()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            Assert.Equal(1, _engine.PromptCount);
            Assert.True(_engine.IsRegistered("p1"));
        }

        [Fact]
        public void RegisterPrompt_NullRegistration_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _engine.RegisterPrompt(null!));
        }

        [Fact]
        public void RegisterPrompt_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.RegisterPrompt(MakePrompt("")));
        }

        [Fact]
        public void RegisterPrompt_DuplicateId_Overwrites()
        {
            _engine.RegisterPrompt(MakePrompt("p1", "version 1"));
            _engine.RegisterPrompt(MakePrompt("p1", "version 2"));
            Assert.Equal(1, _engine.PromptCount);
        }

        [Fact]
        public void RegisterPrompt_MultiplePrompts_AllRegistered()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            _engine.RegisterPrompt(MakePrompt("p2"));
            _engine.RegisterPrompt(MakePrompt("p3"));
            Assert.Equal(3, _engine.PromptCount);
        }

        [Fact]
        public void IsRegistered_UnknownPrompt_ReturnsFalse()
        {
            Assert.False(_engine.IsRegistered("nonexistent"));
        }

        // ─── Outcome Recording Tests ──────────────────

        [Fact]
        public void RecordOutcome_ValidOutcome_RecordsCounts()
        {
            _engine.RecordOutcome("p1", true, 100);
            _engine.RecordOutcome("p1", false, 200);
            Assert.Equal(2, _engine.GetOutcomeCount("p1"));
        }

        [Fact]
        public void RecordOutcome_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.RecordOutcome("", true, 100));
        }

        [Fact]
        public void RecordOutcome_NegativeLatency_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.RecordOutcome("p1", true, -1));
        }

        [Fact]
        public void GetOutcomeCount_NoOutcomes_ReturnsZero()
        {
            Assert.Equal(0, _engine.GetOutcomeCount("p1"));
        }

        // ─── Variable Entanglement Tests ──────────────

        [Fact]
        public void VariableEntanglement_SharedVars_Detected()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "name", "age", "city" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "name", "age", "email" }));

            var entanglements = _engine.DetectEntanglements();
            Assert.Contains(entanglements, e =>
                e.Type == EntanglementType.SharedVariable &&
                ((e.PromptA == "p1" && e.PromptB == "p2") || (e.PromptA == "p2" && e.PromptB == "p1")));
        }

        [Fact]
        public void VariableEntanglement_NoOverlap_NotDetected()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "name", "age" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "color", "size" }));

            var entanglements = _engine.DetectEntanglements();
            Assert.DoesNotContain(entanglements, e => e.Type == EntanglementType.SharedVariable);
        }

        [Fact]
        public void VariableEntanglement_IdenticalVars_HighStrength()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "x", "y", "z" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "x", "y", "z" }));

            var entanglements = _engine.DetectEntanglements();
            var varEnt = entanglements.First(e => e.Type == EntanglementType.SharedVariable);
            Assert.Equal(100, varEnt.Strength);
        }

        [Fact]
        public void VariableEntanglement_CaseInsensitive()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "Name", "AGE" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "name", "age" }));

            var entanglements = _engine.DetectEntanglements();
            Assert.Contains(entanglements, e => e.Type == EntanglementType.SharedVariable);
        }

        [Fact]
        public void VariableEntanglement_BelowThreshold_Filtered()
        {
            // 1 shared out of 5 union = 0.2 Jaccard, below 0.3 threshold → not detected
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a", "b", "c" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "a", "d", "e" }));

            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.SharedVariable);
        }

        [Fact]
        public void VariableEntanglement_EmptyVars_Skipped()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new()));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "x" }));

            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.SharedVariable);
        }

        [Fact]
        public void VariableEntanglement_EvidenceContainsSharedVarNames()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "name", "age" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "name", "age" }));

            var ent = _engine.DetectEntanglements().First(e => e.Type == EntanglementType.SharedVariable);
            Assert.Contains(ent.Evidence, ev => ev.Contains("name", StringComparison.OrdinalIgnoreCase));
        }

        // ─── Template Dependency Tests ────────────────

        [Fact]
        public void TemplateDependency_SharedTemplate_Detected()
        {
            _engine.RegisterPrompt(MakePrompt("p1", templates: new() { "base-v1", "format-md" }));
            _engine.RegisterPrompt(MakePrompt("p2", templates: new() { "base-v1", "translate" }));

            var ents = _engine.DetectEntanglements();
            Assert.Contains(ents, e => e.Type == EntanglementType.TemplateDependency);
        }

        [Fact]
        public void TemplateDependency_NoSharedTemplates_NotDetected()
        {
            _engine.RegisterPrompt(MakePrompt("p1", templates: new() { "tmpl-a" }));
            _engine.RegisterPrompt(MakePrompt("p2", templates: new() { "tmpl-b" }));

            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.TemplateDependency);
        }

        [Fact]
        public void TemplateDependency_AllShared_FullStrength()
        {
            _engine.RegisterPrompt(MakePrompt("p1", templates: new() { "tmpl-x" }));
            _engine.RegisterPrompt(MakePrompt("p2", templates: new() { "tmpl-x" }));

            var ent = _engine.DetectEntanglements().First(e => e.Type == EntanglementType.TemplateDependency);
            Assert.Equal(100, ent.Strength);
        }

        // ─── Order Dependency Tests ───────────────────

        [Fact]
        public void OrderDependency_ExplicitDep_Detected()
        {
            _engine.RegisterPrompt(MakePrompt("p1", deps: new() { "p2" }));
            _engine.RegisterPrompt(MakePrompt("p2"));

            var ents = _engine.DetectEntanglements();
            Assert.Contains(ents, e => e.Type == EntanglementType.OrderDependency);
        }

        [Fact]
        public void OrderDependency_MutualDep_HighStrength()
        {
            _engine.RegisterPrompt(MakePrompt("p1", deps: new() { "p2" }));
            _engine.RegisterPrompt(MakePrompt("p2", deps: new() { "p1" }));

            var ent = _engine.DetectEntanglements().First(e => e.Type == EntanglementType.OrderDependency);
            Assert.Equal(95, ent.Strength);
        }

        [Fact]
        public void OrderDependency_OneDirDep_ModerateStrength()
        {
            _engine.RegisterPrompt(MakePrompt("p1", deps: new() { "p2" }));
            _engine.RegisterPrompt(MakePrompt("p2"));

            var ent = _engine.DetectEntanglements().First(e => e.Type == EntanglementType.OrderDependency);
            Assert.Equal(75, ent.Strength);
        }

        // ─── Semantic Overlap Tests ───────────────────

        [Fact]
        public void SemanticOverlap_SimilarContent_Detected()
        {
            _engine.RegisterPrompt(MakePrompt("p1", content: "Analyze the customer feedback data and generate report"));
            _engine.RegisterPrompt(MakePrompt("p2", content: "Analyze the customer survey data and generate summary"));

            var ents = _engine.DetectEntanglements();
            Assert.Contains(ents, e => e.Type == EntanglementType.SemanticOverlap);
        }

        [Fact]
        public void SemanticOverlap_DissimilarContent_NotDetected()
        {
            _engine.RegisterPrompt(MakePrompt("p1", content: "Analyze financial market trends quarterly"));
            _engine.RegisterPrompt(MakePrompt("p2", content: "Write a haiku about mountain sunrise"));

            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.SemanticOverlap);
        }

        [Fact]
        public void SemanticOverlap_IdenticalContent_HighStrength()
        {
            _engine.RegisterPrompt(MakePrompt("p1", content: "Generate a detailed summary of the research paper"));
            _engine.RegisterPrompt(MakePrompt("p2", content: "Generate a detailed summary of the research paper"));

            var ent = _engine.DetectEntanglements().First(e => e.Type == EntanglementType.SemanticOverlap);
            Assert.Equal(100, ent.Strength);
        }

        [Fact]
        public void SemanticOverlap_EmptyContent_Skipped()
        {
            _engine.RegisterPrompt(MakePrompt("p1", content: ""));
            _engine.RegisterPrompt(MakePrompt("p2", content: "Some actual content here"));

            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.SemanticOverlap);
        }

        [Fact]
        public void SemanticOverlap_ShortWords_Filtered()
        {
            // Words with <= 2 chars should be filtered
            _engine.RegisterPrompt(MakePrompt("p1", content: "it is an ok to be"));
            _engine.RegisterPrompt(MakePrompt("p2", content: "it is an ok to go"));

            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.SemanticOverlap);
        }

        // ─── Behavioral Correlation Tests ─────────────

        [Fact]
        public void BehavioralCorrelation_CorrelatedOutcomes_Detected()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            _engine.RegisterPrompt(MakePrompt("p2"));

            // Both succeed and fail together
            for (int i = 0; i < 10; i++)
            {
                bool success = i % 2 == 0;
                _engine.RecordOutcome("p1", success, 100);
                _engine.RecordOutcome("p2", success, 150);
            }

            var ents = _engine.DetectEntanglements();
            Assert.Contains(ents, e => e.Type == EntanglementType.BehavioralCorrelation);
        }

        [Fact]
        public void BehavioralCorrelation_UncorrelatedOutcomes_NotDetected()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            _engine.RegisterPrompt(MakePrompt("p2"));

            var rng = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                _engine.RecordOutcome("p1", i % 2 == 0, 100);
                _engine.RecordOutcome("p2", rng.Next(2) == 0, 100);
            }

            var ents = _engine.DetectEntanglements();
            // May or may not detect — check that if detected, strength is reasonable
            var behavioral = ents.Where(e => e.Type == EntanglementType.BehavioralCorrelation).ToList();
            // With random data and seed 42, correlation should be low
            Assert.True(behavioral.Count == 0 || behavioral.All(e => e.Strength < 80));
        }

        [Fact]
        public void BehavioralCorrelation_InsufficientData_NotDetected()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            _engine.RegisterPrompt(MakePrompt("p2"));

            // Only 3 outcomes each, below MinOutcomesForCorrelation=5
            for (int i = 0; i < 3; i++)
            {
                _engine.RecordOutcome("p1", true, 100);
                _engine.RecordOutcome("p2", true, 100);
            }

            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.BehavioralCorrelation);
        }

        [Fact]
        public void BehavioralCorrelation_PerfectNegativeCorrelation_Detected()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            _engine.RegisterPrompt(MakePrompt("p2"));

            for (int i = 0; i < 10; i++)
            {
                _engine.RecordOutcome("p1", i % 2 == 0, 100);
                _engine.RecordOutcome("p2", i % 2 != 0, 100);
            }

            var ents = _engine.DetectEntanglements();
            var corr = ents.FirstOrDefault(e => e.Type == EntanglementType.BehavioralCorrelation);
            Assert.NotNull(corr);
            Assert.True(corr.Strength >= 90); // |r| ≈ 1
        }

        [Fact]
        public void BehavioralCorrelation_AllSuccess_ZeroVariance()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            _engine.RegisterPrompt(MakePrompt("p2"));

            for (int i = 0; i < 10; i++)
            {
                _engine.RecordOutcome("p1", true, 100);
                _engine.RecordOutcome("p2", true, 100);
            }

            // Zero variance → Pearson returns 0 → not detected
            var ents = _engine.DetectEntanglements();
            Assert.DoesNotContain(ents, e => e.Type == EntanglementType.BehavioralCorrelation);
        }

        // ─── Cascade Chain Tests ──────────────────────

        [Fact]
        public void CascadeChains_LinearChain_DetectsPath()
        {
            // p1 → p2 → p3 via shared vars
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a", "b" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "b", "c" }));
            _engine.RegisterPrompt(MakePrompt("p3", vars: new() { "c", "d" }));

            var chains = _engine.GetCascadeChains("p1");
            Assert.True(chains.Count > 0);
            // Should find a chain reaching p3
            Assert.Contains(chains, c => c.AffectedPromptIds.Contains("p3"));
        }

        [Fact]
        public void CascadeChains_NoEntanglements_EmptyResult()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "b" }));

            var chains = _engine.GetCascadeChains("p1");
            Assert.Empty(chains);
        }

        [Fact]
        public void CascadeChains_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.GetCascadeChains(""));
        }

        [Fact]
        public void CascadeChains_DepthLimit_Respected()
        {
            var config = new EntanglementConfig { CascadeDepthLimit = 2, MinStrengthToReport = 0, VariableOverlapThreshold = 0.1 };
            var engine = new PromptEntanglementEngine(config);

            // Chain of 5: p1-p2-p3-p4-p5
            engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a", "b" }));
            engine.RegisterPrompt(MakePrompt("p2", vars: new() { "b", "c" }));
            engine.RegisterPrompt(MakePrompt("p3", vars: new() { "c", "d" }));
            engine.RegisterPrompt(MakePrompt("p4", vars: new() { "d", "e" }));
            engine.RegisterPrompt(MakePrompt("p5", vars: new() { "e", "f" }));

            var chains = engine.GetCascadeChains("p1");
            // With depth limit 2, should not reach beyond 2 hops
            Assert.DoesNotContain(chains, c => c.ChainLength > 2);
        }

        // ─── Cluster Detection Tests ──────────────────

        [Fact]
        public void Clusters_ConnectedPrompts_FormCluster()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "x", "y" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "x", "y" }));
            _engine.RegisterPrompt(MakePrompt("p3", vars: new() { "x", "y" }));

            var clusters = _engine.GetClusters();
            Assert.Single(clusters);
            Assert.Equal(3, clusters[0].PromptIds.Count);
        }

        [Fact]
        public void Clusters_DisconnectedGroups_SeparateClusters()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a", "b" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "a", "b" }));
            _engine.RegisterPrompt(MakePrompt("p3", vars: new() { "x", "y" }));
            _engine.RegisterPrompt(MakePrompt("p4", vars: new() { "x", "y" }));

            var clusters = _engine.GetClusters();
            Assert.Equal(2, clusters.Count);
        }

        [Fact]
        public void Clusters_NoEntanglements_Empty()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "b" }));

            var clusters = _engine.GetClusters();
            Assert.Empty(clusters);
        }

        [Fact]
        public void Clusters_SinglePrompt_NoCluster()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a" }));
            var clusters = _engine.GetClusters();
            Assert.Empty(clusters);
        }

        [Fact]
        public void Clusters_HaveDominantType()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "x", "y" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "x", "y" }));

            var clusters = _engine.GetClusters();
            Assert.NotEmpty(clusters);
            Assert.Equal(EntanglementType.SharedVariable, clusters[0].DominantType);
        }

        [Fact]
        public void Clusters_RiskScoreComputed()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "x", "y" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "x", "y" }));

            var clusters = _engine.GetClusters();
            Assert.True(clusters[0].RiskScore > 0);
        }

        // ─── Health Score Tests ───────────────────────

        [Fact]
        public void HealthScore_NoPrompts_Returns100()
        {
            Assert.Equal(100, _engine.GetHealthScore());
        }

        [Fact]
        public void HealthScore_NoEntanglements_Returns100()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "b" }));
            Assert.Equal(100, _engine.GetHealthScore());
        }

        [Fact]
        public void HealthScore_HighStrengthEntanglements_LowScore()
        {
            // Many overlapping prompts to bring score down
            for (int i = 0; i < 10; i++)
            {
                _engine.RegisterPrompt(MakePrompt($"p{i}", vars: new() { "shared1", "shared2", "shared3" }));
            }

            var score = _engine.GetHealthScore();
            Assert.True(score < 80);
        }

        [Fact]
        public void HealthScore_FloorAtZero()
        {
            // Create many high-strength entanglements
            for (int i = 0; i < 30; i++)
            {
                _engine.RegisterPrompt(MakePrompt($"p{i}",
                    vars: new() { "a", "b", "c" },
                    templates: new() { "tmpl" },
                    content: "Same content for all prompts to maximize entanglement"));
            }

            var score = _engine.GetHealthScore();
            Assert.True(score >= 0);
        }

        [Fact]
        public void HealthScore_CapAt100()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            Assert.True(_engine.GetHealthScore() <= 100);
        }

        // ─── Health Tier Tests ────────────────────────

        [Fact]
        public void HealthTier_Decoupled()
        {
            Assert.Equal(EntanglementHealthTier.Decoupled, PromptEntanglementEngine.ClassifyHealthTier(95));
        }

        [Fact]
        public void HealthTier_Manageable()
        {
            Assert.Equal(EntanglementHealthTier.Manageable, PromptEntanglementEngine.ClassifyHealthTier(75));
        }

        [Fact]
        public void HealthTier_Tangled()
        {
            Assert.Equal(EntanglementHealthTier.Tangled, PromptEntanglementEngine.ClassifyHealthTier(55));
        }

        [Fact]
        public void HealthTier_Knotted()
        {
            Assert.Equal(EntanglementHealthTier.Knotted, PromptEntanglementEngine.ClassifyHealthTier(35));
        }

        [Fact]
        public void HealthTier_Spaghetti()
        {
            Assert.Equal(EntanglementHealthTier.Spaghetti, PromptEntanglementEngine.ClassifyHealthTier(10));
        }

        [Fact]
        public void HealthTier_Boundaries()
        {
            Assert.Equal(EntanglementHealthTier.Decoupled, PromptEntanglementEngine.ClassifyHealthTier(90));
            Assert.Equal(EntanglementHealthTier.Manageable, PromptEntanglementEngine.ClassifyHealthTier(70));
            Assert.Equal(EntanglementHealthTier.Tangled, PromptEntanglementEngine.ClassifyHealthTier(50));
            Assert.Equal(EntanglementHealthTier.Knotted, PromptEntanglementEngine.ClassifyHealthTier(20));
            Assert.Equal(EntanglementHealthTier.Spaghetti, PromptEntanglementEngine.ClassifyHealthTier(19));
        }

        // ─── Severity Tests ──────────────────────────

        [Fact]
        public void Severity_Negligible()
        {
            var e = new Entanglement { Strength = 15 };
            Assert.Equal(EntanglementSeverity.Negligible, e.Severity);
        }

        [Fact]
        public void Severity_Low()
        {
            var e = new Entanglement { Strength = 30 };
            Assert.Equal(EntanglementSeverity.Low, e.Severity);
        }

        [Fact]
        public void Severity_Moderate()
        {
            var e = new Entanglement { Strength = 50 };
            Assert.Equal(EntanglementSeverity.Moderate, e.Severity);
        }

        [Fact]
        public void Severity_High()
        {
            var e = new Entanglement { Strength = 70 };
            Assert.Equal(EntanglementSeverity.High, e.Severity);
        }

        [Fact]
        public void Severity_Critical()
        {
            var e = new Entanglement { Strength = 85 };
            Assert.Equal(EntanglementSeverity.Critical, e.Severity);
        }

        // ─── Insight Generation Tests ─────────────────

        [Fact]
        public void Insights_NoEntanglements_DecoupledMessage()
        {
            _engine.RegisterPrompt(MakePrompt("p1"));
            var report = _engine.GenerateReport();
            Assert.Contains(report.Insights, i => i.Contains("decoupled"));
        }

        [Fact]
        public void Insights_HubPrompt_Identified()
        {
            // Create a hub: p1 connected to p2, p3, p4, p5 via shared vars
            _engine.RegisterPrompt(MakePrompt("hub", vars: new() { "a", "b", "c", "d" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "a", "b" }));
            _engine.RegisterPrompt(MakePrompt("p3", vars: new() { "b", "c" }));
            _engine.RegisterPrompt(MakePrompt("p4", vars: new() { "c", "d" }));
            _engine.RegisterPrompt(MakePrompt("p5", vars: new() { "a", "d" }));

            var report = _engine.GenerateReport();
            Assert.Contains(report.Insights, i => i.Contains("hub", StringComparison.OrdinalIgnoreCase) || i.Contains("Hub"));
        }

        [Fact]
        public void Insights_IsolatedPrompts_Noted()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a", "b" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "a", "b" }));
            _engine.RegisterPrompt(MakePrompt("isolated", vars: new() { "z" }));

            var report = _engine.GenerateReport();
            Assert.Contains(report.Insights, i => i.Contains("isolated", StringComparison.OrdinalIgnoreCase) || i.Contains("Isolated"));
        }

        [Fact]
        public void Insights_CriticalEntanglements_Warned()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "a", "b", "c" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "a", "b", "c" }));

            var report = _engine.GenerateReport();
            var entanglements = _engine.DetectEntanglements();
            if (entanglements.Any(e => e.Severity == EntanglementSeverity.Critical))
            {
                Assert.Contains(report.Insights, i => i.Contains("critical", StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void Insights_TypeDistribution_Included()
        {
            RegisterBasicFleet();
            var report = _engine.GenerateReport();
            Assert.Contains(report.Insights, i => i.Contains("distribution", StringComparison.OrdinalIgnoreCase));
        }

        // ─── GetEntanglementsFor Tests ────────────────

        [Fact]
        public void GetEntanglementsFor_ReturnsRelevantPairs()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "x", "y" }));
            _engine.RegisterPrompt(MakePrompt("p2", vars: new() { "x", "y" }));
            _engine.RegisterPrompt(MakePrompt("p3", vars: new() { "a", "b" }));

            var ents = _engine.GetEntanglementsFor("p1");
            Assert.All(ents, e => Assert.True(e.PromptA == "p1" || e.PromptB == "p1"));
        }

        [Fact]
        public void GetEntanglementsFor_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.GetEntanglementsFor(""));
        }

        [Fact]
        public void GetEntanglementsFor_NoEntanglements_EmptyList()
        {
            _engine.RegisterPrompt(MakePrompt("p1", vars: new() { "unique" }));
            var ents = _engine.GetEntanglementsFor("p1");
            Assert.Empty(ents);
        }

        // ─── Report Generation Tests ──────────────────

        [Fact]
        public void Report_EmptyFleet_ValidReport()
        {
            var report = _engine.GenerateReport();
            Assert.Equal(0, report.TotalPrompts);
            Assert.Equal(0, report.TotalEntanglements);
            Assert.Equal(100, report.HealthScore);
        }

        [Fact]
        public void Report_BasicFleet_ComprehensiveReport()
        {
            RegisterBasicFleet();
            var report = _engine.GenerateReport();

            Assert.Equal(4, report.TotalPrompts);
            Assert.True(report.TotalEntanglements > 0);
            Assert.NotEmpty(report.Insights);
            Assert.True(report.HealthScore >= 0 && report.HealthScore <= 100);
        }

        [Fact]
        public void Report_StrongestPairs_OrderedByStrength()
        {
            RegisterBasicFleet();
            var report = _engine.GenerateReport();

            for (int i = 1; i < report.StrongestPairs.Count; i++)
            {
                Assert.True(report.StrongestPairs[i - 1].Strength >= report.StrongestPairs[i].Strength);
            }
        }

        [Fact]
        public void Report_HealthTier_MatchesScore()
        {
            RegisterBasicFleet();
            var report = _engine.GenerateReport();
            Assert.Equal(PromptEntanglementEngine.ClassifyHealthTier(report.HealthScore), report.HealthTier);
        }

        // ─── Dashboard Tests ──────────────────────────

        [Fact]
        public void Dashboard_ReturnsValidHtml()
        {
            RegisterBasicFleet();
            var html = _engine.RenderDashboard();
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("</html>", html);
            Assert.Contains("Entanglement Dashboard", html);
        }

        [Fact]
        public void Dashboard_ContainsHealthScore()
        {
            RegisterBasicFleet();
            var html = _engine.RenderDashboard();
            Assert.Contains("Health Score", html);
        }

        [Fact]
        public void Dashboard_ContainsInsights()
        {
            RegisterBasicFleet();
            var html = _engine.RenderDashboard();
            Assert.Contains("Autonomous Insights", html);
        }

        [Fact]
        public void Dashboard_EmptyFleet_StillRenders()
        {
            var html = _engine.RenderDashboard();
            Assert.Contains("<!DOCTYPE html>", html);
        }

        [Fact]
        public void Dashboard_EscapesHtmlEntities()
        {
            _engine.RegisterPrompt(MakePrompt("test<script>", vars: new() { "x" }));
            var html = _engine.RenderDashboard();
            Assert.DoesNotContain("<script>", html);
        }

        // ─── Integration / Edge Case Tests ────────────

        [Fact]
        public void FullFleet_MultiTypeEntanglements()
        {
            RegisterBasicFleet();

            // Add outcomes for behavioral correlation
            for (int i = 0; i < 10; i++)
            {
                bool success = i % 2 == 0;
                _engine.RecordOutcome("p1", success, 100);
                _engine.RecordOutcome("p2", success, 150);
            }

            var report = _engine.GenerateReport();
            var types = report.StrongestPairs.Select(e => e.Type).Distinct().ToList();
            Assert.True(types.Count >= 2, "Expected multiple entanglement types");
        }

        [Fact]
        public void LargeFleet_HandlesPerformance()
        {
            for (int i = 0; i < 50; i++)
            {
                _engine.RegisterPrompt(MakePrompt($"p{i}",
                    content: $"Process item {i} with parameter alpha and beta",
                    vars: new() { "alpha", "beta", $"unique{i}" }));
            }

            var report = _engine.GenerateReport();
            Assert.Equal(50, report.TotalPrompts);
            Assert.True(report.TotalEntanglements > 0);
        }

        [Fact]
        public void ConfigTuning_HighThreshold_FewerDetections()
        {
            var strict = new PromptEntanglementEngine(new EntanglementConfig
            {
                VariableOverlapThreshold = 0.9,
                SemanticSimilarityThreshold = 0.9,
                MinStrengthToReport = 80
            });

            var lenient = new PromptEntanglementEngine(new EntanglementConfig
            {
                VariableOverlapThreshold = 0.1,
                SemanticSimilarityThreshold = 0.1,
                MinStrengthToReport = 5
            });

            var prompts = new[]
            {
                MakePrompt("p1", "Analyze data patterns", vars: new() { "data", "format" }),
                MakePrompt("p2", "Analyze data trends", vars: new() { "data", "output" })
            };

            foreach (var p in prompts) { strict.RegisterPrompt(p); lenient.RegisterPrompt(p); }

            var strictEnts = strict.DetectEntanglements();
            var lenientEnts = lenient.DetectEntanglements();

            Assert.True(lenientEnts.Count >= strictEnts.Count);
        }

        [Fact]
        public void DetectEntanglements_ResultsOrderedByStrength()
        {
            RegisterBasicFleet();
            var ents = _engine.DetectEntanglements();

            for (int i = 1; i < ents.Count; i++)
            {
                Assert.True(ents[i - 1].Strength >= ents[i].Strength);
            }
        }
    }
}
