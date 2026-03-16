namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Distribution statistics for a single category or group.
    /// </summary>
    public class CoverageDistribution
    {
        /// <summary>Gets the group name (category, tag, etc.).</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the count of items in this group.</summary>
        public int Count { get; internal set; }

        /// <summary>Gets the percentage of total items.</summary>
        public double Percentage { get; internal set; }

        /// <summary>Gets example entry names in this group.</summary>
        public List<string> Examples { get; internal set; } = new();
    }

    /// <summary>
    /// Information about a variable used across prompts.
    /// </summary>
    public class VariableUsageInfo
    {
        /// <summary>Gets the variable name.</summary>
        public string Variable { get; internal set; } = "";

        /// <summary>Gets how many prompts use this variable.</summary>
        public int UsageCount { get; internal set; }

        /// <summary>Gets the percentage of prompts that use this variable.</summary>
        public double UsagePercentage { get; internal set; }

        /// <summary>Gets the prompts that use this variable.</summary>
        public List<string> UsedBy { get; internal set; } = new();

        /// <summary>Gets whether this variable has a default value in all usages.</summary>
        public bool AlwaysHasDefault { get; internal set; }
    }

    /// <summary>
    /// A single coverage gap or improvement recommendation.
    /// </summary>
    public class CoverageRecommendation
    {
        /// <summary>Gets the recommendation category.</summary>
        public string Category { get; internal set; } = "";

        /// <summary>Gets the severity (Info, Warning, or Critical).</summary>
        public string Severity { get; internal set; } = "Info";

        /// <summary>Gets the recommendation text.</summary>
        public string Message { get; internal set; } = "";

        /// <summary>Gets affected entry names, if any.</summary>
        public List<string> AffectedEntries { get; internal set; } = new();
    }

    /// <summary>
    /// Complexity distribution bucket.
    /// </summary>
    public class ComplexityBucket
    {
        /// <summary>Gets the complexity level label.</summary>
        public string Level { get; internal set; } = "";

        /// <summary>Gets the score range description.</summary>
        public string Range { get; internal set; } = "";

        /// <summary>Gets the count of prompts in this bucket.</summary>
        public int Count { get; internal set; }

        /// <summary>Gets the entry names in this bucket.</summary>
        public List<string> Entries { get; internal set; } = new();
    }

    /// <summary>
    /// Full coverage analysis result for a prompt library.
    /// </summary>
    public class CoverageReport
    {
        /// <summary>Gets the total number of prompts analyzed.</summary>
        public int TotalPrompts { get; internal set; }

        /// <summary>Gets the total number of unique categories.</summary>
        public int UniqueCategories { get; internal set; }

        /// <summary>Gets the total number of unique tags.</summary>
        public int UniqueTags { get; internal set; }

        /// <summary>Gets the total number of unique variables.</summary>
        public int UniqueVariables { get; internal set; }

        /// <summary>Gets the category distribution.</summary>
        public List<CoverageDistribution> CategoryDistribution { get; internal set; } = new();

        /// <summary>Gets the tag distribution (top tags).</summary>
        public List<CoverageDistribution> TagDistribution { get; internal set; } = new();

        /// <summary>Gets variable usage analysis.</summary>
        public List<VariableUsageInfo> VariableUsage { get; internal set; } = new();

        /// <summary>Gets complexity distribution across buckets.</summary>
        public List<ComplexityBucket> ComplexityDistribution { get; internal set; } = new();

        /// <summary>Gets the coverage health score (0–100).</summary>
        public double HealthScore { get; internal set; }

        /// <summary>Gets the health grade letter.</summary>
        public string Grade { get; internal set; } = "";

        /// <summary>Gets recommendations for improving coverage.</summary>
        public List<CoverageRecommendation> Recommendations { get; internal set; } = new();

        /// <summary>Gets prompts with no category assigned.</summary>
        public List<string> UncategorizedPrompts { get; internal set; } = new();

        /// <summary>Gets prompts with no tags assigned.</summary>
        public List<string> UntaggedPrompts { get; internal set; } = new();

        /// <summary>Gets the average number of variables per prompt.</summary>
        public double AverageVariablesPerPrompt { get; internal set; }

        /// <summary>Gets the average template length in characters.</summary>
        public double AverageTemplateLength { get; internal set; }

        /// <summary>Gets a formatted summary string.</summary>
        public string Summary =>
            $"Coverage: {TotalPrompts} prompts, {UniqueCategories} categories, " +
            $"{UniqueTags} tags, {UniqueVariables} variables — " +
            $"Health: {HealthScore:F0}/100 ({Grade})";
    }

    /// <summary>
    /// Analyzes a <see cref="PromptLibrary"/> for coverage gaps, distribution
    /// patterns, variable usage, and provides actionable recommendations to
    /// improve prompt catalog quality and completeness.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The analyzer examines multiple dimensions:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Category Distribution</b> — are prompts spread across categories or concentrated?</description></item>
    /// <item><description><b>Tag Coverage</b> — are prompts well-tagged for discoverability?</description></item>
    /// <item><description><b>Variable Usage</b> — which variables are shared, which are orphaned?</description></item>
    /// <item><description><b>Complexity Spread</b> — is the library balanced across difficulty levels?</description></item>
    /// <item><description><b>Metadata Quality</b> — are descriptions, categories, and tags present?</description></item>
    /// </list>
    /// <para>
    /// Example usage:
    /// <code>
    /// var library = new PromptLibrary();
    /// library.Add("summarize", new PromptTemplate("Summarize: {{text}}"),
    ///     category: "writing", tags: new[] { "summary" });
    /// library.Add("translate", new PromptTemplate("Translate {{text}} to {{language}}"),
    ///     category: "writing", tags: new[] { "translation" });
    ///
    /// var report = PromptCoverageAnalyzer.Analyze(library);
    /// Console.WriteLine(report.Summary);
    /// // → "Coverage: 2 prompts, 1 categories, 2 tags, 2 variables — Health: 65/100 (C)"
    ///
    /// // Export as text, JSON, or HTML
    /// string text = PromptCoverageAnalyzer.ExportText(report);
    /// string json = PromptCoverageAnalyzer.ExportJson(report);
    /// string html = PromptCoverageAnalyzer.ExportHtml(report);
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptCoverageAnalyzer
    {
        private static readonly Regex VariablePattern =
            new(@"\{\{(\w+)\}\}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Analyzes a prompt library and produces a coverage report.
        /// </summary>
        /// <param name="library">The prompt library to analyze.</param>
        /// <returns>A <see cref="CoverageReport"/> with distribution data and recommendations.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="library"/> is null.</exception>
        public static CoverageReport Analyze(PromptLibrary library)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));

            var entries = library.Entries.ToList();
            var report = new CoverageReport
            {
                TotalPrompts = entries.Count
            };

            if (entries.Count == 0)
            {
                report.Grade = "N/A";
                report.Recommendations.Add(new CoverageRecommendation
                {
                    Category = "General",
                    Severity = "Critical",
                    Message = "Library is empty. Add prompts to begin coverage analysis."
                });
                return report;
            }

            AnalyzeCategories(entries, report);
            AnalyzeTags(entries, report);
            AnalyzeVariables(entries, report);
            AnalyzeComplexity(entries, report);
            AnalyzeMetadataQuality(entries, report);
            CalculateHealthScore(report);

            return report;
        }

        private static void AnalyzeCategories(List<PromptEntry> entries, CoverageReport report)
        {
            var categorized = entries
                .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "(uncategorized)" : e.Category)
                .OrderByDescending(g => g.Count())
                .ToList();

            report.UniqueCategories = categorized.Count(g => g.Key != "(uncategorized)");

            report.CategoryDistribution = categorized.Select(g => new CoverageDistribution
            {
                Name = g.Key,
                Count = g.Count(),
                Percentage = Math.Round(100.0 * g.Count() / entries.Count, 1),
                Examples = g.Select(e => e.Name).Take(5).ToList()
            }).ToList();

            report.UncategorizedPrompts = entries
                .Where(e => string.IsNullOrWhiteSpace(e.Category))
                .Select(e => e.Name)
                .ToList();

            // Check for category concentration
            if (categorized.Count > 1)
            {
                var topCategory = categorized.First();
                double topPct = 100.0 * topCategory.Count() / entries.Count;
                if (topPct > 60)
                {
                    report.Recommendations.Add(new CoverageRecommendation
                    {
                        Category = "Distribution",
                        Severity = "Warning",
                        Message = $"Category '{topCategory.Key}' contains {topPct:F0}% of all prompts. Consider diversifying.",
                        AffectedEntries = topCategory.Select(e => e.Name).ToList()
                    });
                }
            }

            if (report.UncategorizedPrompts.Count > 0)
            {
                report.Recommendations.Add(new CoverageRecommendation
                {
                    Category = "Metadata",
                    Severity = report.UncategorizedPrompts.Count > entries.Count / 2 ? "Warning" : "Info",
                    Message = $"{report.UncategorizedPrompts.Count} prompt(s) have no category assigned.",
                    AffectedEntries = report.UncategorizedPrompts.ToList()
                });
            }
        }

        private static void AnalyzeTags(List<PromptEntry> entries, CoverageReport report)
        {
            var allTags = entries.SelectMany(e => e.Tags).ToList();
            var tagGroups = allTags
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ToList();

            report.UniqueTags = tagGroups.Count;

            report.TagDistribution = tagGroups.Take(20).Select(g => new CoverageDistribution
            {
                Name = g.Key,
                Count = g.Count(),
                Percentage = Math.Round(100.0 * g.Count() / entries.Count, 1),
                Examples = entries.Where(e => e.Tags.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                    .Select(e => e.Name).Take(5).ToList()
            }).ToList();

            report.UntaggedPrompts = entries
                .Where(e => e.Tags.Count == 0)
                .Select(e => e.Name)
                .ToList();

            if (report.UntaggedPrompts.Count > 0)
            {
                report.Recommendations.Add(new CoverageRecommendation
                {
                    Category = "Metadata",
                    Severity = report.UntaggedPrompts.Count > entries.Count / 2 ? "Warning" : "Info",
                    Message = $"{report.UntaggedPrompts.Count} prompt(s) have no tags. Tags improve discoverability.",
                    AffectedEntries = report.UntaggedPrompts.ToList()
                });
            }

            // Check for tags used only once
            var singleUseTags = tagGroups.Where(g => g.Count() == 1).Select(g => g.Key).ToList();
            if (singleUseTags.Count > tagGroups.Count / 2 && singleUseTags.Count > 3)
            {
                report.Recommendations.Add(new CoverageRecommendation
                {
                    Category = "Tags",
                    Severity = "Info",
                    Message = $"{singleUseTags.Count} tag(s) are used by only one prompt. Consider consolidating similar tags."
                });
            }
        }

        private static void AnalyzeVariables(List<PromptEntry> entries, CoverageReport report)
        {
            var variableMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var variableDefaults = new Dictionary<string, List<bool>>(StringComparer.OrdinalIgnoreCase);

            int totalVarCount = 0;

            foreach (var entry in entries)
            {
                var templateText = entry.Template.Template;
                var matches = VariablePattern.Matches(templateText);
                var defaults = entry.Template.Defaults;

                foreach (Match match in matches)
                {
                    string varName = match.Groups[1].Value;
                    if (!variableMap.ContainsKey(varName))
                    {
                        variableMap[varName] = new List<string>();
                        variableDefaults[varName] = new List<bool>();
                    }
                    if (!variableMap[varName].Contains(entry.Name))
                    {
                        variableMap[varName].Add(entry.Name);
                        variableDefaults[varName].Add(defaults.ContainsKey(varName));
                    }
                    totalVarCount++;
                }
            }

            report.UniqueVariables = variableMap.Count;
            report.AverageVariablesPerPrompt = entries.Count > 0
                ? Math.Round((double)totalVarCount / entries.Count, 1)
                : 0;

            report.VariableUsage = variableMap
                .OrderByDescending(kv => kv.Value.Count)
                .Select(kv => new VariableUsageInfo
                {
                    Variable = kv.Key,
                    UsageCount = kv.Value.Count,
                    UsagePercentage = Math.Round(100.0 * kv.Value.Count / entries.Count, 1),
                    UsedBy = kv.Value.Take(10).ToList(),
                    AlwaysHasDefault = variableDefaults[kv.Key].All(d => d)
                })
                .ToList();

            // Recommend standardizing highly shared variables
            var sharedVars = report.VariableUsage.Where(v => v.UsageCount >= 3 && !v.AlwaysHasDefault).ToList();
            if (sharedVars.Count > 0)
            {
                report.Recommendations.Add(new CoverageRecommendation
                {
                    Category = "Variables",
                    Severity = "Info",
                    Message = $"{sharedVars.Count} variable(s) are used by 3+ prompts without defaults. " +
                              "Consider adding defaults for consistency: " +
                              string.Join(", ", sharedVars.Select(v => v.Variable))
                });
            }
        }

        private static void AnalyzeComplexity(List<PromptEntry> entries, CoverageReport report)
        {
            var buckets = new Dictionary<string, (string range, List<string> entries)>
            {
                ["Simple"] = ("0–2", new List<string>()),
                ["Moderate"] = ("2–4", new List<string>()),
                ["Complex"] = ("4–7", new List<string>()),
                ["Advanced"] = ("7–10", new List<string>())
            };

            foreach (var entry in entries)
            {
                var templateText = entry.Template.Template;
                double score = EstimateComplexity(templateText);

                string bucket = score switch
                {
                    < 2 => "Simple",
                    < 4 => "Moderate",
                    < 7 => "Complex",
                    _ => "Advanced"
                };

                buckets[bucket].entries.Add(entry.Name);
            }

            report.ComplexityDistribution = buckets.Select(kv => new ComplexityBucket
            {
                Level = kv.Key,
                Range = kv.Value.range,
                Count = kv.Value.entries.Count,
                Entries = kv.Value.entries.Take(10).ToList()
            }).ToList();

            // Check for complexity imbalance
            int totalWithContent = buckets.Values.Sum(b => b.entries.Count);
            if (totalWithContent >= 5)
            {
                var simpleCount = buckets["Simple"].entries.Count + buckets["Moderate"].entries.Count;
                if (simpleCount == totalWithContent)
                {
                    report.Recommendations.Add(new CoverageRecommendation
                    {
                        Category = "Complexity",
                        Severity = "Info",
                        Message = "All prompts are simple/moderate. Consider adding complex prompts for advanced use cases."
                    });
                }
            }
        }

        private static double EstimateComplexity(string template)
        {
            double score = 0;
            int length = template.Length;

            // Length factor
            if (length > 500) score += 1;
            if (length > 1000) score += 1;
            if (length > 2000) score += 1;

            // Variable count
            int varCount = VariablePattern.Matches(template).Count;
            score += Math.Min(varCount * 0.5, 2);

            // Instruction indicators
            var instructions = Regex.Matches(template, @"(?:^|\n)\s*[\-\*\d+\.]\s", RegexOptions.None, TimeSpan.FromMilliseconds(200));
            score += Math.Min(instructions.Count * 0.3, 2);

            // Conditional language
            if (Regex.IsMatch(template, @"\b(if|when|unless|otherwise|else|except)\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
                score += 1;

            // Multi-section
            var headers = Regex.Matches(template, @"(?:^|\n)#+\s", RegexOptions.None, TimeSpan.FromMilliseconds(200));
            score += Math.Min(headers.Count * 0.5, 1.5);

            return Math.Min(score, 10);
        }

        private static void AnalyzeMetadataQuality(List<PromptEntry> entries, CoverageReport report)
        {
            var noDescription = entries.Where(e => string.IsNullOrWhiteSpace(e.Description)).Select(e => e.Name).ToList();
            if (noDescription.Count > 0)
            {
                report.Recommendations.Add(new CoverageRecommendation
                {
                    Category = "Metadata",
                    Severity = noDescription.Count > entries.Count / 2 ? "Warning" : "Info",
                    Message = $"{noDescription.Count} prompt(s) have no description.",
                    AffectedEntries = noDescription.Take(10).ToList()
                });
            }

            report.AverageTemplateLength = entries.Count > 0
                ? Math.Round(entries.Average(e => e.Template.Template.Length), 0)
                : 0;

            // Short prompts that might benefit from more context
            var tooShort = entries.Where(e => e.Template.Template.Length < 20).Select(e => e.Name).ToList();
            if (tooShort.Count > 0)
            {
                report.Recommendations.Add(new CoverageRecommendation
                {
                    Category = "Quality",
                    Severity = "Warning",
                    Message = $"{tooShort.Count} prompt(s) are very short (<20 chars). They may lack sufficient context.",
                    AffectedEntries = tooShort
                });
            }
        }

        private static void CalculateHealthScore(CoverageReport report)
        {
            double score = 100;

            // Penalize uncategorized prompts
            if (report.TotalPrompts > 0)
            {
                double uncatPct = 100.0 * report.UncategorizedPrompts.Count / report.TotalPrompts;
                score -= uncatPct * 0.3;
            }

            // Penalize untagged prompts
            if (report.TotalPrompts > 0)
            {
                double untagPct = 100.0 * report.UntaggedPrompts.Count / report.TotalPrompts;
                score -= untagPct * 0.2;
            }

            // Reward category diversity (but not too many categories for few prompts)
            if (report.UniqueCategories > 0 && report.TotalPrompts >= 3)
            {
                double ratio = (double)report.UniqueCategories / report.TotalPrompts;
                if (ratio < 0.1) score -= 10; // too few categories
                else if (ratio > 0.8) score -= 5; // too many categories for few prompts
            }

            // Penalize critical/warning recommendations
            foreach (var rec in report.Recommendations)
            {
                if (rec.Severity == "Critical") score -= 15;
                else if (rec.Severity == "Warning") score -= 5;
            }

            score = Math.Max(0, Math.Min(100, score));
            report.HealthScore = Math.Round(score, 0);
            report.Grade = score switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };
        }

        /// <summary>
        /// Exports the coverage report as formatted plain text.
        /// </summary>
        /// <param name="report">The report to export.</param>
        /// <returns>A formatted text string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is null.</exception>
        public static string ExportText(CoverageReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("  PROMPT LIBRARY COVERAGE REPORT");
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  Total Prompts:      {report.TotalPrompts}");
            sb.AppendLine($"  Unique Categories:  {report.UniqueCategories}");
            sb.AppendLine($"  Unique Tags:        {report.UniqueTags}");
            sb.AppendLine($"  Unique Variables:   {report.UniqueVariables}");
            sb.AppendLine($"  Avg Vars/Prompt:    {report.AverageVariablesPerPrompt}");
            sb.AppendLine($"  Avg Template Len:   {report.AverageTemplateLength} chars");
            sb.AppendLine($"  Health Score:       {report.HealthScore}/100 ({report.Grade})");
            sb.AppendLine();

            if (report.CategoryDistribution.Count > 0)
            {
                sb.AppendLine("── Category Distribution ──────────────────");
                foreach (var cat in report.CategoryDistribution)
                {
                    string bar = new string('█', (int)(cat.Percentage / 5));
                    sb.AppendLine($"  {cat.Name,-20} {cat.Count,3} ({cat.Percentage,5:F1}%) {bar}");
                }
                sb.AppendLine();
            }

            if (report.TagDistribution.Count > 0)
            {
                sb.AppendLine("── Top Tags ──────────────────────────────");
                foreach (var tag in report.TagDistribution.Take(10))
                {
                    sb.AppendLine($"  {tag.Name,-20} {tag.Count,3} ({tag.Percentage,5:F1}%)");
                }
                sb.AppendLine();
            }

            if (report.VariableUsage.Count > 0)
            {
                sb.AppendLine("── Variable Usage ────────────────────────");
                foreach (var v in report.VariableUsage.Take(15))
                {
                    string def = v.AlwaysHasDefault ? " [has defaults]" : "";
                    string varDisplay = $"{{{{{v.Variable}}}}}";
                    sb.AppendLine($"  {varDisplay,-20} used by {v.UsageCount} prompt(s) ({v.UsagePercentage:F0}%){def}");
                }
                sb.AppendLine();
            }

            if (report.ComplexityDistribution.Count > 0)
            {
                sb.AppendLine("── Complexity Distribution ────────────────");
                foreach (var bucket in report.ComplexityDistribution)
                {
                    string bar = new string('█', bucket.Count);
                    sb.AppendLine($"  {bucket.Level,-12} ({bucket.Range,4}) {bucket.Count,3} {bar}");
                }
                sb.AppendLine();
            }

            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine("── Recommendations ───────────────────────");
                foreach (var rec in report.Recommendations.OrderByDescending(r => r.Severity))
                {
                    string icon = rec.Severity switch
                    {
                        "Critical" => "🔴",
                        "Warning" => "🟡",
                        _ => "🔵"
                    };
                    sb.AppendLine($"  {icon} [{rec.Severity}] {rec.Message}");
                    if (rec.AffectedEntries.Count > 0)
                    {
                        sb.AppendLine($"     Affected: {string.Join(", ", rec.AffectedEntries.Take(5))}");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>
        /// Exports the coverage report as JSON.
        /// </summary>
        /// <param name="report">The report to export.</param>
        /// <param name="indented">Whether to indent the JSON output. Default: true.</param>
        /// <returns>A JSON string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is null.</exception>
        public static string ExportJson(CoverageReport report, bool indented = true)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            return JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Exports the coverage report as a self-contained HTML page with
        /// visual charts and interactive sections.
        /// </summary>
        /// <param name="report">The report to export.</param>
        /// <returns>A complete HTML document string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is null.</exception>
        public static string ExportHtml(CoverageReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.AppendLine("<title>Prompt Library Coverage Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#e2e8f0;padding:2rem}");
            sb.AppendLine(".container{max-width:900px;margin:0 auto}");
            sb.AppendLine("h1{text-align:center;font-size:1.8rem;margin-bottom:.5rem;color:#f8fafc}");
            sb.AppendLine(".subtitle{text-align:center;color:#94a3b8;margin-bottom:2rem}");
            sb.AppendLine(".stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:1rem;margin-bottom:2rem}");
            sb.AppendLine(".stat{background:#1e293b;border-radius:12px;padding:1.2rem;text-align:center}");
            sb.AppendLine(".stat .value{font-size:2rem;font-weight:700;color:#38bdf8}");
            sb.AppendLine(".stat .label{font-size:.85rem;color:#94a3b8;margin-top:.3rem}");
            sb.AppendLine(".grade{font-size:3rem;font-weight:800}");
            sb.AppendLine(".grade.A{color:#22c55e}.grade.B{color:#84cc16}.grade.C{color:#eab308}.grade.D{color:#f97316}.grade.F{color:#ef4444}");
            sb.AppendLine(".section{background:#1e293b;border-radius:12px;padding:1.5rem;margin-bottom:1.5rem}");
            sb.AppendLine(".section h2{font-size:1.1rem;color:#f8fafc;margin-bottom:1rem;border-bottom:1px solid #334155;padding-bottom:.5rem}");
            sb.AppendLine(".bar-row{display:flex;align-items:center;margin-bottom:.5rem}");
            sb.AppendLine(".bar-label{width:140px;font-size:.85rem;color:#cbd5e1;flex-shrink:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}");
            sb.AppendLine(".bar-track{flex:1;height:22px;background:#334155;border-radius:4px;overflow:hidden;margin:0 .5rem}");
            sb.AppendLine(".bar-fill{height:100%;border-radius:4px;transition:width .3s}");
            sb.AppendLine(".bar-value{width:60px;font-size:.8rem;color:#94a3b8;text-align:right}");
            sb.AppendLine(".rec{padding:.8rem;margin-bottom:.5rem;border-radius:8px;font-size:.9rem}");
            sb.AppendLine(".rec.Critical{background:#7f1d1d;border-left:4px solid #ef4444}");
            sb.AppendLine(".rec.Warning{background:#713f12;border-left:4px solid #eab308}");
            sb.AppendLine(".rec.Info{background:#1e3a5f;border-left:4px solid #38bdf8}");
            sb.AppendLine(".rec .sev{font-weight:700;margin-right:.5rem}");
            sb.AppendLine(".affected{font-size:.8rem;color:#94a3b8;margin-top:.3rem}");
            sb.AppendLine(".pill{display:inline-block;padding:2px 8px;background:#334155;border-radius:12px;font-size:.75rem;margin:2px}");
            sb.AppendLine("</style></head><body><div class=\"container\">");
            sb.AppendLine("<h1>📊 Prompt Library Coverage Report</h1>");
            sb.AppendLine($"<p class=\"subtitle\">Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC</p>");

            // Stats grid
            sb.AppendLine("<div class=\"stats\">");
            sb.AppendLine($"<div class=\"stat\"><div class=\"grade {report.Grade}\">{report.Grade}</div><div class=\"label\">Health {report.HealthScore}/100</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"value\">{report.TotalPrompts}</div><div class=\"label\">Prompts</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"value\">{report.UniqueCategories}</div><div class=\"label\">Categories</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"value\">{report.UniqueTags}</div><div class=\"label\">Tags</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"value\">{report.UniqueVariables}</div><div class=\"label\">Variables</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"value\">{report.AverageVariablesPerPrompt}</div><div class=\"label\">Avg Vars/Prompt</div></div>");
            sb.AppendLine("</div>");

            // Category distribution
            if (report.CategoryDistribution.Count > 0)
            {
                string[] colors = { "#38bdf8", "#818cf8", "#a78bfa", "#c084fc", "#e879f9", "#f472b6", "#fb923c", "#facc15", "#4ade80", "#2dd4bf" };
                sb.AppendLine("<div class=\"section\"><h2>Category Distribution</h2>");
                int ci = 0;
                foreach (var cat in report.CategoryDistribution)
                {
                    string color = colors[ci % colors.Length];
                    sb.AppendLine($"<div class=\"bar-row\"><span class=\"bar-label\">{HtmlEncode(cat.Name)}</span>");
                    sb.AppendLine($"<div class=\"bar-track\"><div class=\"bar-fill\" style=\"width:{cat.Percentage}%;background:{color}\"></div></div>");
                    sb.AppendLine($"<span class=\"bar-value\">{cat.Count} ({cat.Percentage}%)</span></div>");
                    ci++;
                }
                sb.AppendLine("</div>");
            }

            // Complexity distribution
            if (report.ComplexityDistribution.Count > 0)
            {
                string[] cColors = { "#22c55e", "#eab308", "#f97316", "#ef4444" };
                int maxCount = report.ComplexityDistribution.Max(b => b.Count);
                sb.AppendLine("<div class=\"section\"><h2>Complexity Distribution</h2>");
                int bi = 0;
                foreach (var bucket in report.ComplexityDistribution)
                {
                    double pct = maxCount > 0 ? 100.0 * bucket.Count / maxCount : 0;
                    string color = cColors[bi % cColors.Length];
                    sb.AppendLine($"<div class=\"bar-row\"><span class=\"bar-label\">{bucket.Level} ({bucket.Range})</span>");
                    sb.AppendLine($"<div class=\"bar-track\"><div class=\"bar-fill\" style=\"width:{pct:F0}%;background:{color}\"></div></div>");
                    sb.AppendLine($"<span class=\"bar-value\">{bucket.Count}</span></div>");
                    bi++;
                }
                sb.AppendLine("</div>");
            }

            // Variable usage
            if (report.VariableUsage.Count > 0)
            {
                int maxUsage = report.VariableUsage.Max(v => v.UsageCount);
                sb.AppendLine("<div class=\"section\"><h2>Top Variables</h2>");
                foreach (var v in report.VariableUsage.Take(10))
                {
                    double pct = maxUsage > 0 ? 100.0 * v.UsageCount / maxUsage : 0;
                    string def = v.AlwaysHasDefault ? " ✓" : "";
                    sb.AppendLine($"<div class=\"bar-row\"><span class=\"bar-label\">{{{{{HtmlEncode(v.Variable)}}}}}{def}</span>");
                    sb.AppendLine($"<div class=\"bar-track\"><div class=\"bar-fill\" style=\"width:{pct:F0}%;background:#818cf8\"></div></div>");
                    sb.AppendLine($"<span class=\"bar-value\">{v.UsageCount} ({v.UsagePercentage:F0}%)</span></div>");
                }
                sb.AppendLine("</div>");
            }

            // Recommendations
            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine("<div class=\"section\"><h2>Recommendations</h2>");
                foreach (var rec in report.Recommendations.OrderByDescending(r => r.Severity))
                {
                    sb.AppendLine($"<div class=\"rec {rec.Severity}\"><span class=\"sev\">[{rec.Severity}]</span> {HtmlEncode(rec.Message)}");
                    if (rec.AffectedEntries.Count > 0)
                    {
                        sb.Append("<div class=\"affected\">");
                        foreach (var e in rec.AffectedEntries.Take(5))
                            sb.Append($"<span class=\"pill\">{HtmlEncode(e)}</span>");
                        if (rec.AffectedEntries.Count > 5)
                            sb.Append($"<span class=\"pill\">+{rec.AffectedEntries.Count - 5} more</span>");
                        sb.AppendLine("</div>");
                    }
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }

        private static string HtmlEncode(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
