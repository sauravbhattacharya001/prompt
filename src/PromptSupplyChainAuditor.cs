namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ────────────────────────────────────────────
    //  PromptSupplyChainAuditor – Autonomous Dependency Supply Chain Analysis
    //
    //  Treats prompt pipelines as supply chains: each sub-prompt, template,
    //  context source, few-shot example, or external data feed is a "supplier".
    //  The auditor autonomously:
    //    1. Maps the full supply graph (tiers, critical paths)
    //    2. Detects single-points-of-failure (sole-source dependencies)
    //    3. Assesses freshness risk (stale suppliers, version drift)
    //    4. Scores supply chain resilience (0-100)
    //    5. Identifies vulnerability propagation paths
    //    6. Generates autonomous diversification recommendations
    //    7. Tracks supplier reliability history
    //
    //  Unlike PromptDependencyGraph (structural mapping) or PromptRiskForecaster
    //  (trend-based prediction), this engine focuses on supply-chain-specific
    //  risks: concentration, freshness, substitutability, and cascading failure.
    // ────────────────────────────────────────────

    /// <summary>Classification of supplier type in the prompt supply chain.</summary>
    public enum SupplierType
    {
        /// <summary>A sub-prompt or template fragment.</summary>
        Template,
        /// <summary>Few-shot examples providing in-context learning.</summary>
        FewShotExamples,
        /// <summary>External context source (database, API, retrieval).</summary>
        ContextSource,
        /// <summary>System prompt or instruction layer.</summary>
        SystemInstruction,
        /// <summary>User input or variable injection.</summary>
        UserInput,
        /// <summary>Model or routing configuration.</summary>
        ModelConfig,
        /// <summary>Guard or safety filter in the pipeline.</summary>
        SafetyFilter,
        /// <summary>Post-processing or output formatting step.</summary>
        PostProcessor
    }

    /// <summary>Risk category for supply chain vulnerabilities.</summary>
    public enum SupplyRiskCategory
    {
        /// <summary>Single point of failure — no alternative supplier.</summary>
        SoleSource,
        /// <summary>Supplier data is stale or outdated.</summary>
        Freshness,
        /// <summary>Supplier has unreliable delivery history.</summary>
        Reliability,
        /// <summary>Vulnerability can cascade through chain.</summary>
        CascadeRisk,
        /// <summary>Supplier version mismatch with consumer expectations.</summary>
        VersionDrift,
        /// <summary>Supplier is deprecated or approaching end-of-life.</summary>
        Deprecation,
        /// <summary>Excessive concentration in one supplier tier.</summary>
        Concentration,
        /// <summary>Supplier has known quality issues.</summary>
        QualityDefect
    }

    /// <summary>Severity of a supply chain finding.</summary>
    public enum SupplyRiskSeverity
    {
        /// <summary>Informational, no immediate risk.</summary>
        Low,
        /// <summary>Should be addressed in normal planning.</summary>
        Medium,
        /// <summary>Requires prompt attention.</summary>
        High,
        /// <summary>Immediate action required — active or imminent failure.</summary>
        Critical
    }

    /// <summary>Supply chain tier classification.</summary>
    public enum SupplyTier
    {
        /// <summary>Direct supplier to the final prompt.</summary>
        Tier1,
        /// <summary>Supplier to a Tier 1 supplier.</summary>
        Tier2,
        /// <summary>Deep dependency (supplier to Tier 2+).</summary>
        Tier3Plus
    }

    /// <summary>Type of diversification recommendation.</summary>
    public enum DiversificationAction
    {
        /// <summary>Add an alternative supplier for redundancy.</summary>
        AddAlternative,
        /// <summary>Implement a fallback path if supplier fails.</summary>
        AddFallback,
        /// <summary>Cache supplier output for freshness resilience.</summary>
        AddCaching,
        /// <summary>Pin supplier to a specific version.</summary>
        PinVersion,
        /// <summary>Replace supplier with a more reliable one.</summary>
        ReplaceSupplier,
        /// <summary>Split concentrated dependency across multiple sources.</summary>
        SplitConcentration,
        /// <summary>Add monitoring/alerting for early failure detection.</summary>
        AddMonitoring,
        /// <summary>Pre-compute or pre-fetch to reduce runtime dependency.</summary>
        PreCompute
    }

    /// <summary>Represents a supplier in the prompt supply chain.</summary>
    public sealed class PromptSupplier
    {
        /// <summary>Unique identifier for this supplier.</summary>
        public string Id { get; }

        /// <summary>Human-readable name.</summary>
        public string Name { get; set; }

        /// <summary>Type of supplier.</summary>
        public SupplierType Type { get; set; }

        /// <summary>Supply chain tier.</summary>
        public SupplyTier Tier { get; set; } = SupplyTier.Tier1;

        /// <summary>When this supplier's content was last updated.</summary>
        public DateTimeOffset? LastUpdated { get; set; }

        /// <summary>Maximum acceptable age before considered stale (hours).</summary>
        public double FreshnessThresholdHours { get; set; } = 168; // 1 week default

        /// <summary>Supplier version string.</summary>
        public string? Version { get; set; }

        /// <summary>Whether this supplier can be substituted by alternatives.</summary>
        public bool IsSubstitutable { get; set; } = true;

        /// <summary>IDs of alternative suppliers that can replace this one.</summary>
        public List<string> Alternatives { get; } = new();

        /// <summary>IDs of suppliers this one depends on (sub-suppliers).</summary>
        public List<string> SubSuppliers { get; } = new();

        /// <summary>Historical reliability events.</summary>
        public List<ReliabilityEvent> ReliabilityHistory { get; } = new();

        /// <summary>Tags for grouping and filtering.</summary>
        public Dictionary<string, string> Tags { get; } = new();

        /// <summary>Whether this supplier is marked as deprecated.</summary>
        public bool IsDeprecated { get; set; }

        /// <summary>Expected deprecation date (null if not applicable).</summary>
        public DateTimeOffset? DeprecationDate { get; set; }

        /// <summary>Create a supplier with ID and name.</summary>
        public PromptSupplier(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Supplier id required.", nameof(id));
            Id = id;
            Name = name ?? id;
        }
    }

    /// <summary>A reliability event recorded for a supplier.</summary>
    public sealed class ReliabilityEvent
    {
        /// <summary>When the event occurred.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>Whether the supplier delivered successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Latency in milliseconds (if applicable).</summary>
        public double? LatencyMs { get; set; }

        /// <summary>Optional failure reason.</summary>
        public string? FailureReason { get; set; }

        public ReliabilityEvent(DateTimeOffset timestamp, bool success)
        {
            Timestamp = timestamp;
            Success = success;
        }
    }

    /// <summary>A supply chain vulnerability finding.</summary>
    public sealed class SupplyChainFinding
    {
        /// <summary>Unique finding identifier.</summary>
        public string FindingId { get; }

        /// <summary>Risk category.</summary>
        public SupplyRiskCategory Category { get; set; }

        /// <summary>Severity level.</summary>
        public SupplyRiskSeverity Severity { get; set; }

        /// <summary>ID of the affected supplier.</summary>
        public string SupplierId { get; set; }

        /// <summary>Human-readable description.</summary>
        public string Description { get; set; }

        /// <summary>IDs of suppliers in the propagation path (for cascades).</summary>
        public List<string> PropagationPath { get; } = new();

        /// <summary>Estimated impact score 0-100.</summary>
        public double ImpactScore { get; set; }

        /// <summary>Recommended remediation.</summary>
        public DiversificationAction RecommendedAction { get; set; }

        /// <summary>Detailed remediation guidance.</summary>
        public string? RemediationDetail { get; set; }

        public SupplyChainFinding(string findingId, string supplierId, string description)
        {
            FindingId = findingId;
            SupplierId = supplierId;
            Description = description;
        }
    }

    /// <summary>Supplier reliability score summary.</summary>
    public sealed class SupplierReliabilityScore
    {
        /// <summary>Supplier ID.</summary>
        public string SupplierId { get; set; } = "";

        /// <summary>Overall reliability 0-100.</summary>
        public double Score { get; set; }

        /// <summary>Total events tracked.</summary>
        public int TotalEvents { get; set; }

        /// <summary>Success rate 0-1.</summary>
        public double SuccessRate { get; set; }

        /// <summary>Mean latency in ms.</summary>
        public double MeanLatencyMs { get; set; }

        /// <summary>Trend: improving, stable, degrading.</summary>
        public string Trend { get; set; } = "stable";
    }

    /// <summary>Complete supply chain audit report.</summary>
    public sealed class SupplyChainAuditReport
    {
        /// <summary>When the audit was performed.</summary>
        public DateTimeOffset AuditTimestamp { get; set; }

        /// <summary>Total suppliers in the chain.</summary>
        public int TotalSuppliers { get; set; }

        /// <summary>Number of unique supply tiers.</summary>
        public int TierDepth { get; set; }

        /// <summary>Overall resilience score 0-100.</summary>
        public double ResilienceScore { get; set; }

        /// <summary>Resilience grade (A-F).</summary>
        public string Grade { get; set; } = "C";

        /// <summary>All findings from the audit.</summary>
        public List<SupplyChainFinding> Findings { get; } = new();

        /// <summary>Critical path through the supply chain.</summary>
        public List<string> CriticalPath { get; } = new();

        /// <summary>Suppliers identified as single points of failure.</summary>
        public List<string> SinglePointsOfFailure { get; } = new();

        /// <summary>Concentration analysis: type -> count.</summary>
        public Dictionary<SupplierType, int> ConcentrationMap { get; } = new();

        /// <summary>Reliability scores per supplier.</summary>
        public List<SupplierReliabilityScore> ReliabilityScores { get; } = new();

        /// <summary>Autonomous recommendations summary.</summary>
        public List<string> Recommendations { get; } = new();

        /// <summary>Render as formatted text report.</summary>
        public string ToTextReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════╗");
            sb.AppendLine("║     PROMPT SUPPLY CHAIN AUDIT REPORT                ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"  Audit Time:       {AuditTimestamp:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine($"  Total Suppliers:  {TotalSuppliers}");
            sb.AppendLine($"  Tier Depth:       {TierDepth}");
            sb.AppendLine($"  Resilience Score: {ResilienceScore:F1}/100  [{Grade}]");
            sb.AppendLine();

            if (SinglePointsOfFailure.Count > 0)
            {
                sb.AppendLine("  ⚠ SINGLE POINTS OF FAILURE:");
                foreach (var spof in SinglePointsOfFailure)
                    sb.AppendLine($"    • {spof}");
                sb.AppendLine();
            }

            if (CriticalPath.Count > 0)
            {
                sb.AppendLine($"  Critical Path: {string.Join(" → ", CriticalPath)}");
                sb.AppendLine();
            }

            if (Findings.Count > 0)
            {
                sb.AppendLine($"  FINDINGS ({Findings.Count}):");
                sb.AppendLine($"  {"ID",-12} {"Severity",-10} {"Category",-16} {"Supplier",-15} {"Impact",6}");
                sb.AppendLine($"  {new string('─', 65)}");
                foreach (var f in Findings.OrderByDescending(x => x.ImpactScore))
                {
                    sb.AppendLine($"  {f.FindingId,-12} {f.Severity,-10} {f.Category,-16} {f.SupplierId,-15} {f.ImpactScore,5:F0}%");
                }
                sb.AppendLine();
            }

            if (Recommendations.Count > 0)
            {
                sb.AppendLine("  RECOMMENDATIONS:");
                for (int i = 0; i < Recommendations.Count; i++)
                    sb.AppendLine($"    {i + 1}. {Recommendations[i]}");
            }

            return sb.ToString();
        }

        /// <summary>Serialize to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    /// <summary>
    /// Autonomous prompt supply chain auditor. Analyzes prompt pipeline
    /// dependencies as a supply chain, detecting vulnerabilities, concentration
    /// risks, freshness issues, and cascading failure paths.
    /// </summary>
    public sealed class PromptSupplyChainAuditor
    {
        private readonly List<PromptSupplier> _suppliers = new();
        private readonly Dictionary<string, List<string>> _consumerMap = new(); // consumerId -> supplierIds
        private int _findingCounter;

        /// <summary>All registered suppliers.</summary>
        public IReadOnlyList<PromptSupplier> Suppliers => _suppliers.AsReadOnly();

        /// <summary>Register a supplier in the supply chain.</summary>
        public PromptSupplyChainAuditor AddSupplier(PromptSupplier supplier)
        {
            if (supplier == null) throw new ArgumentNullException(nameof(supplier));
            if (_suppliers.Any(s => s.Id == supplier.Id))
                throw new ArgumentException($"Supplier '{supplier.Id}' already registered.");
            _suppliers.Add(supplier);
            return this;
        }

        /// <summary>Register a consumer relationship (consumer depends on supplier).</summary>
        public PromptSupplyChainAuditor AddConsumerRelationship(string consumerId, string supplierId)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Consumer ID required.", nameof(consumerId));
            if (string.IsNullOrWhiteSpace(supplierId))
                throw new ArgumentException("Supplier ID required.", nameof(supplierId));

            if (!_consumerMap.ContainsKey(consumerId))
                _consumerMap[consumerId] = new List<string>();
            if (!_consumerMap[consumerId].Contains(supplierId))
                _consumerMap[consumerId].Add(supplierId);
            return this;
        }

        /// <summary>Record a reliability event for a supplier.</summary>
        public PromptSupplyChainAuditor RecordReliability(string supplierId, DateTimeOffset timestamp, bool success, double? latencyMs = null, string? failureReason = null)
        {
            var supplier = _suppliers.FirstOrDefault(s => s.Id == supplierId)
                ?? throw new ArgumentException($"Supplier '{supplierId}' not found.");
            supplier.ReliabilityHistory.Add(new ReliabilityEvent(timestamp, success)
            {
                LatencyMs = latencyMs,
                FailureReason = failureReason
            });
            return this;
        }

        /// <summary>Run a full autonomous supply chain audit.</summary>
        public SupplyChainAuditReport Audit(DateTimeOffset? asOf = null)
        {
            var now = asOf ?? DateTimeOffset.UtcNow;
            var report = new SupplyChainAuditReport
            {
                AuditTimestamp = now,
                TotalSuppliers = _suppliers.Count,
                TierDepth = ComputeTierDepth()
            };

            // Build concentration map
            foreach (var s in _suppliers)
            {
                if (!report.ConcentrationMap.ContainsKey(s.Type))
                    report.ConcentrationMap[s.Type] = 0;
                report.ConcentrationMap[s.Type]++;
            }

            // Run all detection engines
            DetectSoleSourceRisks(report, now);
            DetectFreshnessRisks(report, now);
            DetectReliabilityRisks(report, now);
            DetectCascadeRisks(report, now);
            DetectVersionDrift(report, now);
            DetectDeprecationRisks(report, now);
            DetectConcentrationRisks(report, now);

            // Compute critical path
            ComputeCriticalPath(report);

            // Compute reliability scores
            ComputeReliabilityScores(report, now);

            // Calculate resilience score
            report.ResilienceScore = ComputeResilienceScore(report);
            report.Grade = ScoreToGrade(report.ResilienceScore);

            // Generate recommendations
            GenerateRecommendations(report);

            return report;
        }

        /// <summary>Get suppliers with no alternatives (sole-source).</summary>
        public IReadOnlyList<PromptSupplier> GetSoleSourceSuppliers()
        {
            return _suppliers
                .Where(s => s.Alternatives.Count == 0 && !s.IsSubstitutable == false)
                .Where(s => s.Alternatives.Count == 0)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>Get stale suppliers based on their freshness threshold.</summary>
        public IReadOnlyList<PromptSupplier> GetStaleSuppliers(DateTimeOffset? asOf = null)
        {
            var now = asOf ?? DateTimeOffset.UtcNow;
            return _suppliers
                .Where(s => s.LastUpdated.HasValue &&
                    (now - s.LastUpdated.Value).TotalHours > s.FreshnessThresholdHours)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>Compute the blast radius if a supplier fails (downstream consumers affected).</summary>
        public IReadOnlyList<string> ComputeBlastRadius(string supplierId)
        {
            var affected = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(supplierId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var kvp in _consumerMap)
                {
                    if (kvp.Value.Contains(current) && affected.Add(kvp.Key))
                    {
                        queue.Enqueue(kvp.Key);
                    }
                }
            }
            return affected.ToList().AsReadOnly();
        }

        /// <summary>Get all suppliers at a given tier.</summary>
        public IReadOnlyList<PromptSupplier> GetSuppliersByTier(SupplyTier tier)
        {
            return _suppliers.Where(s => s.Tier == tier).ToList().AsReadOnly();
        }

        /// <summary>Compute substitutability score for a supplier (0=irreplaceable, 100=fully substitutable).</summary>
        public double ComputeSubstitutability(string supplierId)
        {
            var supplier = _suppliers.FirstOrDefault(s => s.Id == supplierId);
            if (supplier == null) return 0;

            double score = 0;
            if (supplier.IsSubstitutable) score += 30;
            score += Math.Min(supplier.Alternatives.Count * 20.0, 50.0);
            if (supplier.Type == SupplierType.UserInput) score += 20; // inherently flexible
            return Math.Min(score, 100);
        }

        // ─── Detection Engines ───────────────────────────────────

        private void DetectSoleSourceRisks(SupplyChainAuditReport report, DateTimeOffset now)
        {
            foreach (var supplier in _suppliers)
            {
                if (supplier.Alternatives.Count == 0 && supplier.Type != SupplierType.UserInput)
                {
                    var blastRadius = ComputeBlastRadius(supplier.Id);
                    var impact = Math.Min(30 + blastRadius.Count * 15.0, 100.0);

                    var finding = new SupplyChainFinding(
                        $"SC-{++_findingCounter:D3}",
                        supplier.Id,
                        $"Sole-source dependency: '{supplier.Name}' has no alternatives. Blast radius: {blastRadius.Count} consumers.")
                    {
                        Category = SupplyRiskCategory.SoleSource,
                        Severity = impact > 70 ? SupplyRiskSeverity.Critical :
                                   impact > 50 ? SupplyRiskSeverity.High :
                                   SupplyRiskSeverity.Medium,
                        ImpactScore = impact,
                        RecommendedAction = DiversificationAction.AddAlternative,
                        RemediationDetail = $"Add at least one alternative supplier for '{supplier.Name}' or implement a fallback path."
                    };
                    finding.PropagationPath.AddRange(blastRadius);
                    report.Findings.Add(finding);
                    report.SinglePointsOfFailure.Add(supplier.Id);
                }
            }
        }

        private void DetectFreshnessRisks(SupplyChainAuditReport report, DateTimeOffset now)
        {
            foreach (var supplier in _suppliers)
            {
                if (!supplier.LastUpdated.HasValue) continue;
                var ageHours = (now - supplier.LastUpdated.Value).TotalHours;
                if (ageHours <= supplier.FreshnessThresholdHours) continue;

                var staleness = ageHours / supplier.FreshnessThresholdHours;
                var impact = Math.Min(staleness * 25.0, 90.0);

                report.Findings.Add(new SupplyChainFinding(
                    $"SC-{++_findingCounter:D3}",
                    supplier.Id,
                    $"Stale supplier: '{supplier.Name}' last updated {ageHours:F0}h ago (threshold: {supplier.FreshnessThresholdHours}h). Staleness ratio: {staleness:F1}x.")
                {
                    Category = SupplyRiskCategory.Freshness,
                    Severity = staleness > 3 ? SupplyRiskSeverity.High :
                               staleness > 1.5 ? SupplyRiskSeverity.Medium :
                               SupplyRiskSeverity.Low,
                    ImpactScore = impact,
                    RecommendedAction = DiversificationAction.AddCaching,
                    RemediationDetail = $"Update supplier content or add caching to decouple from real-time freshness."
                });
            }
        }

        private void DetectReliabilityRisks(SupplyChainAuditReport report, DateTimeOffset now)
        {
            foreach (var supplier in _suppliers)
            {
                if (supplier.ReliabilityHistory.Count < 3) continue;

                var recent = supplier.ReliabilityHistory
                    .OrderByDescending(e => e.Timestamp)
                    .Take(20)
                    .ToList();

                var successRate = recent.Count(e => e.Success) / (double)recent.Count;
                if (successRate >= 0.9) continue;

                var impact = (1 - successRate) * 100;

                report.Findings.Add(new SupplyChainFinding(
                    $"SC-{++_findingCounter:D3}",
                    supplier.Id,
                    $"Unreliable supplier: '{supplier.Name}' has {successRate:P0} success rate over last {recent.Count} events.")
                {
                    Category = SupplyRiskCategory.Reliability,
                    Severity = successRate < 0.5 ? SupplyRiskSeverity.Critical :
                               successRate < 0.7 ? SupplyRiskSeverity.High :
                               SupplyRiskSeverity.Medium,
                    ImpactScore = impact,
                    RecommendedAction = DiversificationAction.ReplaceSupplier,
                    RemediationDetail = $"Supplier reliability is below threshold. Consider replacement or adding circuit-breaker fallback."
                });
            }
        }

        private void DetectCascadeRisks(SupplyChainAuditReport report, DateTimeOffset now)
        {
            foreach (var supplier in _suppliers)
            {
                var blastRadius = ComputeBlastRadius(supplier.Id);
                if (blastRadius.Count < 3) continue;

                // Check if any in the blast radius are also sole-source
                var cascadingSpofs = blastRadius
                    .Where(id => _suppliers.Any(s => s.Id == id && s.Alternatives.Count == 0))
                    .ToList();

                if (cascadingSpofs.Count == 0) continue;

                var impact = Math.Min(40 + cascadingSpofs.Count * 20.0, 100.0);

                var finding = new SupplyChainFinding(
                    $"SC-{++_findingCounter:D3}",
                    supplier.Id,
                    $"Cascade risk: failure of '{supplier.Name}' would cascade through {blastRadius.Count} consumers, {cascadingSpofs.Count} of which are also sole-source.")
                {
                    Category = SupplyRiskCategory.CascadeRisk,
                    Severity = SupplyRiskSeverity.High,
                    ImpactScore = impact,
                    RecommendedAction = DiversificationAction.AddFallback,
                    RemediationDetail = $"Add fallback paths to break the cascade chain. Prioritize diversifying: {string.Join(", ", cascadingSpofs.Take(3))}."
                };
                finding.PropagationPath.Add(supplier.Id);
                finding.PropagationPath.AddRange(blastRadius);
                report.Findings.Add(finding);
            }
        }

        private void DetectVersionDrift(SupplyChainAuditReport report, DateTimeOffset now)
        {
            // Group suppliers by type and check for version inconsistencies
            var versionedSuppliers = _suppliers.Where(s => !string.IsNullOrEmpty(s.Version)).ToList();
            var byType = versionedSuppliers.GroupBy(s => s.Type);

            foreach (var group in byType)
            {
                var versions = group.Select(s => s.Version!).Distinct().ToList();
                if (versions.Count <= 1) continue;

                foreach (var supplier in group)
                {
                    var otherVersions = versions.Where(v => v != supplier.Version).ToList();
                    report.Findings.Add(new SupplyChainFinding(
                        $"SC-{++_findingCounter:D3}",
                        supplier.Id,
                        $"Version drift: '{supplier.Name}' at v{supplier.Version}, but peers use: {string.Join(", ", otherVersions.Select(v => $"v{v}"))}.")
                    {
                        Category = SupplyRiskCategory.VersionDrift,
                        Severity = SupplyRiskSeverity.Low,
                        ImpactScore = 25,
                        RecommendedAction = DiversificationAction.PinVersion,
                        RemediationDetail = "Align supplier versions or explicitly pin to avoid drift-related incompatibilities."
                    });
                }
            }
        }

        private void DetectDeprecationRisks(SupplyChainAuditReport report, DateTimeOffset now)
        {
            foreach (var supplier in _suppliers)
            {
                if (!supplier.IsDeprecated && !supplier.DeprecationDate.HasValue) continue;

                double impact;
                SupplyRiskSeverity severity;
                string desc;

                if (supplier.IsDeprecated)
                {
                    impact = 70;
                    severity = SupplyRiskSeverity.High;
                    desc = $"Deprecated supplier: '{supplier.Name}' is marked as deprecated and should be replaced.";
                }
                else if (supplier.DeprecationDate.HasValue)
                {
                    var daysUntil = (supplier.DeprecationDate.Value - now).TotalDays;
                    if (daysUntil > 90)
                    {
                        impact = 30;
                        severity = SupplyRiskSeverity.Low;
                    }
                    else if (daysUntil > 30)
                    {
                        impact = 50;
                        severity = SupplyRiskSeverity.Medium;
                    }
                    else
                    {
                        impact = 80;
                        severity = SupplyRiskSeverity.High;
                    }
                    desc = $"Approaching deprecation: '{supplier.Name}' will be deprecated in {daysUntil:F0} days.";
                }
                else continue;

                report.Findings.Add(new SupplyChainFinding(
                    $"SC-{++_findingCounter:D3}",
                    supplier.Id,
                    desc)
                {
                    Category = SupplyRiskCategory.Deprecation,
                    Severity = severity,
                    ImpactScore = impact,
                    RecommendedAction = DiversificationAction.ReplaceSupplier,
                    RemediationDetail = "Identify and migrate to a replacement supplier before end-of-life."
                });
            }
        }

        private void DetectConcentrationRisks(SupplyChainAuditReport report, DateTimeOffset now)
        {
            var totalSuppliers = _suppliers.Count;
            if (totalSuppliers < 4) return;

            foreach (var kvp in report.ConcentrationMap)
            {
                var ratio = kvp.Value / (double)totalSuppliers;
                if (ratio <= 0.5) continue;

                report.Findings.Add(new SupplyChainFinding(
                    $"SC-{++_findingCounter:D3}",
                    $"type:{kvp.Key}",
                    $"High concentration: {kvp.Value}/{totalSuppliers} suppliers ({ratio:P0}) are type '{kvp.Key}'. A systemic issue in this category could disable the entire chain.")
                {
                    Category = SupplyRiskCategory.Concentration,
                    Severity = ratio > 0.75 ? SupplyRiskSeverity.High : SupplyRiskSeverity.Medium,
                    ImpactScore = ratio * 80,
                    RecommendedAction = DiversificationAction.SplitConcentration,
                    RemediationDetail = $"Diversify supplier types. Consider splitting '{kvp.Key}' responsibilities across different supply strategies."
                });
            }
        }

        // ─── Computation Helpers ────────────────────────────────

        private int ComputeTierDepth()
        {
            var tiers = _suppliers.Select(s => s.Tier).Distinct().Count();
            return Math.Max(tiers, 1);
        }

        private void ComputeCriticalPath(SupplyChainAuditReport report)
        {
            // Critical path = longest dependency chain through sole-source suppliers
            var spofs = new HashSet<string>(report.SinglePointsOfFailure);
            if (spofs.Count == 0) return;

            // BFS from each root (suppliers with no sub-suppliers) through SPOF chain
            var roots = _suppliers.Where(s => s.SubSuppliers.Count == 0 && spofs.Contains(s.Id)).ToList();
            var longestPath = new List<string>();

            foreach (var root in roots)
            {
                var path = new List<string> { root.Id };
                TraverseCriticalPath(root.Id, spofs, path, longestPath);
            }

            if (longestPath.Count == 0 && spofs.Count > 0)
                longestPath.AddRange(spofs.Take(5));

            report.CriticalPath.AddRange(longestPath);
        }

        private void TraverseCriticalPath(string currentId, HashSet<string> spofs, List<string> currentPath, List<string> longestPath)
        {
            // Find consumers of current that are also SPOFs
            var nextHops = _consumerMap
                .Where(kvp => kvp.Value.Contains(currentId) && spofs.Contains(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();

            if (nextHops.Count == 0)
            {
                if (currentPath.Count > longestPath.Count)
                {
                    longestPath.Clear();
                    longestPath.AddRange(currentPath);
                }
                return;
            }

            foreach (var next in nextHops)
            {
                if (currentPath.Contains(next)) continue; // prevent cycles
                currentPath.Add(next);
                TraverseCriticalPath(next, spofs, currentPath, longestPath);
                currentPath.RemoveAt(currentPath.Count - 1);
            }
        }

        private void ComputeReliabilityScores(SupplyChainAuditReport report, DateTimeOffset now)
        {
            foreach (var supplier in _suppliers)
            {
                var events = supplier.ReliabilityHistory;
                if (events.Count == 0)
                {
                    report.ReliabilityScores.Add(new SupplierReliabilityScore
                    {
                        SupplierId = supplier.Id,
                        Score = 75, // Unknown = neutral
                        TotalEvents = 0,
                        SuccessRate = 1.0,
                        MeanLatencyMs = 0,
                        Trend = "unknown"
                    });
                    continue;
                }

                var ordered = events.OrderByDescending(e => e.Timestamp).ToList();
                var successRate = ordered.Count(e => e.Success) / (double)ordered.Count;
                var meanLatency = ordered.Where(e => e.LatencyMs.HasValue).Select(e => e.LatencyMs!.Value).DefaultIfEmpty(0).Average();

                // Trend: compare first half vs second half success rate
                var halfPoint = ordered.Count / 2;
                var recentHalf = ordered.Take(Math.Max(halfPoint, 1)).ToList();
                var olderHalf = ordered.Skip(halfPoint).ToList();
                var recentRate = recentHalf.Count(e => e.Success) / (double)recentHalf.Count;
                var olderRate = olderHalf.Count > 0 ? olderHalf.Count(e => e.Success) / (double)olderHalf.Count : recentRate;

                string trend = "stable";
                if (recentRate - olderRate > 0.1) trend = "improving";
                else if (olderRate - recentRate > 0.1) trend = "degrading";

                var score = successRate * 80 + (trend == "improving" ? 20 : trend == "stable" ? 10 : 0);

                report.ReliabilityScores.Add(new SupplierReliabilityScore
                {
                    SupplierId = supplier.Id,
                    Score = Math.Min(score, 100),
                    TotalEvents = events.Count,
                    SuccessRate = successRate,
                    MeanLatencyMs = meanLatency,
                    Trend = trend
                });
            }
        }

        private double ComputeResilienceScore(SupplyChainAuditReport report)
        {
            if (_suppliers.Count == 0) return 100;

            double score = 100;

            // Penalize for SPOFs
            var spofRatio = report.SinglePointsOfFailure.Count / (double)_suppliers.Count;
            score -= spofRatio * 35;

            // Penalize for critical/high findings
            var criticalCount = report.Findings.Count(f => f.Severity == SupplyRiskSeverity.Critical);
            var highCount = report.Findings.Count(f => f.Severity == SupplyRiskSeverity.High);
            score -= criticalCount * 10;
            score -= highCount * 5;

            // Penalize for low average reliability
            var avgReliability = report.ReliabilityScores.Any()
                ? report.ReliabilityScores.Average(r => r.Score)
                : 75;
            if (avgReliability < 70) score -= (70 - avgReliability);

            // Reward for tier depth (more tiers = more structure but also more risk)
            if (report.TierDepth == 1) score += 5; // flat = simple

            // Penalize for stale suppliers
            var staleCount = report.Findings.Count(f => f.Category == SupplyRiskCategory.Freshness);
            score -= staleCount * 3;

            return Math.Max(0, Math.Min(100, score));
        }

        private void GenerateRecommendations(SupplyChainAuditReport report)
        {
            if (report.SinglePointsOfFailure.Count > 0)
            {
                report.Recommendations.Add(
                    $"PRIORITY: Add alternatives for {report.SinglePointsOfFailure.Count} sole-source suppliers: {string.Join(", ", report.SinglePointsOfFailure.Take(3))}.");
            }

            var cascadeFindings = report.Findings.Where(f => f.Category == SupplyRiskCategory.CascadeRisk).ToList();
            if (cascadeFindings.Count > 0)
            {
                report.Recommendations.Add(
                    $"Break cascade chains by adding fallback paths. Highest-risk cascade starts at: {cascadeFindings.First().SupplierId}.");
            }

            var unreliable = report.ReliabilityScores.Where(r => r.Score < 60).ToList();
            if (unreliable.Count > 0)
            {
                report.Recommendations.Add(
                    $"Replace or add circuit-breakers for {unreliable.Count} unreliable suppliers: {string.Join(", ", unreliable.Select(r => r.SupplierId).Take(3))}.");
            }

            var staleFindings = report.Findings.Where(f => f.Category == SupplyRiskCategory.Freshness).ToList();
            if (staleFindings.Count > 0)
            {
                report.Recommendations.Add(
                    $"Update {staleFindings.Count} stale suppliers or implement caching to decouple freshness requirements.");
            }

            var deprecated = report.Findings.Where(f => f.Category == SupplyRiskCategory.Deprecation).ToList();
            if (deprecated.Count > 0)
            {
                report.Recommendations.Add(
                    $"Migrate away from {deprecated.Count} deprecated supplier(s) before end-of-life.");
            }

            if (report.ResilienceScore >= 80)
            {
                report.Recommendations.Add("Supply chain health is good. Continue monitoring for drift.");
            }
            else if (report.ResilienceScore < 40)
            {
                report.Recommendations.Add("CRITICAL: Supply chain is fragile. Immediate diversification needed to avoid systemic failure.");
            }
        }

        private static string ScoreToGrade(double score) => score switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };
    }
}
