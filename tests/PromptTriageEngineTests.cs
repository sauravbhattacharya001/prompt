namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptTriageEngineTests
    {
        private PromptTriageEngine CreateEngine(TriageConfig? config = null)
            => new PromptTriageEngine(config);

        private TriageSignal MakeSignal(
            string promptId = "prompt-1",
            TriageCategory category = TriageCategory.FailureSpike,
            TriageSeverity severity = TriageSeverity.Medium,
            string description = "Test signal",
            DateTime? timestamp = null)
        {
            return new TriageSignal
            {
                PromptId = promptId,
                Category = category,
                Severity = severity,
                Description = description,
                Timestamp = timestamp ?? DateTime.UtcNow
            };
        }

        // ── Signal Ingestion ─────────────────────────────

        [Fact]
        public void IngestSignal_CreatesNewIncident()
        {
            var engine = CreateEngine();
            var incident = engine.IngestSignal(MakeSignal());

            Assert.NotNull(incident);
            Assert.Equal("prompt-1", incident.PromptId);
            Assert.Equal(TriageCategory.FailureSpike, incident.Category);
            Assert.Equal(IncidentStatus.Open, incident.Status);
            Assert.Equal(1, incident.SignalCount);
        }

        [Fact]
        public void IngestSignal_NullSignal_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentNullException>(() => engine.IngestSignal(null!));
        }

        [Fact]
        public void IngestSignal_EmptyPromptId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.IngestSignal(new TriageSignal { PromptId = "" }));
        }

        [Fact]
        public void IngestSignal_AssignsId()
        {
            var engine = CreateEngine();
            var incident = engine.IngestSignal(MakeSignal());
            Assert.NotNull(incident.Id);
            Assert.NotEmpty(incident.Id);
            Assert.Equal(12, incident.Id.Length);
        }

        [Fact]
        public void IngestSignal_SetsInvestigationPlan()
        {
            var engine = CreateEngine();
            var incident = engine.IngestSignal(MakeSignal());
            Assert.NotEmpty(incident.InvestigationPlan);
        }

        [Fact]
        public void IngestSignal_ComputesBlastRadius()
        {
            var engine = CreateEngine();
            var incident = engine.IngestSignal(MakeSignal());
            Assert.True(incident.BlastRadius > 0);
        }

        [Fact]
        public void IngestSignal_SetsUrgencyForCritical()
        {
            var engine = CreateEngine();
            var incident = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Critical));
            Assert.Equal(TriageUrgency.Immediate, incident.Urgency);
        }

        // ── Deduplication ────────────────────────────────

        [Fact]
        public void IngestSignal_DeduplicatesSamePromptAndCategory()
        {
            var engine = CreateEngine();
            var now = DateTime.UtcNow;
            var s1 = MakeSignal(timestamp: now);
            var s2 = MakeSignal(timestamp: now.AddMinutes(5));

            var inc1 = engine.IngestSignal(s1);
            var inc2 = engine.IngestSignal(s2);

            Assert.Equal(inc1.Id, inc2.Id);
            Assert.Equal(2, inc2.SignalCount);
        }

        [Fact]
        public void IngestSignal_DoesNotDeduplicateDifferentCategory()
        {
            var engine = CreateEngine();
            var s1 = MakeSignal(category: TriageCategory.FailureSpike);
            var s2 = MakeSignal(category: TriageCategory.CostAnomaly);

            var inc1 = engine.IngestSignal(s1);
            var inc2 = engine.IngestSignal(s2);

            Assert.NotEqual(inc1.Id, inc2.Id);
        }

        [Fact]
        public void IngestSignal_DoesNotDeduplicateDifferentPrompt()
        {
            var engine = CreateEngine();
            var s1 = MakeSignal(promptId: "prompt-1");
            var s2 = MakeSignal(promptId: "prompt-2");

            var inc1 = engine.IngestSignal(s1);
            var inc2 = engine.IngestSignal(s2);

            Assert.NotEqual(inc1.Id, inc2.Id);
        }

        [Fact]
        public void IngestSignal_DoesNotDeduplicateOutsideWindow()
        {
            var config = new TriageConfig { DeduplicationWindowMinutes = 10 };
            var engine = CreateEngine(config);
            var now = DateTime.UtcNow;
            var s1 = MakeSignal(timestamp: now.AddMinutes(-20));
            var s2 = MakeSignal(timestamp: now);

            var inc1 = engine.IngestSignal(s1);
            var inc2 = engine.IngestSignal(s2);

            Assert.NotEqual(inc1.Id, inc2.Id);
        }

        [Fact]
        public void IngestSignal_DedupUpgradesSeverity()
        {
            var engine = CreateEngine();
            var now = DateTime.UtcNow;
            engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now));
            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.High, timestamp: now.AddMinutes(1)));

            Assert.Equal(TriageSeverity.High, inc.Severity);
        }

        [Fact]
        public void IngestSignal_DedupDoesNotDowngradeSeverity()
        {
            var engine = CreateEngine();
            var now = DateTime.UtcNow;
            engine.IngestSignal(MakeSignal(severity: TriageSeverity.High, timestamp: now));
            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now.AddMinutes(1)));

            Assert.Equal(TriageSeverity.High, inc.Severity);
        }

        [Fact]
        public void IngestSignal_DedupAccumulatesSignalCount()
        {
            var engine = CreateEngine();
            var now = DateTime.UtcNow;
            engine.IngestSignal(MakeSignal(timestamp: now));
            engine.IngestSignal(MakeSignal(timestamp: now.AddMinutes(1)));
            engine.IngestSignal(MakeSignal(timestamp: now.AddMinutes(2)));
            var inc = engine.IngestSignal(MakeSignal(timestamp: now.AddMinutes(3)));

            Assert.Equal(4, inc.SignalCount);
        }

        // ── GetIncident ──────────────────────────────────

        [Fact]
        public void GetIncident_ReturnsCorrectIncident()
        {
            var engine = CreateEngine();
            var created = engine.IngestSignal(MakeSignal());
            var fetched = engine.GetIncident(created.Id);

            Assert.NotNull(fetched);
            Assert.Equal(created.Id, fetched!.Id);
        }

        [Fact]
        public void GetIncident_ReturnsNullForUnknown()
        {
            var engine = CreateEngine();
            Assert.Null(engine.GetIncident("nonexistent"));
        }

        [Fact]
        public void GetIncident_EmptyId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.GetIncident(""));
        }

        // ── GetIncidentsByPrompt ─────────────────────────

        [Fact]
        public void GetIncidentsByPrompt_ReturnsMatchingIncidents()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(promptId: "prompt-1", category: TriageCategory.FailureSpike));
            engine.IngestSignal(MakeSignal(promptId: "prompt-1", category: TriageCategory.CostAnomaly));
            engine.IngestSignal(MakeSignal(promptId: "prompt-2"));

            var results = engine.GetIncidentsByPrompt("prompt-1");
            Assert.Equal(2, results.Count);
            Assert.All(results, i => Assert.Equal("prompt-1", i.PromptId));
        }

        [Fact]
        public void GetIncidentsByPrompt_EmptyId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.GetIncidentsByPrompt(""));
        }

        // ── GetOpenIncidents ─────────────────────────────

        [Fact]
        public void GetOpenIncidents_ExcludesResolvedAndDismissed()
        {
            var engine = CreateEngine();
            var inc1 = engine.IngestSignal(MakeSignal(promptId: "p1", category: TriageCategory.FailureSpike));
            var inc2 = engine.IngestSignal(MakeSignal(promptId: "p2", category: TriageCategory.CostAnomaly));
            var inc3 = engine.IngestSignal(MakeSignal(promptId: "p3", category: TriageCategory.QualityDrift));

            engine.UpdateStatus(inc2.Id, IncidentStatus.Resolved);
            engine.DismissIncident(inc3.Id, "Not relevant");

            var open = engine.GetOpenIncidents();
            Assert.Single(open);
            Assert.Equal(inc1.Id, open[0].Id);
        }

        [Fact]
        public void GetOpenIncidents_SortedByBlastRadiusDescending()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(promptId: "low", severity: TriageSeverity.Low));
            engine.IngestSignal(MakeSignal(promptId: "critical", severity: TriageSeverity.Critical));

            var open = engine.GetOpenIncidents();
            Assert.Equal("critical", open[0].PromptId);
        }

        [Fact]
        public void GetOpenIncidents_IncludesInvestigatingAndMitigating()
        {
            var engine = CreateEngine();
            var inc1 = engine.IngestSignal(MakeSignal(promptId: "p1", category: TriageCategory.FailureSpike));
            var inc2 = engine.IngestSignal(MakeSignal(promptId: "p2", category: TriageCategory.CostAnomaly));

            engine.UpdateStatus(inc1.Id, IncidentStatus.Investigating);
            engine.UpdateStatus(inc2.Id, IncidentStatus.Mitigating);

            var open = engine.GetOpenIncidents();
            Assert.Equal(2, open.Count);
        }

        // ── UpdateStatus ─────────────────────────────────

        [Fact]
        public void UpdateStatus_TransitionsCorrectly()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());

            engine.UpdateStatus(inc.Id, IncidentStatus.Investigating, "Looking into it");

            var fetched = engine.GetIncident(inc.Id);
            Assert.Equal(IncidentStatus.Investigating, fetched!.Status);
            Assert.Equal("Looking into it", fetched.ResolutionNotes);
        }

        [Fact]
        public void UpdateStatus_ResolvedSetsTimestamp()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());

            engine.UpdateStatus(inc.Id, IncidentStatus.Resolved, "Fixed");

            var fetched = engine.GetIncident(inc.Id);
            Assert.Equal(IncidentStatus.Resolved, fetched!.Status);
            Assert.NotNull(fetched.ResolvedAt);
        }

        [Fact]
        public void UpdateStatus_UnknownId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<KeyNotFoundException>(() => engine.UpdateStatus("nonexistent", IncidentStatus.Resolved));
        }

        [Fact]
        public void UpdateStatus_EmptyId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.UpdateStatus("", IncidentStatus.Open));
        }

        [Fact]
        public void UpdateStatus_NullNotes_DoesNotOverwrite()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());
            engine.UpdateStatus(inc.Id, IncidentStatus.Investigating, "Initial notes");
            engine.UpdateStatus(inc.Id, IncidentStatus.Mitigating);

            var fetched = engine.GetIncident(inc.Id);
            Assert.Equal("Initial notes", fetched!.ResolutionNotes);
        }

        // ── DismissIncident ──────────────────────────────

        [Fact]
        public void DismissIncident_SetsStatusAndReason()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());

            engine.DismissIncident(inc.Id, "False positive");

            var fetched = engine.GetIncident(inc.Id);
            Assert.Equal(IncidentStatus.Dismissed, fetched!.Status);
            Assert.Equal("False positive", fetched.ResolutionNotes);
        }

        [Fact]
        public void DismissIncident_EmptyReason_Throws()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());
            Assert.Throws<ArgumentException>(() => engine.DismissIncident(inc.Id, ""));
        }

        [Fact]
        public void DismissIncident_UnknownId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<KeyNotFoundException>(() => engine.DismissIncident("nonexistent", "reason"));
        }

        [Fact]
        public void DismissIncident_EmptyId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.DismissIncident("", "reason"));
        }

        // ── EscalateIncident ─────────────────────────────

        [Fact]
        public void EscalateIncident_BumpsSeverity()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Medium));

            engine.EscalateIncident(inc.Id);

            var fetched = engine.GetIncident(inc.Id);
            Assert.Equal(TriageSeverity.High, fetched!.Severity);
        }

        [Fact]
        public void EscalateIncident_CriticalDoesNotGoHigher()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Critical));

            engine.EscalateIncident(inc.Id);

            var fetched = engine.GetIncident(inc.Id);
            Assert.Equal(TriageSeverity.Critical, fetched!.Severity);
        }

        [Fact]
        public void EscalateIncident_UpdatesBlastRadius()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low));
            double before = inc.BlastRadius;

            engine.EscalateIncident(inc.Id);

            var fetched = engine.GetIncident(inc.Id);
            Assert.True(fetched!.BlastRadius > before);
        }

        [Fact]
        public void EscalateIncident_UpdatesUrgency()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Medium));
            Assert.Equal(TriageUrgency.Scheduled, inc.Urgency);

            engine.EscalateIncident(inc.Id);

            var fetched = engine.GetIncident(inc.Id);
            Assert.Equal(TriageUrgency.Urgent, fetched!.Urgency);
        }

        [Fact]
        public void EscalateIncident_UnknownId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<KeyNotFoundException>(() => engine.EscalateIncident("nonexistent"));
        }

        [Fact]
        public void EscalateIncident_EmptyId_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.EscalateIncident(""));
        }

        // ── ComputeBlastRadius ───────────────────────────

        [Fact]
        public void ComputeBlastRadius_CriticalHigherThanInfo()
        {
            var engine = CreateEngine();
            var critInc = engine.IngestSignal(MakeSignal(promptId: "p1", severity: TriageSeverity.Critical));
            var infoInc = engine.IngestSignal(MakeSignal(promptId: "p2", severity: TriageSeverity.Info, category: TriageCategory.QualityDrift));

            Assert.True(critInc.BlastRadius > infoInc.BlastRadius);
        }

        [Fact]
        public void ComputeBlastRadius_IncreasesWithMoreSignals()
        {
            var engine = CreateEngine();
            var now = DateTime.UtcNow;
            var inc = engine.IngestSignal(MakeSignal(timestamp: now));
            double firstRadius = inc.BlastRadius;

            for (int i = 0; i < 5; i++)
                engine.IngestSignal(MakeSignal(timestamp: now.AddMinutes(i + 1)));

            Assert.True(inc.BlastRadius > firstRadius);
        }

        [Fact]
        public void ComputeBlastRadius_CappedAt100()
        {
            var engine = CreateEngine();
            var now = DateTime.UtcNow;
            engine.IngestSignal(MakeSignal(severity: TriageSeverity.Critical, timestamp: now));
            for (int i = 0; i < 50; i++)
                engine.IngestSignal(MakeSignal(severity: TriageSeverity.Critical, timestamp: now.AddMinutes(i + 1)));

            var inc = engine.GetOpenIncidents().First();
            Assert.True(inc.BlastRadius <= 100);
        }

        [Fact]
        public void ComputeBlastRadius_NullIncident_Throws()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentNullException>(() => engine.ComputeBlastRadius(null!));
        }

        [Fact]
        public void ComputeBlastRadius_FailureSpikeWeighedMoreThanLatency()
        {
            var engine = CreateEngine();
            var failInc = engine.IngestSignal(MakeSignal(promptId: "p1", category: TriageCategory.FailureSpike));
            var latInc = engine.IngestSignal(MakeSignal(promptId: "p2", category: TriageCategory.LatencyDegradation));

            Assert.True(failInc.BlastRadius > latInc.BlastRadius);
        }

        // ── InvestigationPlan ────────────────────────────

        [Theory]
        [InlineData(TriageCategory.FailureSpike)]
        [InlineData(TriageCategory.LatencyDegradation)]
        [InlineData(TriageCategory.QualityDrift)]
        [InlineData(TriageCategory.CostAnomaly)]
        [InlineData(TriageCategory.TokenOverflow)]
        [InlineData(TriageCategory.HallucinationSignal)]
        [InlineData(TriageCategory.ComplianceViolation)]
        [InlineData(TriageCategory.DependencyFailure)]
        public void GenerateInvestigationPlan_HasStepsForEachCategory(TriageCategory category)
        {
            var engine = CreateEngine();
            var plan = engine.GenerateInvestigationPlan(category);
            Assert.True(plan.Count >= 4, $"Expected >=4 steps for {category}, got {plan.Count}");
            Assert.All(plan, step => Assert.False(string.IsNullOrWhiteSpace(step)));
        }

        [Fact]
        public void GenerateInvestigationPlan_FailureSpike_ContainsErrorLogStep()
        {
            var engine = CreateEngine();
            var plan = engine.GenerateInvestigationPlan(TriageCategory.FailureSpike);
            Assert.Contains(plan, step => step.Contains("error log", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GenerateInvestigationPlan_Hallucination_ContainsTemperatureStep()
        {
            var engine = CreateEngine();
            var plan = engine.GenerateInvestigationPlan(TriageCategory.HallucinationSignal);
            Assert.Contains(plan, step => step.Contains("temperature", StringComparison.OrdinalIgnoreCase));
        }

        // ── AutoTriage ───────────────────────────────────

        [Fact]
        public void AutoTriage_EscalatesHighSignalCountIncidents()
        {
            var config = new TriageConfig { AutoEscalateThreshold = 3 };
            var engine = CreateEngine(config);
            var now = DateTime.UtcNow;

            engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now));
            for (int i = 0; i < 4; i++)
                engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now.AddMinutes(i + 1)));

            var report = engine.AutoTriage();
            var incident = engine.GetOpenIncidents().First();

            Assert.True(incident.Severity < TriageSeverity.Low); // escalated (lower enum = higher severity)
        }

        [Fact]
        public void AutoTriage_ReturnsReport()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal());
            var report = engine.AutoTriage();

            Assert.NotNull(report);
            Assert.Equal(1, report.TotalIncidents);
        }

        [Fact]
        public void AutoTriage_DoesNotEscalateBelowThreshold()
        {
            var config = new TriageConfig { AutoEscalateThreshold = 10 };
            var engine = CreateEngine(config);
            var now = DateTime.UtcNow;

            engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now));
            engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now.AddMinutes(1)));

            engine.AutoTriage();
            var incident = engine.GetOpenIncidents().First();
            Assert.Equal(TriageSeverity.Low, incident.Severity);
        }

        [Fact]
        public void AutoTriage_SkipsResolvedIncidents()
        {
            var config = new TriageConfig { AutoEscalateThreshold = 2 };
            var engine = CreateEngine(config);
            var now = DateTime.UtcNow;

            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now));
            for (int i = 0; i < 3; i++)
                engine.IngestSignal(MakeSignal(severity: TriageSeverity.Low, timestamp: now.AddMinutes(i + 1)));

            engine.UpdateStatus(inc.Id, IncidentStatus.Resolved);
            engine.AutoTriage();

            var fetched = engine.GetIncident(inc.Id);
            // Severity should stay Low since it was resolved before auto-triage
            Assert.Equal(TriageSeverity.Low, fetched!.Severity);
        }

        // ── GetTriageReport ──────────────────────────────

        [Fact]
        public void GetTriageReport_EmptyEngine()
        {
            var engine = CreateEngine();
            var report = engine.GetTriageReport();

            Assert.Equal(0, report.TotalIncidents);
            Assert.Equal(100, report.HealthScore);
            Assert.NotEmpty(report.AutonomousInsights);
        }

        [Fact]
        public void GetTriageReport_CountsCorrectly()
        {
            var engine = CreateEngine();
            var inc1 = engine.IngestSignal(MakeSignal(promptId: "p1", category: TriageCategory.FailureSpike));
            var inc2 = engine.IngestSignal(MakeSignal(promptId: "p2", category: TriageCategory.CostAnomaly));
            engine.UpdateStatus(inc1.Id, IncidentStatus.Resolved);

            var report = engine.GetTriageReport();
            Assert.Equal(2, report.TotalIncidents);
            Assert.Equal(1, report.OpenCount);
            Assert.Equal(1, report.ResolvedCount);
        }

        [Fact]
        public void GetTriageReport_CalculatesMTTR()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());
            engine.UpdateStatus(inc.Id, IncidentStatus.Resolved, "Fixed");

            var report = engine.GetTriageReport();
            Assert.True(report.MeanTimeToResolveMinutes >= 0);
        }

        [Fact]
        public void GetTriageReport_ByCategoryPopulated()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(category: TriageCategory.FailureSpike));
            engine.IngestSignal(MakeSignal(promptId: "p2", category: TriageCategory.CostAnomaly));

            var report = engine.GetTriageReport();
            Assert.True(report.ByCategory.ContainsKey(TriageCategory.FailureSpike));
            Assert.True(report.ByCategory.ContainsKey(TriageCategory.CostAnomaly));
        }

        [Fact]
        public void GetTriageReport_BySeverityPopulated()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(severity: TriageSeverity.Critical));
            engine.IngestSignal(MakeSignal(promptId: "p2", severity: TriageSeverity.Low, category: TriageCategory.QualityDrift));

            var report = engine.GetTriageReport();
            Assert.True(report.BySeverity.ContainsKey(TriageSeverity.Critical));
            Assert.True(report.BySeverity.ContainsKey(TriageSeverity.Low));
        }

        [Fact]
        public void GetTriageReport_HealthDeductedByOpenIncidents()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(severity: TriageSeverity.Critical));

            var report = engine.GetTriageReport();
            Assert.True(report.HealthScore < 100);
        }

        [Fact]
        public void GetTriageReport_TopIncidentsRankedByBlastRadius()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(promptId: "low", severity: TriageSeverity.Info, category: TriageCategory.QualityDrift));
            engine.IngestSignal(MakeSignal(promptId: "high", severity: TriageSeverity.Critical));

            var report = engine.GetTriageReport();
            Assert.Equal("high", report.TopIncidents.First().PromptId);
        }

        [Fact]
        public void GetTriageReport_HasInsights()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal());
            var report = engine.GetTriageReport();
            Assert.NotEmpty(report.AutonomousInsights);
        }

        [Fact]
        public void GetTriageReport_DismissedCountCorrect()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());
            engine.DismissIncident(inc.Id, "False positive");

            var report = engine.GetTriageReport();
            Assert.Equal(1, report.DismissedCount);
        }

        // ── Dashboard ────────────────────────────────────

        [Fact]
        public void GenerateDashboard_ReturnsHtml()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal());
            var html = engine.GenerateDashboard();

            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Prompt Triage Dashboard", html);
        }

        [Fact]
        public void GenerateDashboard_ContainsIncidentData()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(promptId: "test-prompt"));
            var html = engine.GenerateDashboard();

            Assert.Contains("test-prompt", html);
            Assert.Contains("FailureSpike", html);
        }

        [Fact]
        public void GenerateDashboard_EmptyEngine_StillValid()
        {
            var engine = CreateEngine();
            var html = engine.GenerateDashboard();
            Assert.Contains("<!DOCTYPE html>", html);
        }

        [Fact]
        public void GenerateDashboard_ContainsInsightsSection()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal());
            var html = engine.GenerateDashboard();
            Assert.Contains("Autonomous Insights", html);
        }

        // ── Urgency ──────────────────────────────────────

        [Theory]
        [InlineData(TriageSeverity.Critical, TriageUrgency.Immediate)]
        [InlineData(TriageSeverity.High, TriageUrgency.Urgent)]
        [InlineData(TriageSeverity.Medium, TriageUrgency.Scheduled)]
        [InlineData(TriageSeverity.Low, TriageUrgency.Backlog)]
        [InlineData(TriageSeverity.Info, TriageUrgency.Backlog)]
        public void Urgency_MatchesSeverity(TriageSeverity severity, TriageUrgency expectedUrgency)
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal(severity: severity));
            Assert.Equal(expectedUrgency, inc.Urgency);
        }

        [Fact]
        public void Urgency_DisabledRules_DefaultsToScheduled()
        {
            var config = new TriageConfig { UrgencyRulesEnabled = false };
            var engine = CreateEngine(config);
            var inc = engine.IngestSignal(MakeSignal(severity: TriageSeverity.Critical));
            Assert.Equal(TriageUrgency.Scheduled, inc.Urgency);
        }

        // ── Multi-prompt scenarios ───────────────────────

        [Fact]
        public void MultiPrompt_IndependentIncidents()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(promptId: "alpha", category: TriageCategory.FailureSpike));
            engine.IngestSignal(MakeSignal(promptId: "beta", category: TriageCategory.FailureSpike));
            engine.IngestSignal(MakeSignal(promptId: "gamma", category: TriageCategory.HallucinationSignal));

            var report = engine.GetTriageReport();
            Assert.Equal(3, report.TotalIncidents);
        }

        [Fact]
        public void MultiPrompt_DifferentCategoriesSamePrompt()
        {
            var engine = CreateEngine();
            engine.IngestSignal(MakeSignal(category: TriageCategory.FailureSpike));
            engine.IngestSignal(MakeSignal(category: TriageCategory.CostAnomaly));
            engine.IngestSignal(MakeSignal(category: TriageCategory.HallucinationSignal));

            var byPrompt = engine.GetIncidentsByPrompt("prompt-1");
            Assert.Equal(3, byPrompt.Count);
        }

        // ── Status lifecycle ─────────────────────────────

        [Fact]
        public void StatusLifecycle_FullTransition()
        {
            var engine = CreateEngine();
            var inc = engine.IngestSignal(MakeSignal());

            Assert.Equal(IncidentStatus.Open, inc.Status);

            engine.UpdateStatus(inc.Id, IncidentStatus.Investigating);
            Assert.Equal(IncidentStatus.Investigating, engine.GetIncident(inc.Id)!.Status);

            engine.UpdateStatus(inc.Id, IncidentStatus.Mitigating);
            Assert.Equal(IncidentStatus.Mitigating, engine.GetIncident(inc.Id)!.Status);

            engine.UpdateStatus(inc.Id, IncidentStatus.Resolved, "Root cause fixed");
            var resolved = engine.GetIncident(inc.Id)!;
            Assert.Equal(IncidentStatus.Resolved, resolved.Status);
            Assert.NotNull(resolved.ResolvedAt);
            Assert.Equal("Root cause fixed", resolved.ResolutionNotes);
        }

        // ── Edge cases ───────────────────────────────────

        [Fact]
        public void DefaultConfig_HasReasonableDefaults()
        {
            var config = new TriageConfig();
            Assert.Equal(30, config.DeduplicationWindowMinutes);
            Assert.Equal(5, config.AutoEscalateThreshold);
            Assert.True(config.UrgencyRulesEnabled);
        }

        [Fact]
        public void Signal_DefaultValues()
        {
            var signal = new TriageSignal();
            Assert.Equal("", signal.PromptId);
            Assert.Equal(TriageSeverity.Medium, signal.Severity);
            Assert.Empty(signal.Tags);
            Assert.Null(signal.NumericValue);
        }

        [Fact]
        public void Incident_DefaultValues()
        {
            var incident = new TriageIncident();
            Assert.Equal(IncidentStatus.Open, incident.Status);
            Assert.Equal(1, incident.SignalCount);
            Assert.Empty(incident.AffectedPrompts);
            Assert.Null(incident.ResolvedAt);
        }

        [Fact]
        public void HealthScore_MultipleOpenCriticals_DropsSignificantly()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.IngestSignal(MakeSignal(promptId: $"p{i}", severity: TriageSeverity.Critical, category: TriageCategory.FailureSpike));

            var report = engine.GetTriageReport();
            Assert.True(report.HealthScore <= 0); // 5 * 20 = 100 deduction
        }
    }
}
