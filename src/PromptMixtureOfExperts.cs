using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Prompt;

/// <summary>
/// Mixture-of-Experts routing system that classifies inputs and routes them
/// to the best specialized prompt expert, with confidence scoring,
/// fallback routing, and performance-based weight adaptation.
/// </summary>
public sealed class PromptMixtureOfExperts
{
    private readonly List<Expert> _experts = new();
    private Expert? _fallback;
    private double _confidenceThreshold = 0.3;
    private int _topK = 1;
    private bool _adaptiveWeights = true;
    private readonly List<RoutingRecord> _history = new();

    /// <summary>Registers a new expert with a domain, prompt template, and keyword matchers.</summary>
    public PromptMixtureOfExperts AddExpert(string name, string domain, string promptTemplate, IEnumerable<string> keywords, double baseWeight = 1.0)
    {
        _experts.Add(new Expert(name, domain, promptTemplate, keywords.ToList(), baseWeight));
        return this;
    }

    /// <summary>Sets a fallback expert used when no expert exceeds the confidence threshold.</summary>
    public PromptMixtureOfExperts SetFallback(string name, string promptTemplate)
    {
        _fallback = new Expert(name, "fallback", promptTemplate, new List<string>(), 1.0);
        return this;
    }

    /// <summary>Sets the minimum confidence score to select an expert (0-1). Default 0.3.</summary>
    public PromptMixtureOfExperts WithConfidenceThreshold(double threshold)
    {
        _confidenceThreshold = Math.Clamp(threshold, 0.0, 1.0);
        return this;
    }

    /// <summary>Sets how many top experts to blend when using ensemble mode. Default 1.</summary>
    public PromptMixtureOfExperts WithTopK(int k)
    {
        _topK = Math.Max(1, k);
        return this;
    }

    /// <summary>Enables or disables adaptive weight adjustment based on feedback. Default true.</summary>
    public PromptMixtureOfExperts WithAdaptiveWeights(bool enabled)
    {
        _adaptiveWeights = enabled;
        return this;
    }

    /// <summary>Routes an input to the best expert(s) and returns the routing result.</summary>
    public RoutingResult Route(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return RoutingResult.Empty();
        if (_experts.Count == 0)
            return _fallback != null
                ? new RoutingResult(_fallback.Name, _fallback.Render(input), 0.0, true, new List<ExpertScore>())
                : RoutingResult.Empty();

        var scores = ScoreExperts(input);
        var sorted = scores.OrderByDescending(s => s.Score).ToList();
        var top = sorted.FirstOrDefault();

        if (top == null || top.Score < _confidenceThreshold)
        {
            var record = new RoutingRecord(input, _fallback?.Name ?? "(none)", 0.0, true);
            _history.Add(record);
            return _fallback != null
                ? new RoutingResult(_fallback.Name, _fallback.Render(input), 0.0, true, sorted)
                : RoutingResult.Empty();
        }

        string rendered;
        string expertName;

        if (_topK > 1 && sorted.Count > 1)
        {
            var topExperts = sorted.Take(_topK).ToList();
            var totalWeight = topExperts.Sum(e => e.Score);
            var blended = new StringBuilder();
            blended.AppendLine("## Ensemble Response (Top-K Experts)");
            blended.AppendLine();
            foreach (var es in topExperts)
            {
                var weight = totalWeight > 0 ? es.Score / totalWeight : 1.0 / topExperts.Count;
                blended.AppendLine($"### [{es.ExpertName}] (weight: {weight:P0})");
                blended.AppendLine(es.Expert.Render(input));
                blended.AppendLine();
            }
            rendered = blended.ToString().TrimEnd();
            expertName = string.Join("+", topExperts.Select(e => e.ExpertName));
        }
        else
        {
            expertName = top.ExpertName;
            rendered = top.Expert.Render(input);
        }

        _history.Add(new RoutingRecord(input, expertName, top.Score, false));
        return new RoutingResult(expertName, rendered, top.Score, false, sorted);
    }

    /// <summary>Provides feedback for the last routing to adapt expert weights.</summary>
    public void Feedback(string expertName, bool positive)
    {
        if (!_adaptiveWeights) return;
        var expert = _experts.FirstOrDefault(e => e.Name.Equals(expertName, StringComparison.OrdinalIgnoreCase));
        if (expert == null) return;
        double delta = positive ? 0.1 : -0.05;
        expert.AdjustWeight(delta);
    }

    /// <summary>Returns routing history.</summary>
    public IReadOnlyList<RoutingRecord> GetHistory() => _history.AsReadOnly();

    /// <summary>Returns a report of all experts with their current weights and hit counts.</summary>
    public string GetReport(ReportFormat format = ReportFormat.Text)
    {
        var expertStats = _experts.Select(e =>
        {
            int hits = _history.Count(h => h.ExpertName.Contains(e.Name, StringComparison.OrdinalIgnoreCase) && !h.UsedFallback);
            return new { e.Name, e.Domain, Weight = e.CurrentWeight, Hits = hits, Keywords = e.Keywords.Count };
        }).OrderByDescending(x => x.Hits).ToList();

        int fallbackHits = _history.Count(h => h.UsedFallback);

        return format switch
        {
            ReportFormat.Markdown => BuildMarkdownReport(expertStats, fallbackHits),
            ReportFormat.Json => BuildJsonReport(expertStats, fallbackHits),
            _ => BuildTextReport(expertStats, fallbackHits)
        };
    }

    // Pre-tokenization regex compiled once (was: re-compiled per Route call).
    private static readonly Regex WordSplitRegex = new(@"\W+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private List<ExpertScore> ScoreExperts(string input)
    {
        // Lowercase the input once and build a word set once per Route call,
        // rather than per-expert as the previous Linq-heavy implementation did.
        var lower = input.ToLowerInvariant();
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var w in WordSplitRegex.Split(lower))
        {
            if (w.Length > 0) words.Add(w);
        }

        var results = new List<ExpertScore>(_experts.Count);
        foreach (var e in _experts)
        {
            var kwCount = e.LowerKeywords.Count;
            double keywordScore = 0.0;
            double wordOverlap = 0.0;

            if (kwCount > 0)
            {
                int substringHits = 0;
                int wordHits = 0;
                // Single loop: counts both substring containment AND exact word overlap,
                // using pre-lowercased keywords. Previous version allocated 2N lowercased
                // strings and did 2 Linq passes per expert per Route.
                for (int i = 0; i < kwCount; i++)
                {
                    var lk = e.LowerKeywords[i];
                    if (lower.Contains(lk, StringComparison.Ordinal)) substringHits++;
                    if (words.Contains(lk)) wordHits++;
                }
                keywordScore = (double)substringHits / kwCount;
                wordOverlap = (double)wordHits / kwCount;
            }

            double domainBonus = lower.Contains(e.LowerDomain, StringComparison.Ordinal) ? 0.2 : 0.0;

            double raw = (keywordScore * 0.5 + wordOverlap * 0.3 + domainBonus) * e.CurrentWeight;
            double score = Math.Min(raw, 1.0);

            results.Add(new ExpertScore(e.Name, score, e));
        }
        return results;
    }

    private static string BuildTextReport(IEnumerable<dynamic> stats, int fallbackHits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Mixture-of-Experts Report ===");
        sb.AppendLine();
        foreach (var s in stats)
            sb.AppendLine($"  {s.Name,-20} domain={s.Domain,-15} weight={s.Weight:F2}  hits={s.Hits}  keywords={s.Keywords}");
        sb.AppendLine();
        sb.AppendLine($"  Fallback hits: {fallbackHits}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildMarkdownReport(IEnumerable<dynamic> stats, int fallbackHits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Mixture-of-Experts Report");
        sb.AppendLine();
        sb.AppendLine("| Expert | Domain | Weight | Hits | Keywords |");
        sb.AppendLine("|--------|--------|--------|------|----------|");
        foreach (var s in stats)
            sb.AppendLine($"| {s.Name} | {s.Domain} | {s.Weight:F2} | {s.Hits} | {s.Keywords} |");
        sb.AppendLine();
        sb.AppendLine($"**Fallback hits:** {fallbackHits}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildJsonReport(IEnumerable<dynamic> stats, int fallbackHits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"experts\": [");
        var list = stats.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            string comma = i < list.Count - 1 ? "," : "";
            sb.AppendLine($"    {{\"name\":\"{s.Name}\",\"domain\":\"{s.Domain}\",\"weight\":{s.Weight:F2},\"hits\":{s.Hits},\"keywords\":{s.Keywords}}}{comma}");
        }
        sb.AppendLine("  ],");
        sb.AppendLine($"  \"fallbackHits\": {fallbackHits},");
        sb.AppendLine($"  \"totalRoutings\": {list.Sum((Func<dynamic, int>)(x => (int)x.Hits)) + fallbackHits}");
        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Individual expert definition.</summary>
    public sealed class Expert
    {
        public string Name { get; }
        public string Domain { get; }
        public string PromptTemplate { get; }
        public IReadOnlyList<string> Keywords { get; }
        public double CurrentWeight { get; private set; }

        // Pre-lowercased copies of keywords and domain — computed once at construction
        // so the hot ScoreExperts loop doesn't allocate a new lowercased string per
        // keyword per Route call.
        internal IReadOnlyList<string> LowerKeywords { get; }
        internal string LowerDomain { get; }

        internal Expert(string name, string domain, string promptTemplate, List<string> keywords, double baseWeight)
        {
            Name = name;
            Domain = domain;
            PromptTemplate = promptTemplate;
            Keywords = keywords.AsReadOnly();
            var lower = new List<string>(keywords.Count);
            foreach (var k in keywords) lower.Add(k.ToLowerInvariant());
            LowerKeywords = lower.AsReadOnly();
            LowerDomain = domain.ToLowerInvariant();
            CurrentWeight = baseWeight;
        }

        internal string Render(string input) =>
            PromptTemplate.Contains("{input}", StringComparison.Ordinal)
                ? PromptTemplate.Replace("{input}", input)
                : $"{PromptTemplate}\n\nInput: {input}";

        internal void AdjustWeight(double delta) =>
            CurrentWeight = Math.Clamp(CurrentWeight + delta, 0.1, 3.0);
    }

    /// <summary>Score for an individual expert on a given input.</summary>
    public sealed class ExpertScore
    {
        public string ExpertName { get; }
        public double Score { get; }
        internal Expert Expert { get; }

        internal ExpertScore(string name, double score, Expert expert)
        {
            ExpertName = name;
            Score = score;
            Expert = expert;
        }
    }

    /// <summary>Result of routing an input through the MoE system.</summary>
    public sealed class RoutingResult
    {
        public string ExpertName { get; }
        public string RenderedPrompt { get; }
        public double Confidence { get; }
        public bool UsedFallback { get; }
        public IReadOnlyList<ExpertScore> AllScores { get; }

        internal RoutingResult(string expertName, string rendered, double confidence, bool usedFallback, List<ExpertScore> scores)
        {
            ExpertName = expertName;
            RenderedPrompt = rendered;
            Confidence = confidence;
            UsedFallback = usedFallback;
            AllScores = scores.AsReadOnly();
        }

        internal static RoutingResult Empty() =>
            new("(none)", string.Empty, 0.0, true, new List<ExpertScore>());
    }

    /// <summary>Historical routing record.</summary>
    public sealed class RoutingRecord
    {
        public string Input { get; }
        public string ExpertName { get; }
        public double Confidence { get; }
        public bool UsedFallback { get; }
        public DateTime Timestamp { get; }

        internal RoutingRecord(string input, string expertName, double confidence, bool usedFallback)
        {
            Input = input;
            ExpertName = expertName;
            Confidence = confidence;
            UsedFallback = usedFallback;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>Report output format.</summary>
    public enum ReportFormat { Text, Markdown, Json }
}
