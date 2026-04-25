namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptFeedbackLoop – Autonomous Feedback-Driven Prompt Improvement
    //
    //  Collects structured feedback on prompt outputs, detects recurring
    //  issue patterns via keyword frequency and category analysis, and
    //  autonomously suggests (or auto-applies) prompt refinements.
    //  Supports timeline analysis, category heatmaps, health scoring,
    //  proactive insights, and multi-format export.
    // ────────────────────────────────────────────

    /// <summary>Quality rating for a prompt output.</summary>
    public enum FeedbackRating
    {
        /// <summary>Outstanding output.</summary>
        Excellent = 5,
        /// <summary>Good output with minor issues.</summary>
        Good = 4,
        /// <summary>Acceptable but could be better.</summary>
        Acceptable = 3,
        /// <summary>Below expectations.</summary>
        Poor = 2,
        /// <summary>Unacceptable output.</summary>
        Terrible = 1
    }

    /// <summary>Category of feedback concern.</summary>
    public enum FeedbackCategory
    {
        /// <summary>Factual correctness.</summary>
        Accuracy,
        /// <summary>Relevance to the request.</summary>
        Relevance,
        /// <summary>Completeness of the response.</summary>
        Completeness,
        /// <summary>Tone and voice appropriateness.</summary>
        Tone,
        /// <summary>Output formatting quality.</summary>
        Format,
        /// <summary>Safety and content policy compliance.</summary>
        Safety,
        /// <summary>Creative quality.</summary>
        Creativity,
        /// <summary>Brevity and conciseness.</summary>
        Conciseness
    }

    /// <summary>Type of prompt refinement action.</summary>
    public enum RefinementType
    {
        /// <summary>Add a new instruction.</summary>
        AddInstruction,
        /// <summary>Remove an existing instruction.</summary>
        RemoveInstruction,
        /// <summary>Rephrase for clarity.</summary>
        RephraseClarify,
        /// <summary>Add an explicit constraint.</summary>
        AddConstraint,
        /// <summary>Add a few-shot example.</summary>
        AddExample,
        /// <summary>Adjust tone guidance.</summary>
        AdjustTone,
        /// <summary>Simplify prompt structure.</summary>
        SimplifyStructure
    }

    /// <summary>Export format for feedback loop reports.</summary>
    public enum FeedbackLoopExportFormat
    {
        /// <summary>JSON output.</summary>
        Json,
        /// <summary>Markdown output.</summary>
        Markdown,
        /// <summary>Styled HTML output.</summary>
        Html,
        /// <summary>Plain text output.</summary>
        Text
    }

    /// <summary>A single feedback entry for a prompt output.</summary>
    public sealed record FeedbackEntry
    {
        /// <summary>Unique identifier.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();
        /// <summary>Prompt being evaluated.</summary>
        public string PromptId { get; init; } = string.Empty;
        /// <summary>When the feedback was recorded.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        /// <summary>Quality rating.</summary>
        public FeedbackRating Rating { get; init; }
        /// <summary>Feedback category.</summary>
        public FeedbackCategory Category { get; init; }
        /// <summary>Human comment describing the issue.</summary>
        public string Comment { get; init; } = string.Empty;
        /// <summary>Optional snippet of the output being evaluated.</summary>
        public string? OutputSnippet { get; init; }
        /// <summary>Optional tags for classification.</summary>
        public List<string> Tags { get; init; } = new();
    }

    /// <summary>A recurring issue pattern detected in feedback.</summary>
    public sealed record FeedbackPattern
    {
        /// <summary>Pattern identifier (category + keyword).</summary>
        public string PatternId { get; init; } = string.Empty;
        /// <summary>Category of the pattern.</summary>
        public FeedbackCategory Category { get; init; }
        /// <summary>Human-readable description.</summary>
        public string Description { get; init; } = string.Empty;
        /// <summary>Number of occurrences.</summary>
        public int OccurrenceCount { get; init; }
        /// <summary>First time this pattern appeared.</summary>
        public DateTime FirstSeen { get; init; }
        /// <summary>Most recent occurrence.</summary>
        public DateTime LastSeen { get; init; }
        /// <summary>Severity score from 0.0 to 1.0.</summary>
        public double Severity { get; init; }
        /// <summary>Example comments exhibiting this pattern.</summary>
        public List<string> ExampleComments { get; init; } = new();
    }

    /// <summary>A suggested prompt refinement.</summary>
    public sealed record PromptRefinement
    {
        /// <summary>Unique identifier.</summary>
        public Guid RefinementId { get; init; } = Guid.NewGuid();
        /// <summary>Type of refinement.</summary>
        public RefinementType Type { get; init; }
        /// <summary>What this refinement does.</summary>
        public string Description { get; init; } = string.Empty;
        /// <summary>Original prompt segment (if applicable).</summary>
        public string OriginalSegment { get; init; } = string.Empty;
        /// <summary>Suggested replacement or addition.</summary>
        public string SuggestedSegment { get; init; } = string.Empty;
        /// <summary>Confidence score from 0.0 to 1.0.</summary>
        public double Confidence { get; init; }
        /// <summary>Pattern IDs that triggered this refinement.</summary>
        public List<string> TriggeredBy { get; init; } = new();
        /// <summary>When the refinement was generated.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    /// <summary>Feedback summary for a single prompt.</summary>
    public sealed record FeedbackSummary
    {
        /// <summary>Prompt identifier.</summary>
        public string PromptId { get; init; } = string.Empty;
        /// <summary>Total feedback entries.</summary>
        public int TotalFeedback { get; init; }
        /// <summary>Average rating (1-5 scale).</summary>
        public double AverageRating { get; init; }
        /// <summary>Average rating per category.</summary>
        public Dictionary<FeedbackCategory, double> CategoryBreakdown { get; init; } = new();
        /// <summary>Detected recurring patterns.</summary>
        public List<FeedbackPattern> DetectedPatterns { get; init; } = new();
        /// <summary>Suggested refinements.</summary>
        public List<PromptRefinement> SuggestedRefinements { get; init; } = new();
        /// <summary>Overall health score (0-100).</summary>
        public double HealthScore { get; init; }
        /// <summary>Proactive insight messages.</summary>
        public List<string> ProactiveInsights { get; init; } = new();
    }

    /// <summary>Cross-prompt feedback report.</summary>
    public sealed record FeedbackLoopReport
    {
        /// <summary>When the report was generated.</summary>
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
        /// <summary>Per-prompt summaries.</summary>
        public List<FeedbackSummary> Summaries { get; init; } = new();
        /// <summary>Global patterns across all prompts.</summary>
        public List<FeedbackPattern> GlobalPatterns { get; init; } = new();
        /// <summary>Top-priority refinements across all prompts.</summary>
        public List<PromptRefinement> TopRefinements { get; init; } = new();
        /// <summary>Export format used.</summary>
        public FeedbackLoopExportFormat ExportFormat { get; init; }
    }

    /// <summary>
    /// Autonomous feedback-driven prompt improvement engine.
    /// Collects structured feedback, detects recurring issue patterns,
    /// and suggests or auto-applies prompt refinements.
    /// </summary>
    public sealed class PromptFeedbackLoop
    {
        private readonly List<FeedbackEntry> _entries = new();
        private readonly Dictionary<string, string> _prompts = new();
        private readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "it", "was", "to", "and", "of", "in",
            "for", "that", "this", "with", "on", "at", "by", "from", "or",
            "be", "are", "not", "but", "has", "had", "have", "do", "does",
            "did", "will", "would", "could", "should", "may", "might", "can",
            "its", "my", "your", "we", "they", "he", "she", "i", "me", "you"
        };

        /// <summary>Whether auto-refinement is enabled.</summary>
        public bool AutoRefineEnabled { get; set; }

        /// <summary>Minimum occurrences to form a pattern (default 3).</summary>
        public int PatternThreshold { get; set; } = 3;

        /// <summary>Minimum confidence to auto-apply a refinement (default 0.6).</summary>
        public double RefinementConfidenceThreshold { get; set; } = 0.6;

        /// <summary>All registered prompt IDs.</summary>
        public IReadOnlyCollection<string> RegisteredPrompts => _prompts.Keys.ToList().AsReadOnly();

        /// <summary>Total feedback entries collected.</summary>
        public int TotalEntries => _entries.Count;

        /// <summary>Register a prompt for feedback tracking.</summary>
        /// <param name="promptId">Unique prompt identifier.</param>
        /// <param name="promptText">The prompt text.</param>
        public void RegisterPrompt(string promptId, string promptText)
        {
            ArgumentNullException.ThrowIfNull(promptId);
            ArgumentNullException.ThrowIfNull(promptText);
            _prompts[promptId] = promptText;
        }

        /// <summary>Add a single feedback entry.</summary>
        public FeedbackEntry AddFeedback(
            string promptId,
            FeedbackRating rating,
            FeedbackCategory category,
            string comment,
            string? outputSnippet = null,
            List<string>? tags = null)
        {
            var entry = new FeedbackEntry
            {
                PromptId = promptId,
                Rating = rating,
                Category = category,
                Comment = comment ?? string.Empty,
                OutputSnippet = outputSnippet,
                Tags = tags ?? new List<string>()
            };
            _entries.Add(entry);
            return entry;
        }

        /// <summary>Add multiple feedback entries at once.</summary>
        public int AddFeedbackBatch(IEnumerable<FeedbackEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            var list = entries.ToList();
            _entries.AddRange(list);
            return list.Count;
        }

        /// <summary>
        /// Detect recurring issue patterns in feedback via keyword frequency
        /// and category analysis. Optionally filter to a single prompt.
        /// </summary>
        public List<FeedbackPattern> DetectPatterns(string? promptId = null)
        {
            var relevant = promptId != null
                ? _entries.Where(e => e.PromptId == promptId).ToList()
                : _entries.ToList();

            if (relevant.Count == 0) return new List<FeedbackPattern>();

            var patterns = new List<FeedbackPattern>();
            var negativeFeedback = relevant
                .Where(e => e.Rating is FeedbackRating.Poor or FeedbackRating.Terrible)
                .ToList();

            foreach (var category in Enum.GetValues<FeedbackCategory>())
            {
                var categoryNegative = negativeFeedback.Where(e => e.Category == category).ToList();
                var categoryTotal = relevant.Where(e => e.Category == category).ToList();
                if (categoryNegative.Count == 0) continue;

                // Extract keywords from negative comments
                var keywordEntries = new Dictionary<string, List<FeedbackEntry>>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in categoryNegative)
                {
                    var words = ExtractKeywords(entry.Comment);
                    foreach (var word in words)
                    {
                        if (!keywordEntries.ContainsKey(word))
                            keywordEntries[word] = new List<FeedbackEntry>();
                        keywordEntries[word].Add(entry);
                    }
                }

                foreach (var (keyword, entries2) in keywordEntries)
                {
                    if (entries2.Count < PatternThreshold) continue;

                    var severity = categoryTotal.Count > 0
                        ? (double)entries2.Count / categoryTotal.Count
                        : 0.0;

                    patterns.Add(new FeedbackPattern
                    {
                        PatternId = $"{category}:{keyword}",
                        Category = category,
                        Description = $"Recurring '{keyword}' issue in {category} feedback ({entries2.Count} occurrences)",
                        OccurrenceCount = entries2.Count,
                        FirstSeen = entries2.Min(e => e.Timestamp),
                        LastSeen = entries2.Max(e => e.Timestamp),
                        Severity = Math.Min(severity, 1.0),
                        ExampleComments = entries2.Take(3).Select(e => e.Comment).ToList()
                    });
                }
            }

            return patterns.OrderByDescending(p => p.Severity).ThenByDescending(p => p.OccurrenceCount).ToList();
        }

        /// <summary>Generate refinement suggestions for a prompt based on detected patterns.</summary>
        public List<PromptRefinement> SuggestRefinements(string promptId)
        {
            var patterns = DetectPatterns(promptId);
            var totalEntries = _entries.Count(e => e.PromptId == promptId);
            var refinements = new List<PromptRefinement>();

            var categoryRefinements = new Dictionary<FeedbackCategory, (RefinementType Type, string Description, string Suggestion)>
            {
                [FeedbackCategory.Accuracy] = (RefinementType.AddConstraint, "Add explicit accuracy constraints", "Ensure all facts are verified and cite sources when possible."),
                [FeedbackCategory.Relevance] = (RefinementType.RephraseClarify, "Clarify the scope and focus", "Focus specifically on the requested topic without tangential information."),
                [FeedbackCategory.Completeness] = (RefinementType.AddInstruction, "Add instruction to cover missing aspects", "Provide a comprehensive response covering all aspects of the question."),
                [FeedbackCategory.Tone] = (RefinementType.AdjustTone, "Adjust tone guidance", "Use a professional yet approachable tone appropriate for the audience."),
                [FeedbackCategory.Format] = (RefinementType.AddConstraint, "Add formatting requirements", "Structure the response with clear headings, bullet points, and logical organization."),
                [FeedbackCategory.Safety] = (RefinementType.AddConstraint, "Add safety guardrails", "Avoid generating harmful, biased, or inappropriate content."),
                [FeedbackCategory.Creativity] = (RefinementType.RemoveInstruction, "Remove overly restrictive constraints", "Allow creative freedom while maintaining relevance to the request."),
                [FeedbackCategory.Conciseness] = (RefinementType.SimplifyStructure, "Simplify prompt structure", "Be concise and direct. Avoid unnecessary verbosity or repetition.")
            };

            // Group patterns by category to avoid duplicate refinements
            var categoryPatterns = patterns.GroupBy(p => p.Category);

            foreach (var group in categoryPatterns)
            {
                if (!categoryRefinements.TryGetValue(group.Key, out var refInfo)) continue;

                var maxSeverity = group.Max(p => p.Severity);
                var totalOccurrences = group.Sum(p => p.OccurrenceCount);
                var confidence = maxSeverity * 0.7 + (totalEntries > 0 ? (double)totalOccurrences / totalEntries : 0) * 0.3;

                refinements.Add(new PromptRefinement
                {
                    Type = refInfo.Type,
                    Description = refInfo.Description,
                    OriginalSegment = _prompts.TryGetValue(promptId, out var text) ? text : string.Empty,
                    SuggestedSegment = refInfo.Suggestion,
                    Confidence = Math.Min(confidence, 1.0),
                    TriggeredBy = group.Select(p => p.PatternId).ToList()
                });
            }

            return refinements.OrderByDescending(r => r.Confidence).ToList();
        }

        /// <summary>
        /// Auto-apply refinements above the confidence threshold to a prompt.
        /// Returns the original prompt, the refined prompt, and the list of applied refinements.
        /// </summary>
        public (string OriginalPrompt, string RefinedPrompt, List<PromptRefinement> Applied) AutoRefine(string promptId)
        {
            var original = _prompts.TryGetValue(promptId, out var text) ? text : string.Empty;
            var refinements = SuggestRefinements(promptId)
                .Where(r => r.Confidence >= RefinementConfidenceThreshold)
                .ToList();

            if (refinements.Count == 0)
                return (original, original, refinements);

            var sb = new StringBuilder(original);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("--- Auto-Refinements ---");
            foreach (var r in refinements)
            {
                sb.AppendLine($"• [{r.Type}] {r.SuggestedSegment}");
            }

            var refined = sb.ToString().TrimEnd();
            _prompts[promptId] = refined;
            return (original, refined, refinements);
        }

        /// <summary>Get a comprehensive feedback summary for a prompt.</summary>
        public FeedbackSummary GetSummary(string promptId)
        {
            var entries = _entries.Where(e => e.PromptId == promptId).ToList();
            var avgRating = entries.Count > 0 ? entries.Average(e => (double)(int)e.Rating) : 0;

            var categoryBreakdown = new Dictionary<FeedbackCategory, double>();
            foreach (var cat in Enum.GetValues<FeedbackCategory>())
            {
                var catEntries = entries.Where(e => e.Category == cat).ToList();
                if (catEntries.Count > 0)
                    categoryBreakdown[cat] = catEntries.Average(e => (double)(int)e.Rating);
            }

            var patterns = DetectPatterns(promptId);
            var refinements = SuggestRefinements(promptId);

            // Health score: rating (60%) + pattern severity inverse (25%) + refinement coverage (15%)
            var ratingNorm = entries.Count > 0 ? (avgRating - 1.0) / 4.0 : 0.5;
            var avgSeverity = patterns.Count > 0 ? patterns.Average(p => p.Severity) : 0.0;
            var refinementsApplied = refinements.Count > 0
                ? refinements.Count(r => r.Confidence >= RefinementConfidenceThreshold) / (double)refinements.Count
                : 1.0;
            var healthScore = ratingNorm * 60.0 + (1.0 - avgSeverity) * 25.0 + refinementsApplied * 15.0;

            var insights = GenerateInsights(promptId, entries, patterns, refinements, avgRating);

            return new FeedbackSummary
            {
                PromptId = promptId,
                TotalFeedback = entries.Count,
                AverageRating = Math.Round(avgRating, 2),
                CategoryBreakdown = categoryBreakdown,
                DetectedPatterns = patterns,
                SuggestedRefinements = refinements,
                HealthScore = Math.Round(Math.Clamp(healthScore, 0, 100), 1),
                ProactiveInsights = insights
            };
        }

        /// <summary>Generate a cross-prompt report.</summary>
        public FeedbackLoopReport GenerateReport(FeedbackLoopExportFormat format = FeedbackLoopExportFormat.Json)
        {
            var promptIds = _entries.Select(e => e.PromptId).Distinct().ToList();
            var summaries = promptIds.Select(GetSummary).ToList();
            var globalPatterns = DetectPatterns();
            var topRefinements = promptIds
                .SelectMany(SuggestRefinements)
                .OrderByDescending(r => r.Confidence)
                .Take(10)
                .ToList();

            return new FeedbackLoopReport
            {
                Summaries = summaries,
                GlobalPatterns = globalPatterns,
                TopRefinements = topRefinements,
                ExportFormat = format
            };
        }

        /// <summary>Export a report to the specified format string.</summary>
        public string ExportReport(FeedbackLoopReport report)
        {
            return report.ExportFormat switch
            {
                FeedbackLoopExportFormat.Json => ExportJson(report),
                FeedbackLoopExportFormat.Markdown => ExportMarkdown(report),
                FeedbackLoopExportFormat.Html => ExportHtml(report),
                FeedbackLoopExportFormat.Text => ExportText(report),
                _ => ExportJson(report)
            };
        }

        /// <summary>Get daily-aggregated feedback timeline for a prompt.</summary>
        public List<(DateTime Date, double AvgRating, int Count)> GetFeedbackTimeline(string promptId)
        {
            return _entries
                .Where(e => e.PromptId == promptId)
                .GroupBy(e => e.Timestamp.Date)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, Math.Round(g.Average(e => (double)(int)e.Rating), 2), g.Count()))
                .ToList();
        }

        /// <summary>Get a category × rating matrix (heatmap data).</summary>
        public Dictionary<FeedbackCategory, Dictionary<FeedbackRating, int>> GetCategoryHeatmap(string? promptId = null)
        {
            var relevant = promptId != null
                ? _entries.Where(e => e.PromptId == promptId)
                : _entries;

            var heatmap = new Dictionary<FeedbackCategory, Dictionary<FeedbackRating, int>>();
            foreach (var cat in Enum.GetValues<FeedbackCategory>())
            {
                heatmap[cat] = new Dictionary<FeedbackRating, int>();
                foreach (var rating in Enum.GetValues<FeedbackRating>())
                    heatmap[cat][rating] = 0;
            }

            foreach (var entry in relevant)
            {
                heatmap[entry.Category][entry.Rating]++;
            }

            return heatmap;
        }

        /// <summary>Clear all feedback data and registered prompts.</summary>
        public void Clear()
        {
            _entries.Clear();
            _prompts.Clear();
        }

        // ── Private helpers ──────────────────────────────────

        private HashSet<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>();
            var words = text.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !_stopWords.Contains(w));
            return new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
        }

        private List<string> GenerateInsights(
            string promptId,
            List<FeedbackEntry> entries,
            List<FeedbackPattern> patterns,
            List<PromptRefinement> refinements,
            double avgRating)
        {
            var insights = new List<string>();

            if (entries.Count == 0)
            {
                insights.Add("No feedback collected yet. Start gathering user feedback to enable pattern detection.");
                return insights;
            }

            // Rating insight
            if (avgRating < 2.5)
                insights.Add($"⚠️ Average rating is {avgRating:F1}/5 — prompt needs significant improvement.");
            else if (avgRating >= 4.0)
                insights.Add($"✅ Average rating is {avgRating:F1}/5 — prompt is performing well.");

            // Category weakness
            var worstCategory = entries
                .GroupBy(e => e.Category)
                .Where(g => g.Count() >= 2)
                .Select(g => (Cat: g.Key, Avg: g.Average(e => (double)(int)e.Rating)))
                .OrderBy(x => x.Avg)
                .FirstOrDefault();
            if (worstCategory.Avg > 0 && worstCategory.Avg < 3.0)
                insights.Add($"🔴 {worstCategory.Cat} has the lowest average rating ({worstCategory.Avg:F1}/5) — focus refinement here.");

            // Accelerating pattern
            var recentPatterns = patterns
                .Where(p => (DateTime.UtcNow - p.LastSeen).TotalDays < 7)
                .OrderByDescending(p => p.Severity)
                .FirstOrDefault();
            if (recentPatterns != null)
                insights.Add($"📈 Pattern '{recentPatterns.PatternId}' is recent and has severity {recentPatterns.Severity:F2} — consider addressing it soon.");

            // High-confidence refinements
            var highConf = refinements.Where(r => r.Confidence >= 0.8).ToList();
            if (highConf.Count > 0)
                insights.Add($"💡 {highConf.Count} high-confidence refinement(s) available — consider A/B testing them.");

            // Volume insight
            if (entries.Count < 10)
                insights.Add("📊 Collect more feedback (10+) for more reliable pattern detection.");

            return insights;
        }

        private static string ExportJson(FeedbackLoopReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"generatedAt\": \"{report.GeneratedAt:O}\",");
            sb.AppendLine($"  \"summaryCount\": {report.Summaries.Count},");
            sb.AppendLine($"  \"globalPatternCount\": {report.GlobalPatterns.Count},");
            sb.AppendLine($"  \"topRefinementCount\": {report.TopRefinements.Count},");
            sb.AppendLine("  \"summaries\": [");
            for (int i = 0; i < report.Summaries.Count; i++)
            {
                var s = report.Summaries[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"promptId\": \"{EscapeJson(s.PromptId)}\",");
                sb.AppendLine($"      \"totalFeedback\": {s.TotalFeedback},");
                sb.AppendLine($"      \"averageRating\": {s.AverageRating},");
                sb.AppendLine($"      \"healthScore\": {s.HealthScore},");
                sb.AppendLine($"      \"patternCount\": {s.DetectedPatterns.Count},");
                sb.AppendLine($"      \"refinementCount\": {s.SuggestedRefinements.Count}");
                sb.Append("    }");
                if (i < report.Summaries.Count - 1) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ExportMarkdown(FeedbackLoopReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Prompt Feedback Loop Report");
            sb.AppendLine($"*Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC*");
            sb.AppendLine();

            foreach (var s in report.Summaries)
            {
                sb.AppendLine($"## Prompt: `{s.PromptId}`");
                sb.AppendLine($"- **Total Feedback:** {s.TotalFeedback}");
                sb.AppendLine($"- **Average Rating:** {s.AverageRating:F1}/5");
                sb.AppendLine($"- **Health Score:** {s.HealthScore:F0}/100");
                sb.AppendLine($"- **Patterns Detected:** {s.DetectedPatterns.Count}");
                sb.AppendLine($"- **Refinements Suggested:** {s.SuggestedRefinements.Count}");
                if (s.ProactiveInsights.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Insights:**");
                    foreach (var insight in s.ProactiveInsights)
                        sb.AppendLine($"- {insight}");
                }
                sb.AppendLine();
            }

            if (report.GlobalPatterns.Count > 0)
            {
                sb.AppendLine("## Global Patterns");
                foreach (var p in report.GlobalPatterns.Take(10))
                    sb.AppendLine($"- **{p.PatternId}** — {p.Description} (severity: {p.Severity:F2})");
                sb.AppendLine();
            }

            if (report.TopRefinements.Count > 0)
            {
                sb.AppendLine("## Top Refinements");
                foreach (var r in report.TopRefinements)
                    sb.AppendLine($"- [{r.Type}] {r.Description} (confidence: {r.Confidence:F2})");
            }

            return sb.ToString();
        }

        private static string ExportHtml(FeedbackLoopReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<title>Prompt Feedback Loop Report</title>");
            sb.AppendLine("<style>body{font-family:system-ui;max-width:900px;margin:2rem auto;padding:0 1rem;color:#1a1a1a}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;margin:1rem 0}th,td{border:1px solid #ddd;padding:.5rem;text-align:left}");
            sb.AppendLine("th{background:#f5f5f5}.good{color:#16a34a}.warn{color:#d97706}.bad{color:#dc2626}");
            sb.AppendLine(".card{border:1px solid #e5e7eb;border-radius:8px;padding:1rem;margin:1rem 0}</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h1>📊 Prompt Feedback Loop Report</h1>");
            sb.AppendLine($"<p>Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC</p>");

            sb.AppendLine("<table><tr><th>Prompt</th><th>Feedback</th><th>Avg Rating</th><th>Health</th><th>Patterns</th><th>Refinements</th></tr>");
            foreach (var s in report.Summaries)
            {
                var cls = s.HealthScore >= 70 ? "good" : s.HealthScore >= 40 ? "warn" : "bad";
                sb.AppendLine($"<tr><td>{Escape(s.PromptId)}</td><td>{s.TotalFeedback}</td><td>{s.AverageRating:F1}</td>");
                sb.AppendLine($"<td class='{cls}'>{s.HealthScore:F0}</td><td>{s.DetectedPatterns.Count}</td><td>{s.SuggestedRefinements.Count}</td></tr>");
            }
            sb.AppendLine("</table>");

            if (report.GlobalPatterns.Count > 0)
            {
                sb.AppendLine("<h2>Global Patterns</h2><ul>");
                foreach (var p in report.GlobalPatterns.Take(10))
                    sb.AppendLine($"<li><strong>{Escape(p.PatternId)}</strong> — {Escape(p.Description)} (severity: {p.Severity:F2})</li>");
                sb.AppendLine("</ul>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string ExportText(FeedbackLoopReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PROMPT FEEDBACK LOOP REPORT");
            sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine(new string('=', 50));

            foreach (var s in report.Summaries)
            {
                sb.AppendLine($"\nPrompt: {s.PromptId}");
                sb.AppendLine($"  Feedback: {s.TotalFeedback} | Rating: {s.AverageRating:F1}/5 | Health: {s.HealthScore:F0}/100");
                sb.AppendLine($"  Patterns: {s.DetectedPatterns.Count} | Refinements: {s.SuggestedRefinements.Count}");
                foreach (var insight in s.ProactiveInsights)
                    sb.AppendLine($"  → {insight}");
            }

            return sb.ToString();
        }

        private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
