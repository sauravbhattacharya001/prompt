namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>Segment type for prompt caching analysis.</summary>
    public enum SegmentType
    {
        /// <summary>System-level instruction (e.g. "You are…").</summary>
        SystemInstruction,
        /// <summary>Static prefix shared across prompts.</summary>
        StaticPrefix,
        /// <summary>Few-shot example block.</summary>
        FewShotExamples,
        /// <summary>Large context block (documents, data).</summary>
        ContextBlock,
        /// <summary>Dynamic user query / suffix.</summary>
        DynamicSuffix
    }

    /// <summary>A segment of a prompt that may be cacheable.</summary>
    public class CacheableSegment
    {
        /// <summary>Text content of the segment.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Detected segment type.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SegmentType Type { get; set; }

        /// <summary>Estimated token count (word-count × 1.3).</summary>
        public int EstimatedTokens { get; set; }

        /// <summary>Likelihood of a cache hit (0.0–1.0).</summary>
        public double CacheHitProbability { get; set; }

        /// <summary>Actionable restructuring suggestion.</summary>
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>A group of prompts sharing a common prefix.</summary>
    public class PrefixGroup
    {
        /// <summary>The shared prefix text.</summary>
        public string CommonPrefix { get; set; } = string.Empty;

        /// <summary>Estimated tokens in the prefix.</summary>
        public int PrefixTokens { get; set; }

        /// <summary>Indices of prompts that share this prefix.</summary>
        public List<int> PromptIndices { get; set; } = new();

        /// <summary>Estimated token savings if the prefix is cached.</summary>
        public double SavingsIfCached { get; set; }
    }

    /// <summary>Result of prompt caching analysis.</summary>
    public class CachingAnalysis
    {
        /// <summary>Detected cacheable segments.</summary>
        public List<CacheableSegment> Segments { get; set; } = new();

        /// <summary>Total estimated tokens across all prompts.</summary>
        public int TotalTokens { get; set; }

        /// <summary>Tokens that could benefit from caching.</summary>
        public int CacheableTokens { get; set; }

        /// <summary>Projected cost reduction percentage.</summary>
        public double EstimatedSavingsPercent { get; set; }

        /// <summary>Overall caching efficiency score (0–100).</summary>
        public double CacheEfficiencyScore { get; set; }

        /// <summary>Ordered actionable recommendations.</summary>
        public List<string> Recommendations { get; set; } = new();

        /// <summary>Groups of prompts sharing common prefixes.</summary>
        public List<PrefixGroup> CommonPrefixes { get; set; } = new();

        /// <summary>Generate a human-readable report.</summary>
        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Prompt Caching Analysis ===");
            sb.AppendLine($"Total estimated tokens: {TotalTokens:N0}");
            sb.AppendLine($"Cacheable tokens: {CacheableTokens:N0} ({(TotalTokens > 0 ? (double)CacheableTokens / TotalTokens * 100 : 0):F1}%)");
            sb.AppendLine($"Efficiency score: {CacheEfficiencyScore:F0}/100");
            sb.AppendLine();

            if (Segments.Count > 0)
            {
                sb.AppendLine("--- Segment Breakdown ---");
                foreach (var seg in Segments)
                {
                    sb.AppendLine($"[{seg.Type}] ~{seg.EstimatedTokens:N0} tokens (cache probability: {seg.CacheHitProbability * 100:F0}%)");
                    var preview = seg.Content.Length > 60 ? seg.Content[..60] + "..." : seg.Content;
                    preview = preview.Replace("\r", "").Replace("\n", " ");
                    sb.AppendLine($"  -> \"{preview}\"");
                }
                sb.AppendLine();
            }

            if (CommonPrefixes.Count > 0)
            {
                sb.AppendLine("--- Common Prefixes ---");
                for (int i = 0; i < CommonPrefixes.Count; i++)
                {
                    var g = CommonPrefixes[i];
                    sb.AppendLine($"Group {i + 1}: {g.PromptIndices.Count} prompts share {g.PrefixTokens:N0}-token prefix");
                    sb.AppendLine($"  Savings if cached: ~{g.SavingsIfCached:N0} tokens/batch");
                }
                sb.AppendLine();
            }

            if (Recommendations.Count > 0)
            {
                sb.AppendLine("--- Recommendations ---");
                for (int i = 0; i < Recommendations.Count; i++)
                    sb.AppendLine($"{i + 1}. {Recommendations[i]}");
                sb.AppendLine();
            }

            string rating = CacheEfficiencyScore switch
            {
                >= 76 => "EXCELLENT",
                >= 51 => "GOOD",
                >= 26 => "FAIR",
                _ => "POOR"
            };
            sb.AppendLine($"Overall: {rating} candidate for prompt caching");

            return sb.ToString();
        }

        /// <summary>Export analysis as JSON.</summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });
        }
    }

    /// <summary>
    /// Analyzes prompts for LLM provider prompt-caching optimization.
    /// Detects cacheable segments, finds common prefixes, estimates savings,
    /// and recommends restructuring for providers like Anthropic and OpenAI.
    /// </summary>
    public class PromptCachingOptimizer
    {
        private static readonly Regex SystemPattern = new(
            @"^(You are |System:|SYSTEM:|<<SYS>>|<\|system\|>)",
            RegexOptions.Compiled);

        private static readonly Regex FewShotPattern = new(
            @"^(Example\s*\d*:|Q:|A:|Input:|Output:|###\s*Example|\d+\.\s+(Question|Input|Query):)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex UserTurnPattern = new(
            @"^(User:|Human:|USER:|HUMAN:|<\|user\|>|\[INST\])",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Estimate token count from text (word-count × 1.3).</summary>
        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            return (int)Math.Ceiling(words * 1.3);
        }

        /// <summary>Analyze the structure of a single prompt.</summary>
        public CachingAnalysis AnalyzeSingle(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new CachingAnalysis();

            var segments = DetectSegments(prompt);
            int total = EstimateTokens(prompt);
            int cacheable = segments
                .Where(s => s.Type != SegmentType.DynamicSuffix)
                .Sum(s => s.EstimatedTokens);

            double score = total > 0 ? Math.Min(100, (double)cacheable / total * 100) : 0;

            var recs = new List<string>();
            if (segments.Any(s => s.Type == SegmentType.SystemInstruction))
                recs.Add("System instructions detected — place at prompt start for consistent caching.");
            if (segments.Any(s => s.Type == SegmentType.FewShotExamples))
                recs.Add("Few-shot examples detected — consolidate into a contiguous static block.");
            if (segments.Any(s => s.Type == SegmentType.ContextBlock))
                recs.Add("Large context block detected — keep static context before dynamic queries.");
            if (score < 30)
                recs.Add("Low cacheability — consider extracting repeated instructions into a shared system prefix.");

            double savings = total > 0 ? (double)cacheable / total * 50 : 0; // ~50% cost for cached tokens

            return new CachingAnalysis
            {
                Segments = segments,
                TotalTokens = total,
                CacheableTokens = cacheable,
                EstimatedSavingsPercent = Math.Round(savings, 1),
                CacheEfficiencyScore = Math.Round(score, 1),
                Recommendations = recs
            };
        }

        /// <summary>Analyze a collection of prompts for caching opportunities.</summary>
        public CachingAnalysis Analyze(IEnumerable<string> prompts)
        {
            var list = prompts?.ToList() ?? new List<string>();
            if (list.Count == 0)
                return new CachingAnalysis();

            if (list.Count == 1)
                return AnalyzeSingle(list[0]);

            // Aggregate segments from all prompts
            var allSegments = new List<CacheableSegment>();
            int totalTokens = 0;
            foreach (var p in list)
            {
                var segs = DetectSegments(p);
                allSegments.AddRange(segs);
                totalTokens += EstimateTokens(p);
            }

            // Deduplicate segment types for the report
            var grouped = allSegments
                .GroupBy(s => s.Type)
                .Select(g => new CacheableSegment
                {
                    Type = g.Key,
                    Content = g.First().Content,
                    EstimatedTokens = (int)g.Average(s => s.EstimatedTokens),
                    CacheHitProbability = g.Key == SegmentType.DynamicSuffix ? 0.05 : g.Average(s => s.CacheHitProbability),
                    Recommendation = g.First().Recommendation
                })
                .OrderBy(s => s.Type)
                .ToList();

            var prefixes = FindCommonPrefixes(list);
            int cacheableTokens = allSegments
                .Where(s => s.Type != SegmentType.DynamicSuffix)
                .Sum(s => s.EstimatedTokens);

            double score = totalTokens > 0 ? Math.Min(100, (double)cacheableTokens / totalTokens * 100) : 0;

            // Boost score if common prefixes are substantial
            if (prefixes.Count > 0)
            {
                double prefixBonus = prefixes.Sum(p => p.SavingsIfCached) / Math.Max(1, totalTokens) * 30;
                score = Math.Min(100, score + prefixBonus);
            }

            var recs = new List<string>();
            if (prefixes.Count > 0)
            {
                int maxShared = prefixes.Max(p => p.PromptIndices.Count);
                recs.Add($"{prefixes.Count} common prefix group(s) found — {maxShared} prompts share the longest prefix.");
            }
            if (grouped.Any(s => s.Type == SegmentType.SystemInstruction))
                recs.Add("Consistent system instructions detected — excellent caching candidate.");
            if (grouped.Any(s => s.Type == SegmentType.FewShotExamples))
                recs.Add("Few-shot examples present — consolidate into a static block for cache reuse.");
            double savings = totalTokens > 0 ? (double)cacheableTokens / totalTokens * 50 : 0;
            recs.Add($"Estimated {savings:F0}% cost reduction with prompt caching enabled.");

            return new CachingAnalysis
            {
                Segments = grouped,
                TotalTokens = totalTokens,
                CacheableTokens = cacheableTokens,
                EstimatedSavingsPercent = Math.Round(savings, 1),
                CacheEfficiencyScore = Math.Round(score, 1),
                Recommendations = recs,
                CommonPrefixes = prefixes
            };
        }

        /// <summary>Find groups of prompts that share a common prefix.</summary>
        public List<PrefixGroup> FindCommonPrefixes(IEnumerable<string> prompts, int minPrefixTokens = 100)
        {
            var list = prompts?.ToList() ?? new List<string>();
            if (list.Count < 2) return new List<PrefixGroup>();

            // Build prefix groups by comparing all pairs
            var groups = new Dictionary<string, List<int>>();

            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    string prefix = CommonPrefix(list[i], list[j]);
                    if (EstimateTokens(prefix) < minPrefixTokens) continue;

                    // Merge into existing group if prefix overlaps substantially
                    bool merged = false;
                    foreach (var kvp in groups)
                    {
                        string shared = CommonPrefix(kvp.Key, prefix);
                        if (EstimateTokens(shared) >= minPrefixTokens)
                        {
                            if (!kvp.Value.Contains(i)) kvp.Value.Add(i);
                            if (!kvp.Value.Contains(j)) kvp.Value.Add(j);
                            merged = true;
                            break;
                        }
                    }
                    if (!merged)
                    {
                        groups[prefix] = new List<int> { i, j };
                    }
                }
            }

            return groups
                .Select(kvp =>
                {
                    int tokens = EstimateTokens(kvp.Key);
                    return new PrefixGroup
                    {
                        CommonPrefix = kvp.Key.Length > 200 ? kvp.Key[..200] + "..." : kvp.Key,
                        PrefixTokens = tokens,
                        PromptIndices = kvp.Value.OrderBy(x => x).ToList(),
                        SavingsIfCached = tokens * (kvp.Value.Count - 1)
                    };
                })
                .OrderByDescending(g => g.SavingsIfCached)
                .ToList();
        }

        /// <summary>Restructure a prompt for optimal caching (static content first, dynamic last).</summary>
        public string Restructure(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return prompt;

            var segments = DetectSegments(prompt);
            if (segments.Count <= 1) return prompt;

            var sb = new StringBuilder();
            var ordered = new[]
            {
                SegmentType.SystemInstruction,
                SegmentType.StaticPrefix,
                SegmentType.FewShotExamples,
                SegmentType.ContextBlock,
                SegmentType.DynamicSuffix
            };

            foreach (var type in ordered)
            {
                var matching = segments.Where(s => s.Type == type).ToList();
                if (matching.Count == 0) continue;

                foreach (var seg in matching)
                {
                    sb.AppendLine(seg.Content.Trim());
                    sb.AppendLine();
                }

                if (type != SegmentType.DynamicSuffix)
                    sb.AppendLine("[CACHE_BREAK]");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Compare against a baseline analysis and detect drift.</summary>
        public CachingAnalysis Monitor(IEnumerable<string> prompts, CachingAnalysis? baseline = null)
        {
            var current = Analyze(prompts);

            if (baseline != null)
            {
                double scoreDelta = current.CacheEfficiencyScore - baseline.CacheEfficiencyScore;
                double savingsDelta = current.EstimatedSavingsPercent - baseline.EstimatedSavingsPercent;

                if (scoreDelta < -10)
                    current.Recommendations.Insert(0,
                        $"WARNING: Cache efficiency dropped {Math.Abs(scoreDelta):F0} points from baseline ({baseline.CacheEfficiencyScore:F0} -> {current.CacheEfficiencyScore:F0}).");
                else if (scoreDelta > 10)
                    current.Recommendations.Insert(0,
                        $"IMPROVEMENT: Cache efficiency rose {scoreDelta:F0} points from baseline.");

                if (Math.Abs(current.TotalTokens - baseline.TotalTokens) > baseline.TotalTokens * 0.2)
                    current.Recommendations.Add(
                        $"Token volume shifted significantly: {baseline.TotalTokens:N0} -> {current.TotalTokens:N0} tokens.");
            }

            return current;
        }

        /// <summary>Detect segments within a single prompt.</summary>
        private List<CacheableSegment> DetectSegments(string prompt)
        {
            var segments = new List<CacheableSegment>();
            var lines = prompt.Split('\n');

            var systemLines = new List<string>();
            var fewShotLines = new List<string>();
            var contextLines = new List<string>();
            var dynamicLines = new List<string>();

            bool seenUserTurn = false;
            bool inFewShot = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');

                if (!seenUserTurn && SystemPattern.IsMatch(line.TrimStart()))
                {
                    systemLines.Add(line);
                    continue;
                }

                if (UserTurnPattern.IsMatch(line.TrimStart()))
                    seenUserTurn = true;

                if (FewShotPattern.IsMatch(line.TrimStart()))
                {
                    inFewShot = true;
                    fewShotLines.Add(line);
                    continue;
                }

                if (inFewShot && !string.IsNullOrWhiteSpace(line) && !UserTurnPattern.IsMatch(line.TrimStart()))
                {
                    fewShotLines.Add(line);
                    continue;
                }

                inFewShot = false;

                if (seenUserTurn)
                {
                    dynamicLines.Add(line);
                }
                else
                {
                    contextLines.Add(line);
                }
            }

            if (systemLines.Count > 0)
            {
                string text = string.Join("\n", systemLines);
                segments.Add(new CacheableSegment
                {
                    Content = text,
                    Type = SegmentType.SystemInstruction,
                    EstimatedTokens = EstimateTokens(text),
                    CacheHitProbability = 0.95,
                    Recommendation = "Keep at prompt start for maximum cache reuse."
                });
            }

            if (fewShotLines.Count > 0)
            {
                string text = string.Join("\n", fewShotLines);
                segments.Add(new CacheableSegment
                {
                    Content = text,
                    Type = SegmentType.FewShotExamples,
                    EstimatedTokens = EstimateTokens(text),
                    CacheHitProbability = 0.85,
                    Recommendation = "Consolidate examples into a contiguous block after system instructions."
                });
            }

            if (contextLines.Count > 0)
            {
                string text = string.Join("\n", contextLines).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                    var type = words > 200 ? SegmentType.ContextBlock : SegmentType.StaticPrefix;
                    segments.Add(new CacheableSegment
                    {
                        Content = text,
                        Type = type,
                        EstimatedTokens = EstimateTokens(text),
                        CacheHitProbability = type == SegmentType.StaticPrefix ? 0.90 : 0.60,
                        Recommendation = type == SegmentType.ContextBlock
                            ? "Large context — keep stable portions before dynamic queries."
                            : "Static prefix — excellent cache candidate."
                    });
                }
            }

            if (dynamicLines.Count > 0)
            {
                string text = string.Join("\n", dynamicLines).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    segments.Add(new CacheableSegment
                    {
                        Content = text,
                        Type = SegmentType.DynamicSuffix,
                        EstimatedTokens = EstimateTokens(text),
                        CacheHitProbability = 0.05,
                        Recommendation = "Dynamic content — place at end after all cacheable segments."
                    });
                }
            }

            return segments;
        }

        /// <summary>Find the common prefix of two strings.</summary>
        private static string CommonPrefix(string a, string b)
        {
            int len = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < len && a[i] == b[i]) i++;
            // Snap back to last whitespace for clean word boundaries
            if (i < len && i > 0)
            {
                int snap = a.LastIndexOf(' ', i - 1);
                if (snap > 0) i = snap;
            }
            return a[..i];
        }
    }
}
