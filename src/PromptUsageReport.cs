namespace Prompt
{
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Generates comprehensive usage reports from <see cref="PromptHistory"/> data.
    /// Supports time-bucketed breakdowns (hourly/daily/weekly), per-model comparison,
    /// cost estimation, trend detection, and export to text, HTML, or JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var history = new PromptHistory();
    /// // ... record some entries ...
    ///
    /// var report = new PromptUsageReport(history);
    /// report.SetCostPerToken("gpt-4", inputCostPer1K: 0.03m, outputCostPer1K: 0.06m);
    /// report.SetCostPerToken("gpt-3.5-turbo", inputCostPer1K: 0.001m, outputCostPer1K: 0.002m);
    ///
    /// string textReport = report.Generate(ReportGranularity.Daily);
    /// string htmlReport = report.GenerateHtml(ReportGranularity.Daily);
    /// string jsonReport = report.GenerateJson(ReportGranularity.Hourly);
    ///
    /// // Trend detection
    /// var trends = report.DetectTrends();
    /// Console.WriteLine($"Usage trend: {trends.UsageTrend}");
    /// Console.WriteLine($"Latency trend: {trends.LatencyTrend}");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptUsageReport
    {
        private readonly PromptHistory _history;
        private readonly Dictionary<string, ModelCost> _costTable = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new usage report generator for the given history.
        /// </summary>
        /// <param name="history">The prompt history to report on.</param>
        /// <exception cref="ArgumentNullException">Thrown when history is null.</exception>
        public PromptUsageReport(PromptHistory history)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
        }

        /// <summary>
        /// Sets cost-per-token pricing for a model (used in cost estimation).
        /// </summary>
        /// <param name="model">Model name (case-insensitive).</param>
        /// <param name="inputCostPer1K">Cost per 1,000 input tokens in USD.</param>
        /// <param name="outputCostPer1K">Cost per 1,000 output tokens in USD.</param>
        public void SetCostPerToken(string model, decimal inputCostPer1K, decimal outputCostPer1K)
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("Model name cannot be null or empty.", nameof(model));
            if (inputCostPer1K < 0)
                throw new ArgumentOutOfRangeException(nameof(inputCostPer1K), "Cost cannot be negative.");
            if (outputCostPer1K < 0)
                throw new ArgumentOutOfRangeException(nameof(outputCostPer1K), "Cost cannot be negative.");

            _costTable[model] = new ModelCost { InputPer1K = inputCostPer1K, OutputPer1K = outputCostPer1K };
        }

        /// <summary>
        /// Generates a plain-text usage report.
        /// </summary>
        /// <param name="granularity">Time bucket size for breakdown.</param>
        /// <param name="after">Only include entries after this time.</param>
        /// <param name="before">Only include entries before this time.</param>
        /// <returns>Formatted text report.</returns>
        public string Generate(
            ReportGranularity granularity = ReportGranularity.Daily,
            DateTimeOffset? after = null,
            DateTimeOffset? before = null)
        {
            var data = BuildReportData(granularity, after, before);
            return FormatText(data);
        }

        /// <summary>
        /// Generates an HTML usage report with inline styles.
        /// </summary>
        /// <param name="granularity">Time bucket size for breakdown.</param>
        /// <param name="after">Only include entries after this time.</param>
        /// <param name="before">Only include entries before this time.</param>
        /// <returns>Self-contained HTML report.</returns>
        public string GenerateHtml(
            ReportGranularity granularity = ReportGranularity.Daily,
            DateTimeOffset? after = null,
            DateTimeOffset? before = null)
        {
            var data = BuildReportData(granularity, after, before);
            return FormatHtml(data);
        }

        /// <summary>
        /// Generates a JSON usage report.
        /// </summary>
        /// <param name="granularity">Time bucket size for breakdown.</param>
        /// <param name="after">Only include entries after this time.</param>
        /// <param name="before">Only include entries before this time.</param>
        /// <param name="indented">Whether to indent the JSON output.</param>
        /// <returns>JSON report string.</returns>
        public string GenerateJson(
            ReportGranularity granularity = ReportGranularity.Daily,
            DateTimeOffset? after = null,
            DateTimeOffset? before = null,
            bool indented = true)
        {
            var data = BuildReportData(granularity, after, before);
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(data, options);
        }

        /// <summary>
        /// Detects trends in usage, latency, and error rates over time.
        /// </summary>
        /// <param name="after">Only include entries after this time.</param>
        /// <param name="before">Only include entries before this time.</param>
        /// <returns>Trend analysis results.</returns>
        public TrendAnalysis DetectTrends(DateTimeOffset? after = null, DateTimeOffset? before = null)
        {
            var entries = GetFilteredEntries(after, before);
            if (entries.Count < 2)
            {
                return new TrendAnalysis
                {
                    UsageTrend = TrendDirection.Insufficient,
                    LatencyTrend = TrendDirection.Insufficient,
                    ErrorRateTrend = TrendDirection.Insufficient,
                    TokenTrend = TrendDirection.Insufficient,
                    DataPoints = entries.Count
                };
            }

            // Split into halves and compare
            int mid = entries.Count / 2;
            var firstHalf = entries.Take(mid).ToList();
            var secondHalf = entries.Skip(mid).ToList();

            var firstSpan = firstHalf.Count > 1
                ? (firstHalf.Last().Timestamp - firstHalf.First().Timestamp).TotalHours
                : 1.0;
            var secondSpan = secondHalf.Count > 1
                ? (secondHalf.Last().Timestamp - secondHalf.First().Timestamp).TotalHours
                : 1.0;

            if (firstSpan <= 0) firstSpan = 1.0;
            if (secondSpan <= 0) secondSpan = 1.0;

            double firstRate = firstHalf.Count / firstSpan;
            double secondRate = secondHalf.Count / secondSpan;

            double firstAvgLatency = firstHalf.Average(e => e.Duration.TotalMilliseconds);
            double secondAvgLatency = secondHalf.Average(e => e.Duration.TotalMilliseconds);

            double firstErrorRate = firstHalf.Count > 0
                ? (double)firstHalf.Count(e => !e.Success) / firstHalf.Count
                : 0;
            double secondErrorRate = secondHalf.Count > 0
                ? (double)secondHalf.Count(e => !e.Success) / secondHalf.Count
                : 0;

            double firstAvgTokens = firstHalf.Average(e => e.EstimatedPromptTokens + e.EstimatedResponseTokens);
            double secondAvgTokens = secondHalf.Average(e => e.EstimatedPromptTokens + e.EstimatedResponseTokens);

            return new TrendAnalysis
            {
                UsageTrend = ClassifyTrend(firstRate, secondRate),
                LatencyTrend = ClassifyTrend(firstAvgLatency, secondAvgLatency),
                ErrorRateTrend = ClassifyTrend(firstErrorRate, secondErrorRate),
                TokenTrend = ClassifyTrend(firstAvgTokens, secondAvgTokens),
                UsageChangePercent = PercentChange(firstRate, secondRate),
                LatencyChangePercent = PercentChange(firstAvgLatency, secondAvgLatency),
                ErrorRateChangePercent = PercentChange(firstErrorRate, secondErrorRate),
                TokenChangePercent = PercentChange(firstAvgTokens, secondAvgTokens),
                DataPoints = entries.Count,
                FirstHalfEntries = firstHalf.Count,
                SecondHalfEntries = secondHalf.Count
            };
        }

        /// <summary>
        /// Gets a per-model cost breakdown using configured pricing.
        /// </summary>
        /// <param name="after">Only include entries after this time.</param>
        /// <param name="before">Only include entries before this time.</param>
        /// <returns>Cost breakdown per model.</returns>
        public IReadOnlyList<ModelCostBreakdown> GetCostBreakdown(
            DateTimeOffset? after = null,
            DateTimeOffset? before = null)
        {
            var entries = GetFilteredEntries(after, before);
            var grouped = entries
                .GroupBy(e => e.Model ?? "(unknown)", StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var inputTokens = g.Sum(e => (long)e.EstimatedPromptTokens);
                    var outputTokens = g.Sum(e => (long)e.EstimatedResponseTokens);
                    decimal? estimatedCost = null;

                    if (_costTable.TryGetValue(g.Key, out var cost))
                    {
                        estimatedCost = Math.Round(
                            (inputTokens / 1000m) * cost.InputPer1K +
                            (outputTokens / 1000m) * cost.OutputPer1K, 4);
                    }

                    return new ModelCostBreakdown
                    {
                        Model = g.Key,
                        CallCount = g.Count(),
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        TotalTokens = inputTokens + outputTokens,
                        EstimatedCostUsd = estimatedCost,
                        AverageDurationMs = Math.Round(g.Average(e => e.Duration.TotalMilliseconds), 1),
                        SuccessRate = Math.Round((double)g.Count(e => e.Success) / g.Count() * 100, 1)
                    };
                })
                .OrderByDescending(m => m.CallCount)
                .ToList();

            return grouped;
        }

        // ──────────── Internal ────────────

        private ReportData BuildReportData(
            ReportGranularity granularity,
            DateTimeOffset? after,
            DateTimeOffset? before)
        {
            var entries = GetFilteredEntries(after, before);
            var buckets = BucketEntries(entries, granularity);
            var costs = GetCostBreakdown(after, before);
            var trends = DetectTrends(after, before);

            long totalInputTokens = entries.Sum(e => (long)e.EstimatedPromptTokens);
            long totalOutputTokens = entries.Sum(e => (long)e.EstimatedResponseTokens);
            decimal totalCost = costs.Where(c => c.EstimatedCostUsd.HasValue)
                .Sum(c => c.EstimatedCostUsd!.Value);

            return new ReportData
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                Granularity = granularity.ToString(),
                FilterAfter = after,
                FilterBefore = before,
                TotalCalls = entries.Count,
                SuccessfulCalls = entries.Count(e => e.Success),
                FailedCalls = entries.Count(e => !e.Success),
                SuccessRate = entries.Count > 0
                    ? Math.Round((double)entries.Count(e => e.Success) / entries.Count * 100, 1)
                    : 0,
                TotalInputTokens = totalInputTokens,
                TotalOutputTokens = totalOutputTokens,
                TotalTokens = totalInputTokens + totalOutputTokens,
                TotalEstimatedCostUsd = totalCost > 0 ? totalCost : null,
                AverageDurationMs = entries.Count > 0
                    ? Math.Round(entries.Average(e => e.Duration.TotalMilliseconds), 1)
                    : 0,
                P50DurationMs = entries.Count > 0
                    ? Math.Round(Percentile(entries.Select(e => e.Duration.TotalMilliseconds).ToArray(), 50), 1)
                    : 0,
                P95DurationMs = entries.Count > 0
                    ? Math.Round(Percentile(entries.Select(e => e.Duration.TotalMilliseconds).ToArray(), 95), 1)
                    : 0,
                P99DurationMs = entries.Count > 0
                    ? Math.Round(Percentile(entries.Select(e => e.Duration.TotalMilliseconds).ToArray(), 99), 1)
                    : 0,
                TimeBuckets = buckets,
                ModelBreakdown = costs.ToList(),
                Trends = trends
            };
        }

        private List<HistoryEntry> GetFilteredEntries(DateTimeOffset? after, DateTimeOffset? before)
        {
            // Use Search with a high limit to get filtered entries
            return _history.Search(after: after, before: before, limit: int.MaxValue).ToList();
        }

        private List<TimeBucket> BucketEntries(List<HistoryEntry> entries, ReportGranularity granularity)
        {
            if (entries.Count == 0) return new List<TimeBucket>();

            Func<DateTimeOffset, string> keyFn = granularity switch
            {
                ReportGranularity.Hourly => ts => ts.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture),
                ReportGranularity.Daily => ts => ts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReportGranularity.Weekly => ts =>
                {
                    var cal = CultureInfo.InvariantCulture.Calendar;
                    int week = cal.GetWeekOfYear(ts.DateTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    return $"{ts.Year}-W{week:D2}";
                },
                ReportGranularity.Monthly => ts => ts.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                _ => ts => ts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };

            return entries
                .GroupBy(e => keyFn(e.Timestamp))
                .OrderBy(g => g.Key)
                .Select(g => new TimeBucket
                {
                    Period = g.Key,
                    Calls = g.Count(),
                    Successes = g.Count(e => e.Success),
                    Failures = g.Count(e => !e.Success),
                    InputTokens = g.Sum(e => (long)e.EstimatedPromptTokens),
                    OutputTokens = g.Sum(e => (long)e.EstimatedResponseTokens),
                    AvgDurationMs = Math.Round(g.Average(e => e.Duration.TotalMilliseconds), 1),
                    MaxDurationMs = Math.Round(g.Max(e => e.Duration.TotalMilliseconds), 1)
                })
                .ToList();
        }

        private static TrendDirection ClassifyTrend(double first, double second)
        {
            if (first == 0 && second == 0) return TrendDirection.Stable;
            double change = first > 0 ? (second - first) / first : (second > 0 ? 1 : 0);
            if (change > 0.15) return TrendDirection.Increasing;
            if (change < -0.15) return TrendDirection.Decreasing;
            return TrendDirection.Stable;
        }

        private static double PercentChange(double first, double second)
        {
            if (first == 0) return second > 0 ? 100 : 0;
            return Math.Round((second - first) / first * 100, 1);
        }

        private static double Percentile(double[] values, double percentile)
        {
            var s = values.OrderBy(x => x).ToArray();
            if (s.Length == 0) return 0;
            if (s.Length == 1) return s[0];
            double index = (percentile / 100.0) * (s.Length - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return s[lower];
            double frac = index - lower;
            return s[lower] * (1 - frac) + s[upper] * frac;
        }

        // ──────────── Text Formatter ────────────

        private static string FormatText(ReportData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════╗");
            sb.AppendLine("║              PROMPT USAGE REPORT                    ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Granularity: {data.Granularity}");
            if (data.FilterAfter.HasValue)
                sb.AppendLine($"After: {data.FilterAfter.Value:yyyy-MM-dd HH:mm:ss}");
            if (data.FilterBefore.HasValue)
                sb.AppendLine($"Before: {data.FilterBefore.Value:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("── Summary ──────────────────────────────────────────");
            sb.AppendLine($"  Total Calls:    {data.TotalCalls:N0}");
            sb.AppendLine($"  Successful:     {data.SuccessfulCalls:N0}");
            sb.AppendLine($"  Failed:         {data.FailedCalls:N0}");
            sb.AppendLine($"  Success Rate:   {data.SuccessRate}%");
            sb.AppendLine();

            sb.AppendLine("── Tokens ───────────────────────────────────────────");
            sb.AppendLine($"  Input Tokens:   {data.TotalInputTokens:N0}");
            sb.AppendLine($"  Output Tokens:  {data.TotalOutputTokens:N0}");
            sb.AppendLine($"  Total Tokens:   {data.TotalTokens:N0}");
            if (data.TotalEstimatedCostUsd.HasValue)
                sb.AppendLine($"  Est. Cost:      ${data.TotalEstimatedCostUsd.Value:F4}");
            sb.AppendLine();

            sb.AppendLine("── Latency ──────────────────────────────────────────");
            sb.AppendLine($"  Average:        {data.AverageDurationMs:N1} ms");
            sb.AppendLine($"  P50:            {data.P50DurationMs:N1} ms");
            sb.AppendLine($"  P95:            {data.P95DurationMs:N1} ms");
            sb.AppendLine($"  P99:            {data.P99DurationMs:N1} ms");
            sb.AppendLine();

            if (data.TimeBuckets.Count > 0)
            {
                sb.AppendLine("── Time Breakdown ───────────────────────────────────");
                sb.AppendLine($"  {"Period",-16} {"Calls",7} {"OK",5} {"Fail",5} {"Tokens",10} {"Avg ms",8}");
                sb.AppendLine($"  {"────────────────",-16} {"───────",7} {"─────",5} {"─────",5} {"──────────",10} {"────────",8}");
                foreach (var b in data.TimeBuckets)
                {
                    sb.AppendLine($"  {b.Period,-16} {b.Calls,7:N0} {b.Successes,5:N0} {b.Failures,5:N0} {b.InputTokens + b.OutputTokens,10:N0} {b.AvgDurationMs,8:N1}");
                }
                sb.AppendLine();
            }

            if (data.ModelBreakdown.Count > 0)
            {
                sb.AppendLine("── Model Breakdown ──────────────────────────────────");
                foreach (var m in data.ModelBreakdown)
                {
                    sb.AppendLine($"  {m.Model}");
                    sb.AppendLine($"    Calls: {m.CallCount:N0} | Tokens: {m.TotalTokens:N0} | Avg: {m.AverageDurationMs:N1}ms | Success: {m.SuccessRate}%");
                    if (m.EstimatedCostUsd.HasValue)
                        sb.AppendLine($"    Est. Cost: ${m.EstimatedCostUsd.Value:F4}");
                }
                sb.AppendLine();
            }

            if (data.Trends != null && data.Trends.UsageTrend != TrendDirection.Insufficient)
            {
                sb.AppendLine("── Trends ───────────────────────────────────────────");
                sb.AppendLine($"  Usage:      {FormatTrend(data.Trends.UsageTrend, data.Trends.UsageChangePercent)}");
                sb.AppendLine($"  Latency:    {FormatTrend(data.Trends.LatencyTrend, data.Trends.LatencyChangePercent)}");
                sb.AppendLine($"  Error Rate: {FormatTrend(data.Trends.ErrorRateTrend, data.Trends.ErrorRateChangePercent)}");
                sb.AppendLine($"  Tokens:     {FormatTrend(data.Trends.TokenTrend, data.Trends.TokenChangePercent)}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string FormatTrend(TrendDirection dir, double pct)
        {
            string arrow = dir switch
            {
                TrendDirection.Increasing => "↑",
                TrendDirection.Decreasing => "↓",
                TrendDirection.Stable => "→",
                _ => "?"
            };
            return $"{arrow} {dir} ({pct:+0.0;-0.0;0.0}%)";
        }

        // ──────────── HTML Formatter ────────────

        private static string FormatHtml(ReportData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>Prompt Usage Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#e2e8f0;padding:2rem}");
            sb.AppendLine(".container{max-width:960px;margin:0 auto}");
            sb.AppendLine("h1{font-size:1.8rem;margin-bottom:0.5rem;color:#38bdf8}");
            sb.AppendLine("h2{font-size:1.2rem;margin:1.5rem 0 0.8rem;color:#94a3b8;border-bottom:1px solid #334155;padding-bottom:0.3rem}");
            sb.AppendLine(".meta{color:#64748b;font-size:0.85rem;margin-bottom:1.5rem}");
            sb.AppendLine(".cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:1rem;margin:1rem 0}");
            sb.AppendLine(".card{background:#1e293b;border-radius:8px;padding:1rem;border:1px solid #334155}");
            sb.AppendLine(".card .label{font-size:0.75rem;color:#94a3b8;text-transform:uppercase;letter-spacing:0.05em}");
            sb.AppendLine(".card .value{font-size:1.5rem;font-weight:700;color:#f1f5f9;margin-top:0.3rem}");
            sb.AppendLine(".card .sub{font-size:0.8rem;color:#64748b;margin-top:0.2rem}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin:0.5rem 0;font-size:0.85rem}");
            sb.AppendLine("th{text-align:left;padding:0.5rem;color:#94a3b8;border-bottom:2px solid #334155;font-weight:600}");
            sb.AppendLine("td{padding:0.5rem;border-bottom:1px solid #1e293b}");
            sb.AppendLine("tr:hover td{background:#1e293b}");
            sb.AppendLine(".bar{height:6px;border-radius:3px;background:#334155;overflow:hidden;margin-top:0.3rem}");
            sb.AppendLine(".bar-fill{height:100%;border-radius:3px;background:linear-gradient(90deg,#38bdf8,#818cf8)}");
            sb.AppendLine(".trend-up{color:#f87171}.trend-down{color:#4ade80}.trend-stable{color:#94a3b8}");
            sb.AppendLine(".cost{color:#fbbf24}");
            sb.AppendLine("</style></head><body><div class=\"container\">");
            sb.AppendLine("<h1>📊 Prompt Usage Report</h1>");
            sb.AppendLine($"<p class=\"meta\">Generated {data.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC · {data.Granularity} granularity</p>");

            // Summary cards
            sb.AppendLine("<div class=\"cards\">");
            HtmlCard(sb, "Total Calls", $"{data.TotalCalls:N0}", $"{data.SuccessRate}% success");
            HtmlCard(sb, "Total Tokens", $"{data.TotalTokens:N0}", $"In: {data.TotalInputTokens:N0} · Out: {data.TotalOutputTokens:N0}");
            HtmlCard(sb, "Avg Latency", $"{data.AverageDurationMs:N1}ms", $"P95: {data.P95DurationMs:N1}ms");
            if (data.TotalEstimatedCostUsd.HasValue)
                HtmlCard(sb, "Est. Cost", $"${data.TotalEstimatedCostUsd.Value:F4}", "based on configured rates");
            sb.AppendLine("</div>");

            // Time breakdown
            if (data.TimeBuckets.Count > 0)
            {
                int maxCalls = data.TimeBuckets.Max(b => b.Calls);
                sb.AppendLine("<h2>Time Breakdown</h2>");
                sb.AppendLine("<table><tr><th>Period</th><th>Calls</th><th>Distribution</th><th>Tokens</th><th>Avg ms</th></tr>");
                foreach (var b in data.TimeBuckets)
                {
                    double pct = maxCalls > 0 ? (double)b.Calls / maxCalls * 100 : 0;
                    sb.AppendLine($"<tr><td>{HtmlEnc(b.Period)}</td><td>{b.Calls:N0}</td>");
                    sb.AppendLine($"<td><div class=\"bar\"><div class=\"bar-fill\" style=\"width:{pct:F0}%\"></div></div></td>");
                    sb.AppendLine($"<td>{b.InputTokens + b.OutputTokens:N0}</td><td>{b.AvgDurationMs:N1}</td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // Model breakdown
            if (data.ModelBreakdown.Count > 0)
            {
                sb.AppendLine("<h2>Model Breakdown</h2>");
                sb.AppendLine("<table><tr><th>Model</th><th>Calls</th><th>Tokens</th><th>Avg ms</th><th>Success</th><th>Cost</th></tr>");
                foreach (var m in data.ModelBreakdown)
                {
                    string costStr = m.EstimatedCostUsd.HasValue ? $"<span class=\"cost\">${m.EstimatedCostUsd.Value:F4}</span>" : "—";
                    sb.AppendLine($"<tr><td>{HtmlEnc(m.Model)}</td><td>{m.CallCount:N0}</td><td>{m.TotalTokens:N0}</td>");
                    sb.AppendLine($"<td>{m.AverageDurationMs:N1}</td><td>{m.SuccessRate}%</td><td>{costStr}</td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // Trends
            if (data.Trends != null && data.Trends.UsageTrend != TrendDirection.Insufficient)
            {
                sb.AppendLine("<h2>Trends</h2>");
                sb.AppendLine("<div class=\"cards\">");
                HtmlTrendCard(sb, "Usage", data.Trends.UsageTrend, data.Trends.UsageChangePercent);
                HtmlTrendCard(sb, "Latency", data.Trends.LatencyTrend, data.Trends.LatencyChangePercent);
                HtmlTrendCard(sb, "Error Rate", data.Trends.ErrorRateTrend, data.Trends.ErrorRateChangePercent);
                HtmlTrendCard(sb, "Tokens/Call", data.Trends.TokenTrend, data.Trends.TokenChangePercent);
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }

        private static void HtmlCard(StringBuilder sb, string label, string value, string sub)
        {
            sb.AppendLine($"<div class=\"card\"><div class=\"label\">{HtmlEnc(label)}</div><div class=\"value\">{HtmlEnc(value)}</div><div class=\"sub\">{HtmlEnc(sub)}</div></div>");
        }

        private static void HtmlTrendCard(StringBuilder sb, string label, TrendDirection dir, double pct)
        {
            string cls = dir switch
            {
                TrendDirection.Increasing => "trend-up",
                TrendDirection.Decreasing => "trend-down",
                _ => "trend-stable"
            };
            string arrow = dir switch
            {
                TrendDirection.Increasing => "↑",
                TrendDirection.Decreasing => "↓",
                _ => "→"
            };
            sb.AppendLine($"<div class=\"card\"><div class=\"label\">{HtmlEnc(label)}</div><div class=\"value {cls}\">{arrow} {pct:+0.0;-0.0;0.0}%</div><div class=\"sub\">{dir}</div></div>");
        }

        private static string HtmlEnc(string? s) =>
            System.Net.WebUtility.HtmlEncode(s ?? "");
    }

    /// <summary>
    /// Time bucket granularity for usage reports.
    /// </summary>
    public enum ReportGranularity
    {
        /// <summary>Group by hour.</summary>
        Hourly,
        /// <summary>Group by calendar day.</summary>
        Daily,
        /// <summary>Group by ISO week.</summary>
        Weekly,
        /// <summary>Group by calendar month.</summary>
        Monthly
    }

    /// <summary>
    /// Direction of a detected trend.
    /// </summary>
    public enum TrendDirection
    {
        /// <summary>Not enough data to determine trend.</summary>
        Insufficient,
        /// <summary>Metric is increasing.</summary>
        Increasing,
        /// <summary>Metric is decreasing.</summary>
        Decreasing,
        /// <summary>Metric is relatively stable.</summary>
        Stable
    }

    /// <summary>
    /// Results of trend analysis across usage dimensions.
    /// </summary>
    public class TrendAnalysis
    {
        public TrendDirection UsageTrend { get; set; }
        public TrendDirection LatencyTrend { get; set; }
        public TrendDirection ErrorRateTrend { get; set; }
        public TrendDirection TokenTrend { get; set; }
        public double UsageChangePercent { get; set; }
        public double LatencyChangePercent { get; set; }
        public double ErrorRateChangePercent { get; set; }
        public double TokenChangePercent { get; set; }
        public int DataPoints { get; set; }
        public int FirstHalfEntries { get; set; }
        public int SecondHalfEntries { get; set; }
    }

    /// <summary>
    /// Cost breakdown for a single model.
    /// </summary>
    public class ModelCostBreakdown
    {
        public string Model { get; set; } = "";
        public int CallCount { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long TotalTokens { get; set; }
        public decimal? EstimatedCostUsd { get; set; }
        public double AverageDurationMs { get; set; }
        public double SuccessRate { get; set; }
    }

    internal class ModelCost
    {
        public decimal InputPer1K { get; set; }
        public decimal OutputPer1K { get; set; }
    }

    internal class ReportData
    {
        public DateTimeOffset GeneratedAt { get; set; }
        public string Granularity { get; set; } = "";
        public DateTimeOffset? FilterAfter { get; set; }
        public DateTimeOffset? FilterBefore { get; set; }
        public int TotalCalls { get; set; }
        public int SuccessfulCalls { get; set; }
        public int FailedCalls { get; set; }
        public double SuccessRate { get; set; }
        public long TotalInputTokens { get; set; }
        public long TotalOutputTokens { get; set; }
        public long TotalTokens { get; set; }
        public decimal? TotalEstimatedCostUsd { get; set; }
        public double AverageDurationMs { get; set; }
        public double P50DurationMs { get; set; }
        public double P95DurationMs { get; set; }
        public double P99DurationMs { get; set; }
        public List<TimeBucket> TimeBuckets { get; set; } = new();
        public List<ModelCostBreakdown> ModelBreakdown { get; set; } = new();
        public TrendAnalysis? Trends { get; set; }
    }

    internal class TimeBucket
    {
        public string Period { get; set; } = "";
        public int Calls { get; set; }
        public int Successes { get; set; }
        public int Failures { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public double AvgDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
    }
}
