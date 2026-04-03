namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Represents intensity data for a segment of prompt text.
    /// </summary>
    public class HeatmapSegment
    {
        /// <summary>Gets the text content of this segment.</summary>
        public string Text { get; init; } = "";

        /// <summary>Gets the start character index in the original prompt.</summary>
        public int Start { get; init; }

        /// <summary>Gets the length of this segment.</summary>
        public int Length { get; init; }

        /// <summary>Gets the composite heat score (0.0 to 1.0).</summary>
        public double Heat { get; init; }

        /// <summary>Gets individual dimension scores.</summary>
        public Dictionary<string, double> Dimensions { get; init; } = new();
    }

    /// <summary>
    /// Result of a heatmap analysis.
    /// </summary>
    public class HeatmapResult
    {
        /// <summary>Gets all analyzed segments.</summary>
        public List<HeatmapSegment> Segments { get; init; } = new();

        /// <summary>Gets the overall heat distribution statistics.</summary>
        public double MeanHeat { get; init; }

        /// <summary>Gets the maximum heat value.</summary>
        public double MaxHeat { get; init; }

        /// <summary>Gets the number of hotspots (segments with heat > 0.7).</summary>
        public int HotspotCount { get; init; }

        /// <summary>Gets identified cold zones (segments with heat < 0.2).</summary>
        public int ColdZoneCount { get; init; }
    }

    /// <summary>
    /// Analyzes prompt text and generates a heatmap showing which parts are most
    /// "active" based on instruction density, variable concentration, complexity,
    /// and structural importance. Outputs analysis data and HTML visualizations.
    /// </summary>
    public class PromptHeatmap
    {
        // Instruction keywords that indicate directive content
        private static readonly HashSet<string> InstructionKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "must", "should", "always", "never", "ensure", "make sure", "do not",
            "don't", "required", "mandatory", "important", "critical", "note",
            "remember", "avoid", "include", "exclude", "use", "return", "output",
            "respond", "format", "provide", "generate", "create", "write", "list",
            "explain", "describe", "analyze", "summarize", "translate", "act as",
            "you are", "your role", "your task", "step", "first", "then", "finally",
            "if", "when", "unless", "only", "exactly", "strictly", "carefully"
        };

        // Variable/placeholder patterns
        private static readonly string[] VariablePatterns = new[]
        {
            "{{", "}}", "{", "}", "<<", ">>", "[[", "]]",
            "$", "%", "<|", "|>"
        };

        /// <summary>
        /// Analyzes a prompt and returns heatmap data with per-segment scores.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <param name="segmentSize">Approximate characters per segment (default 50).</param>
        /// <returns>A HeatmapResult with scored segments.</returns>
        public HeatmapResult Analyze(string prompt, int segmentSize = 50)
        {
            if (string.IsNullOrEmpty(prompt))
                return new HeatmapResult();

            var segments = SplitIntoSegments(prompt, segmentSize);
            var scored = new List<HeatmapSegment>();

            foreach (var (text, start) in segments)
            {
                var dims = new Dictionary<string, double>
                {
                    ["instruction"] = ScoreInstructionDensity(text),
                    ["variables"] = ScoreVariableDensity(text),
                    ["complexity"] = ScoreComplexity(text),
                    ["structure"] = ScoreStructuralImportance(text, start, prompt.Length),
                    ["emphasis"] = ScoreEmphasis(text)
                };

                var heat = dims.Values.Average();

                scored.Add(new HeatmapSegment
                {
                    Text = text,
                    Start = start,
                    Length = text.Length,
                    Heat = Math.Clamp(heat, 0.0, 1.0),
                    Dimensions = dims
                });
            }

            var heats = scored.Select(s => s.Heat).ToList();

            return new HeatmapResult
            {
                Segments = scored,
                MeanHeat = heats.Count > 0 ? heats.Average() : 0,
                MaxHeat = heats.Count > 0 ? heats.Max() : 0,
                HotspotCount = scored.Count(s => s.Heat > 0.7),
                ColdZoneCount = scored.Count(s => s.Heat < 0.2)
            };
        }

        /// <summary>
        /// Generates an HTML heatmap visualization of the prompt.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <param name="title">Optional title for the visualization.</param>
        /// <param name="segmentSize">Approximate characters per segment.</param>
        /// <returns>Complete HTML document string.</returns>
        public string ToHtml(string prompt, string? title = null, int segmentSize = 50)
        {
            var result = Analyze(prompt, segmentSize);
            title ??= "Prompt Heatmap";

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine($"<title>{Escape(title)}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
body { font-family: 'Segoe UI', system-ui, sans-serif; max-width: 900px; margin: 2rem auto; padding: 0 1rem; background: #1a1a2e; color: #e0e0e0; }
h1 { color: #fff; margin-bottom: 0.5rem; }
.stats { display: flex; gap: 1.5rem; margin: 1rem 0; flex-wrap: wrap; }
.stat { background: #16213e; padding: 0.75rem 1.25rem; border-radius: 8px; }
.stat-value { font-size: 1.5rem; font-weight: bold; }
.stat-label { font-size: 0.8rem; color: #888; text-transform: uppercase; }
.heatmap { line-height: 1.8; font-size: 0.95rem; margin: 1.5rem 0; background: #0f0f23; padding: 1.5rem; border-radius: 12px; white-space: pre-wrap; word-wrap: break-word; }
.segment { padding: 2px 0; border-radius: 3px; cursor: pointer; position: relative; transition: outline 0.15s; }
.segment:hover { outline: 2px solid rgba(255,255,255,0.5); }
.tooltip { display: none; position: absolute; bottom: 100%; left: 50%; transform: translateX(-50%); background: #222; border: 1px solid #555; border-radius: 6px; padding: 8px 12px; font-size: 0.75rem; white-space: nowrap; z-index: 10; color: #fff; pointer-events: none; }
.segment:hover .tooltip { display: block; }
.legend { display: flex; align-items: center; gap: 0.5rem; margin: 1rem 0; }
.legend-bar { height: 16px; flex: 1; border-radius: 8px; background: linear-gradient(to right, #0d1b2a, #1b263b, #415a77, #e07a5f, #e63946); }
.legend-label { font-size: 0.75rem; color: #888; }
.dims { margin-top: 1.5rem; }
.dim-row { display: flex; align-items: center; gap: 0.5rem; margin: 0.25rem 0; }
.dim-name { width: 100px; font-size: 0.8rem; text-align: right; color: #aaa; }
.dim-bar-bg { flex: 1; height: 8px; background: #1b263b; border-radius: 4px; overflow: hidden; }
.dim-bar { height: 100%; border-radius: 4px; transition: width 0.3s; }
");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h1>{Escape(title)}</h1>");

            // Stats
            sb.AppendLine("<div class=\"stats\">");
            sb.AppendLine($"<div class=\"stat\"><div class=\"stat-value\">{result.Segments.Count}</div><div class=\"stat-label\">Segments</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"stat-value\">{result.MeanHeat:F2}</div><div class=\"stat-label\">Mean Heat</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"stat-value\">{result.MaxHeat:F2}</div><div class=\"stat-label\">Max Heat</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"stat-value\">{result.HotspotCount}</div><div class=\"stat-label\">Hotspots</div></div>");
            sb.AppendLine($"<div class=\"stat\"><div class=\"stat-value\">{result.ColdZoneCount}</div><div class=\"stat-label\">Cold Zones</div></div>");
            sb.AppendLine("</div>");

            // Legend
            sb.AppendLine("<div class=\"legend\"><span class=\"legend-label\">Cold</span><div class=\"legend-bar\"></div><span class=\"legend-label\">Hot</span></div>");

            // Heatmap
            sb.AppendLine("<div class=\"heatmap\">");
            foreach (var seg in result.Segments)
            {
                var color = HeatToColor(seg.Heat);
                var dimInfo = string.Join(" | ", seg.Dimensions.Select(d => $"{d.Key}: {d.Value:F2}"));
                sb.Append($"<span class=\"segment\" style=\"background-color: {color}\">");
                sb.Append($"<span class=\"tooltip\">Heat: {seg.Heat:F2} | {dimInfo}</span>");
                sb.Append(Escape(seg.Text));
                sb.Append("</span>");
            }
            sb.AppendLine("</div>");

            // Dimension averages
            if (result.Segments.Count > 0)
            {
                var dimNames = result.Segments[0].Dimensions.Keys.ToList();
                sb.AppendLine("<h3>Dimension Averages</h3><div class=\"dims\">");
                foreach (var dim in dimNames)
                {
                    var avg = result.Segments.Average(s => s.Dimensions.GetValueOrDefault(dim, 0));
                    var barColor = dim switch
                    {
                        "instruction" => "#e63946",
                        "variables" => "#457b9d",
                        "complexity" => "#e9c46a",
                        "structure" => "#2a9d8f",
                        "emphasis" => "#f4a261",
                        _ => "#888"
                    };
                    sb.AppendLine($"<div class=\"dim-row\"><span class=\"dim-name\">{dim}</span><div class=\"dim-bar-bg\"><div class=\"dim-bar\" style=\"width:{avg * 100:F0}%;background:{barColor}\"></div></div><span class=\"legend-label\">{avg:F2}</span></div>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// Generates a compact text-based heatmap using block characters.
        /// </summary>
        public string ToText(string prompt, int segmentSize = 50)
        {
            var result = Analyze(prompt, segmentSize);
            var sb = new StringBuilder();
            sb.AppendLine("PROMPT HEATMAP");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine($"Segments: {result.Segments.Count} | Mean: {result.MeanHeat:F2} | Max: {result.MaxHeat:F2} | Hotspots: {result.HotspotCount} | Cold: {result.ColdZoneCount}");
            sb.AppendLine();

            // Block visualization
            var blocks = new[] { '░', '░', '▒', '▒', '▓', '▓', '█', '█', '█', '█' };
            sb.Append("Heat: ");
            foreach (var seg in result.Segments)
            {
                var idx = Math.Clamp((int)(seg.Heat * 9), 0, 9);
                sb.Append(blocks[idx]);
            }
            sb.AppendLine();
            sb.AppendLine();

            // Top hotspots
            var hotspots = result.Segments.OrderByDescending(s => s.Heat).Take(5).ToList();
            sb.AppendLine("Top 5 hottest segments:");
            foreach (var seg in hotspots)
            {
                var preview = seg.Text.Length > 40 ? seg.Text[..40] + "..." : seg.Text;
                preview = preview.Replace("\n", "\\n").Replace("\r", "");
                sb.AppendLine($"  [{seg.Heat:F2}] \"{preview}\"");
            }

            return sb.ToString();
        }

        #region Scoring Methods

        private double ScoreInstructionDensity(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0;

            int hits = 0;
            var lower = text.ToLowerInvariant();
            foreach (var kw in InstructionKeywords)
            {
                if (lower.Contains(kw)) hits++;
            }

            return Math.Clamp(hits / (double)Math.Max(words.Length, 1) * 2.0, 0, 1);
        }

        private double ScoreVariableDensity(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            int count = 0;
            foreach (var pattern in VariablePatterns)
            {
                var idx = 0;
                while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    idx += pattern.Length;
                }
            }

            return Math.Clamp(count / (double)Math.Max(text.Length, 1) * 15.0, 0, 1);
        }

        private double ScoreComplexity(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0;

            // Average word length as complexity proxy
            var avgWordLen = words.Average(w => w.Length);
            var wordLenScore = Math.Clamp((avgWordLen - 3) / 7.0, 0, 1);

            // Punctuation density (commas, colons, semicolons indicate complex structure)
            var punctCount = text.Count(c => c == ',' || c == ':' || c == ';' || c == '(' || c == ')');
            var punctScore = Math.Clamp(punctCount / (double)words.Length, 0, 1);

            // Nesting depth (brackets, parens)
            int maxDepth = 0, depth = 0;
            foreach (var c in text)
            {
                if (c == '(' || c == '[' || c == '{') { depth++; maxDepth = Math.Max(maxDepth, depth); }
                else if (c == ')' || c == ']' || c == '}') depth = Math.Max(0, depth - 1);
            }
            var nestScore = Math.Clamp(maxDepth / 3.0, 0, 1);

            return (wordLenScore + punctScore + nestScore) / 3.0;
        }

        private double ScoreStructuralImportance(string text, int start, int totalLength)
        {
            if (totalLength == 0) return 0;

            // Position: beginning and end of prompt are more important
            var relPos = start / (double)totalLength;
            var posScore = relPos < 0.15 ? 0.9 : relPos > 0.85 ? 0.7 : 0.3;

            // Headers, numbered lists, bullets
            var hasHeader = text.Contains('#') || text.Contains("##");
            var hasList = text.TrimStart().StartsWith("-") || text.TrimStart().StartsWith("*") ||
                         (text.Length > 1 && char.IsDigit(text.TrimStart()[0]) && text.Contains('.'));

            var structScore = (hasHeader ? 0.4 : 0) + (hasList ? 0.3 : 0);

            // Line breaks indicate section boundaries
            var lineBreaks = text.Count(c => c == '\n');
            var breakScore = Math.Clamp(lineBreaks / 3.0, 0, 0.5);

            return Math.Clamp((posScore + structScore + breakScore) / 2.0, 0, 1);
        }

        private double ScoreEmphasis(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            double score = 0;

            // ALL CAPS words
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var capsWords = words.Count(w => w.Length > 2 && w == w.ToUpperInvariant() && w.Any(char.IsLetter));
            score += Math.Clamp(capsWords / (double)Math.Max(words.Length, 1) * 3, 0, 0.4);

            // Markdown bold/italic
            if (text.Contains("**") || text.Contains("__")) score += 0.3;
            if (text.Contains('*') || text.Contains('_')) score += 0.1;

            // Exclamation marks
            var excl = text.Count(c => c == '!');
            score += Math.Clamp(excl * 0.15, 0, 0.3);

            // Quoted text
            if (text.Contains('"') || text.Contains('\'') || text.Contains('`')) score += 0.1;

            return Math.Clamp(score, 0, 1);
        }

        #endregion

        #region Helpers

        private List<(string text, int start)> SplitIntoSegments(string text, int size)
        {
            var result = new List<(string, int)>();
            // Try to split on sentence/line boundaries near the target size
            int pos = 0;
            while (pos < text.Length)
            {
                var remaining = text.Length - pos;
                if (remaining <= size * 1.3)
                {
                    result.Add((text[pos..], pos));
                    break;
                }

                // Look for a good break point near target size
                var end = Math.Min(pos + size + 20, text.Length);
                var searchStart = Math.Max(pos + size - 20, pos);
                var bestBreak = -1;

                for (var i = end - 1; i >= searchStart; i--)
                {
                    if (text[i] == '\n' || text[i] == '.' || text[i] == '!' || text[i] == '?')
                    {
                        bestBreak = i + 1;
                        break;
                    }
                }

                if (bestBreak <= pos) bestBreak = Math.Min(pos + size, text.Length);

                result.Add((text[pos..bestBreak], pos));
                pos = bestBreak;
            }

            return result;
        }

        private static string HeatToColor(double heat)
        {
            // Gradient: dark blue → steel blue → warm orange → red
            var (r, g, b) = heat switch
            {
                < 0.2 => (13, 27, 42),
                < 0.4 => (27 + (int)((heat - 0.2) / 0.2 * 38), 38 + (int)((heat - 0.2) / 0.2 * 52), 59 + (int)((heat - 0.2) / 0.2 * 60)),
                < 0.6 => (65 + (int)((heat - 0.4) / 0.2 * 100), 90 + (int)((heat - 0.4) / 0.2 * 30), 119 - (int)((heat - 0.4) / 0.2 * 20)),
                < 0.8 => (224 - (int)((heat - 0.6) / 0.2 * 10), 122 - (int)((heat - 0.6) / 0.2 * 60), 95 - (int)((heat - 0.6) / 0.2 * 40)),
                _ => (230, 57, 70)
            };

            return $"rgba({Math.Clamp(r, 0, 255)},{Math.Clamp(g, 0, 255)},{Math.Clamp(b, 0, 255)},0.6)";
        }

        private static string Escape(string text)
            => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        #endregion
    }
}
