using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prompt
{
    /// <summary>
    /// Quality dimensions evaluated by <see cref="PromptQualityGate"/>.
    /// </summary>
    public enum QualityDimension
    {
        /// <summary>Is the prompt clear and unambiguous?</summary>
        Clarity,
        /// <summary>Does the prompt provide enough specific detail?</summary>
        Specificity,
        /// <summary>Is the desired output format described?</summary>
        OutputFormat,
        /// <summary>Does the prompt include constraints or boundaries?</summary>
        Constraints,
        /// <summary>Does the prompt define a role or persona?</summary>
        Persona,
        /// <summary>Are examples (few-shot) provided?</summary>
        Examples,
        /// <summary>Is context or background information included?</summary>
        Context,
        /// <summary>Is the prompt appropriately structured (sections, headers)?</summary>
        Structure
    }

    /// <summary>
    /// Overall gate verdict.
    /// </summary>
    public enum GateVerdict
    {
        /// <summary>Prompt meets quality threshold.</summary>
        Pass,
        /// <summary>Prompt is borderline.</summary>
        Warning,
        /// <summary>Prompt is below quality threshold.</summary>
        Fail
    }

    /// <summary>
    /// Score and feedback for a single quality dimension.
    /// </summary>
    public class QualityDimensionScore
    {
        /// <summary>Gets the dimension evaluated.</summary>
        public QualityDimension Dimension { get; internal set; }
        /// <summary>Gets the score (0.0-1.0).</summary>
        public double Score { get; internal set; }
        /// <summary>Gets the weight used in overall calculation.</summary>
        public double Weight { get; internal set; }
        /// <summary>Gets evidence found for this dimension.</summary>
        public List<string> Evidence { get; internal set; } = new();
        /// <summary>Gets improvement suggestions.</summary>
        public List<string> Suggestions { get; internal set; } = new();
        /// <summary>Gets a human-readable label.</summary>
        public string Label => Score >= 0.8 ? "Excellent" : Score >= 0.6 ? "Good" : Score >= 0.3 ? "Fair" : "Poor";
    }

    /// <summary>
    /// Complete quality gate result.
    /// </summary>
    public class QualityGateResult
    {
        /// <summary>Gets the overall quality score (0.0-1.0).</summary>
        public double OverallScore { get; internal set; }
        /// <summary>Gets the gate verdict.</summary>
        public GateVerdict Verdict { get; internal set; }
        /// <summary>Gets the overall quality label.</summary>
        public string Label => OverallScore >= 0.8 ? "Excellent" : OverallScore >= 0.6 ? "Good" : OverallScore >= 0.4 ? "Fair" : "Poor";
        /// <summary>Gets per-dimension scores.</summary>
        public List<QualityDimensionScore> Dimensions { get; internal set; } = new();
        /// <summary>Gets the top improvement suggestions.</summary>
        public List<string> TopSuggestions { get; internal set; } = new();
        /// <summary>Gets the strongest dimensions.</summary>
        public List<QualityDimension> Strengths { get; internal set; } = new();
        /// <summary>Gets the weakest dimensions.</summary>
        public List<QualityDimension> Weaknesses { get; internal set; } = new();
        /// <summary>Gets a one-line summary.</summary>
        public string Summary => $"{Verdict} ({OverallScore:P0}) -- {Label}. {Strengths.Count} strength(s), {Weaknesses.Count} weakness(es).";
        /// <summary>Gets a full report.</summary>
        public string Report
        {
            get
            {
                var lines = new List<string> { $"Prompt Quality Gate: {Verdict} ({OverallScore:P0})", $"Grade: {Label}", "" };
                foreach (var d in Dimensions.OrderByDescending(x => x.Score))
                    lines.Add($"  {d.Dimension,-14} {d.Score:P0} ({d.Label}) -- {(d.Evidence.Count > 0 ? string.Join(", ", d.Evidence.Take(2)) : "no signal")}");
                if (TopSuggestions.Count > 0) { lines.Add(""); lines.Add("Suggestions:"); foreach (var s in TopSuggestions) lines.Add($"  * {s}"); }
                return string.Join("\n", lines);
            }
        }
    }

    /// <summary>
    /// Configuration for the quality gate thresholds.
    /// </summary>
    public class QualityGateConfig
    {
        /// <summary>Overall score threshold for Pass (default 0.6).</summary>
        public double PassThreshold { get; set; } = 0.6;
        /// <summary>Overall score threshold for Warning (default 0.4).</summary>
        public double WarningThreshold { get; set; } = 0.4;
        /// <summary>Dimension weights (optional overrides).</summary>
        public Dictionary<QualityDimension, double> Weights { get; set; } = new();
        /// <summary>Maximum number of top suggestions to return.</summary>
        public int MaxSuggestions { get; set; } = 5;
        /// <summary>Whether to require all dimensions above a minimum score to pass.</summary>
        public bool StrictMode { get; set; } = false;
        /// <summary>Minimum per-dimension score in strict mode.</summary>
        public double StrictMinimum { get; set; } = 0.2;
    }

    /// <summary>
    /// Result of comparing two prompts.
    /// </summary>
    public class QualityComparison
    {
        /// <summary>Gets the quality result for prompt A.</summary>
        public QualityGateResult ResultA { get; internal set; } = new();
        /// <summary>Gets the quality result for prompt B.</summary>
        public QualityGateResult ResultB { get; internal set; } = new();
        /// <summary>Gets the winner.</summary>
        public string Winner { get; internal set; } = "";
        /// <summary>Gets the score delta (B minus A).</summary>
        public double ScoreDelta { get; internal set; }
        /// <summary>Gets dimensions where B improved over A.</summary>
        public List<string> Improvements { get; internal set; } = new();
        /// <summary>Gets dimensions where B regressed from A.</summary>
        public List<string> Regressions { get; internal set; } = new();
        /// <summary>Gets a summary.</summary>
        public string Summary => Winner == "Tie"
            ? $"Tie at {ResultA.OverallScore:P0}"
            : $"Winner: {Winner} ({(Winner == "A" ? ResultA : ResultB).OverallScore:P0} vs {(Winner == "A" ? ResultB : ResultA).OverallScore:P0}). {Improvements.Count} improvement(s), {Regressions.Count} regression(s).";
    }

    /// <summary>
    /// Pre-flight quality checker for prompts. Evaluates 8 dimensions and provides a pass/warning/fail gate with improvement suggestions.
    /// </summary>
    public class PromptQualityGate
    {
        private readonly QualityGateConfig _config;
        private static readonly string[] RolePatterns = { @"\byou\s+are\b", @"\bact\s+as\b", @"\bas\s+a\b", @"\brole\b", @"\bpersona\b", @"\bexpert\b", @"\bspecialist\b", @"\bprofessional\b", @"\bassistant\b", @"\btutor\b", @"\bmentor\b", @"\bcoach\b", @"\banalyst\b", @"\bconsultant\b", @"\bdeveloper\b", @"\bengineer\b", @"\bwriter\b", @"\beditor\b", @"\breviewer\b", @"\bcritique\b" };
        private static readonly string[] FormatPatterns = { @"\bjson\b", @"\bxml\b", @"\bcsv\b", @"\byaml\b", @"\bmarkdown\b", @"\bbullet\s*point", @"\bnumbered\s*list", @"\btable\b", @"\bformat\b", @"\bstructure\b", @"\bschema\b", @"\btemplate\b", @"\bheader\b", @"\bsection\b", @"\bparagraph\b", @"\bcode\s*block\b", @"\bresponse\s*format\b", @"\boutput\s*format\b", @"\breturn\b.*\bas\b", @"\bprovide\b.*\bin\b.*\bformat\b" };
        private static readonly string[] ConstraintPatterns = { @"\bdo\s+not\b", @"\bdon'?t\b", @"\bavoid\b", @"\bnever\b", @"\bmust\b", @"\bshould\b", @"\bonly\b", @"\bexactly\b", @"\bat\s+most\b", @"\bat\s+least\b", @"\bno\s+more\s+than\b", @"\blimit\b", @"\brestrict\b", @"\bensure\b", @"\bexclude\b", @"\bmaximum\b", @"\bminimum\b", @"\brequire\b", @"\bforbid\b" };
        private static readonly string[] ContextPatterns = { @"\bbackground\b", @"\bcontext\b", @"\bgiven\s+that\b", @"\bassume\b", @"\bscenario\b", @"\bsituation\b", @"\baudience\b", @"\btarget\b", @"\bpurpose\b", @"\bgoal\b", @"\bobjective\b", @"\bintent\b", @"\buse\s*case\b", @"\bin\s+the\s+context\s+of\b", @"\bfor\s+the\s+purpose\s+of\b" };
        private static readonly string[] ExamplePatterns = { @"\bfor\s+example\b", @"\be\.?g\.?\b", @"\bsuch\s+as\b", @"\blike\s+this\b", @"\bhere\s+is\s+an?\s+example\b", @"\bsample\b", @"\binput\s*:\s*\S", @"\boutput\s*:\s*\S", @"\bexample\s*\d*\s*:", @"```", @"\bdemonstrat" };
        private static readonly string[] VagueWords = { "something", "stuff", "things", "whatever", "anything", "somehow", "good", "nice", "interesting", "cool", "etc", "various", "some", "maybe", "probably" };
        private static readonly string[] SpecificWords = { "specifically", "precisely", "exactly", "detailed", "comprehensive", "thorough", "particular", "concrete", "quantitative", "measurable", "step-by-step", "systematic" };

        /// <summary>Initializes with default config.</summary>
        public PromptQualityGate() : this(new QualityGateConfig()) { }
        /// <summary>Initializes with custom config.</summary>
        public PromptQualityGate(QualityGateConfig config) { _config = config ?? throw new ArgumentNullException(nameof(config)); }

        /// <summary>Evaluate a prompt and return a quality gate result.</summary>
        public QualityGateResult Evaluate(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            var dims = new List<QualityDimensionScore> { ScoreClarity(prompt), ScoreSpecificity(prompt), ScoreOutputFormat(prompt), ScoreConstraints(prompt), ScorePersona(prompt), ScoreExamples(prompt), ScoreContext(prompt), ScoreStructure(prompt) };
            double tw = 0, ws = 0;
            foreach (var d in dims) { var w = _config.Weights.TryGetValue(d.Dimension, out var cw) ? cw : 1.0; d.Weight = w; ws += d.Score * w; tw += w; }
            double overall = tw > 0 ? ws / tw : 0;
            GateVerdict v;
            if (_config.StrictMode && dims.Any(d => d.Score < _config.StrictMinimum)) v = GateVerdict.Fail;
            else if (overall >= _config.PassThreshold) v = GateVerdict.Pass;
            else if (overall >= _config.WarningThreshold) v = GateVerdict.Warning;
            else v = GateVerdict.Fail;
            return new QualityGateResult { OverallScore = Math.Round(overall, 3), Verdict = v, Dimensions = dims,
                TopSuggestions = dims.OrderBy(d => d.Score).SelectMany(d => d.Suggestions).Distinct().Take(_config.MaxSuggestions).ToList(),
                Strengths = dims.Where(d => d.Score >= 0.7).OrderByDescending(d => d.Score).Select(d => d.Dimension).ToList(),
                Weaknesses = dims.Where(d => d.Score < 0.4).OrderBy(d => d.Score).Select(d => d.Dimension).ToList() };
        }

        /// <summary>Quick check: returns true if the prompt passes.</summary>
        public bool Passes(string prompt) => Evaluate(prompt).Verdict == GateVerdict.Pass;

        /// <summary>Evaluate multiple prompts, ordered by score descending.</summary>
        public List<(string Prompt, QualityGateResult Result)> EvaluateAll(IEnumerable<string> prompts)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));
            return prompts.Select(p => (p, Evaluate(p))).OrderByDescending(x => x.Item2.OverallScore).ToList();
        }

        /// <summary>Compare two prompts.</summary>
        public QualityComparison Compare(string promptA, string promptB)
        {
            if (promptA == null) throw new ArgumentNullException(nameof(promptA));
            if (promptB == null) throw new ArgumentNullException(nameof(promptB));
            var a = Evaluate(promptA); var b = Evaluate(promptB);
            var imp = new List<string>(); var reg = new List<string>();
            for (int i = 0; i < a.Dimensions.Count; i++) { double diff = b.Dimensions[i].Score - a.Dimensions[i].Score; if (diff > 0.15) imp.Add($"{a.Dimensions[i].Dimension}: +{diff:P0}"); else if (diff < -0.15) reg.Add($"{a.Dimensions[i].Dimension}: {diff:P0}"); }
            return new QualityComparison { ResultA = a, ResultB = b, Winner = a.OverallScore > b.OverallScore ? "A" : b.OverallScore > a.OverallScore ? "B" : "Tie", ScoreDelta = Math.Round(b.OverallScore - a.OverallScore, 3), Improvements = imp, Regressions = reg };
        }

        private QualityDimensionScore ScoreClarity(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.5;
            var words = prompt.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 5) { sc -= 0.3; sg.Add("Add more detail -- very short prompts often produce vague results."); }
            else if (words.Length >= 10) { sc += 0.1; ev.Add($"{words.Length} words"); }
            int sent = Regex.Matches(prompt, @"[.!?]+\s").Count + (prompt.Length > 0 && ".!?".Contains(prompt[^1]) ? 1 : 0);
            if (sent >= 2) { sc += 0.1; ev.Add($"{sent} sentences"); }
            if (prompt.Count(c => c == '?') is >= 1 and <= 3) { sc += 0.1; ev.Add("direct question(s)"); }
            var imps = new[] { "write", "create", "generate", "explain", "describe", "list", "analyze", "compare", "summarize", "translate", "implement", "design", "build", "evaluate", "provide", "give", "show", "tell", "calculate", "convert", "extract", "identify", "classify", "review" };
            var fw = words.Length > 0 ? words[0].ToLowerInvariant().TrimStart('#', '-', '*') : "";
            if (imps.Contains(fw)) { sc += 0.15; ev.Add($"imperative verb \"{fw}\""); }
            int vc = words.Count(w => VagueWords.Contains(w.ToLowerInvariant().Trim(',', '.', '!', '?')));
            if (vc >= 3) { sc -= 0.15; sg.Add("Reduce vague words (something, stuff, things, etc.)."); }
            else if (vc == 0 && words.Length >= 10) { sc += 0.05; ev.Add("no vague words"); }
            return new QualityDimensionScore { Dimension = QualityDimension.Clarity, Score = Clamp(sc), Evidence = ev, Suggestions = sg };
        }

        private QualityDimensionScore ScoreSpecificity(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.3;
            var words = prompt.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int spc = words.Count(w => SpecificWords.Contains(w.ToLowerInvariant().Trim(',', '.', '!', '?')));
            if (spc >= 2) { sc += 0.2; ev.Add($"{spc} specificity signals"); } else if (spc == 1) { sc += 0.1; ev.Add("1 specificity signal"); }
            int nc = Regex.Matches(prompt, @"\b\d+\b").Count;
            if (nc >= 2) { sc += 0.15; ev.Add($"{nc} numeric values"); } else if (nc == 1) { sc += 0.08; ev.Add("1 numeric value"); }
            int pn = 0;
            for (int i = 1; i < words.Length; i++) { var w = words[i].Trim(',', '.', '!', '?', ':', ';'); if (w.Length > 1 && char.IsUpper(w[0]) && w.Skip(1).Any(char.IsLower) && !".!?".Contains(words[i - 1][^1])) pn++; }
            if (pn >= 2) { sc += 0.15; ev.Add($"{pn} proper nouns/named entities"); }
            int tt = words.Count(w => w.Contains('_') || w.Contains('.') || (w.Length > 2 && w.Any(char.IsUpper) && w.Any(char.IsLower) && char.IsLower(w[0])));
            if (tt >= 2) { sc += 0.1; ev.Add($"{tt} technical terms"); }
            int q = Regex.Matches(prompt, "\"[^\"]+\"").Count + Regex.Matches(prompt, "'[^']+'").Count;
            if (q >= 1) { sc += 0.1; ev.Add($"{q} quoted term(s)"); }
            if (sc < 0.5) sg.Add("Add specific details -- names, numbers, technical terms, or concrete requirements.");
            return new QualityDimensionScore { Dimension = QualityDimension.Specificity, Score = Clamp(sc), Evidence = ev, Suggestions = sg };
        }

        private QualityDimensionScore ScoreOutputFormat(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.1; var lo = prompt.ToLowerInvariant(); int h = 0;
            foreach (var p in FormatPatterns) { var m = Regex.Matches(lo, p); if (m.Count > 0) { h++; ev.Add(m[0].Value.Trim()); } }
            if (h >= 3) sc = 0.9; else if (h >= 2) sc = 0.7; else if (h >= 1) sc = 0.5;
            if (h == 0) sg.Add("Specify the desired output format (e.g., JSON, bullet points, table, paragraph).");
            return new QualityDimensionScore { Dimension = QualityDimension.OutputFormat, Score = Clamp(sc), Evidence = ev.Take(3).ToList(), Suggestions = sg };
        }

        private QualityDimensionScore ScoreConstraints(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.15; var lo = prompt.ToLowerInvariant(); int h = 0;
            foreach (var p in ConstraintPatterns) { var m = Regex.Matches(lo, p); if (m.Count > 0) { h++; ev.Add(m[0].Value.Trim()); } }
            if (h >= 4) sc = 0.9; else if (h >= 3) sc = 0.75; else if (h >= 2) sc = 0.6; else if (h >= 1) sc = 0.4;
            if (h == 0) sg.Add("Add constraints or boundaries (e.g., 'do not include...', 'limit to...', 'must be...').");
            return new QualityDimensionScore { Dimension = QualityDimension.Constraints, Score = Clamp(sc), Evidence = ev.Take(4).ToList(), Suggestions = sg };
        }

        private QualityDimensionScore ScorePersona(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.1; var lo = prompt.ToLowerInvariant(); int h = 0;
            foreach (var p in RolePatterns) { var m = Regex.Matches(lo, p); if (m.Count > 0) { h++; ev.Add(m[0].Value.Trim()); } }
            if (h >= 3) sc = 0.9; else if (h >= 2) sc = 0.7; else if (h >= 1) sc = 0.5;
            if (h == 0) sg.Add("Consider adding a role/persona (e.g., 'You are an expert data scientist...').");
            return new QualityDimensionScore { Dimension = QualityDimension.Persona, Score = Clamp(sc), Evidence = ev.Take(3).ToList(), Suggestions = sg };
        }

        private QualityDimensionScore ScoreExamples(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.1; var lo = prompt.ToLowerInvariant(); int h = 0;
            foreach (var p in ExamplePatterns) { var m = Regex.Matches(lo, p); if (m.Count > 0) { h++; ev.Add(m[0].Value.Trim()); } }
            int cf = Regex.Matches(prompt, "```").Count / 2;
            if (cf >= 1) { sc += 0.2; ev.Add($"{cf} code block(s)"); }
            if (h >= 3) sc = Math.Max(sc, 0.85); else if (h >= 2) sc = Math.Max(sc, 0.65); else if (h >= 1) sc = Math.Max(sc, 0.45);
            if (h == 0 && cf == 0) sg.Add("Add examples to show the expected input/output pattern.");
            return new QualityDimensionScore { Dimension = QualityDimension.Examples, Score = Clamp(sc), Evidence = ev.Take(3).ToList(), Suggestions = sg };
        }

        private QualityDimensionScore ScoreContext(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.2; var lo = prompt.ToLowerInvariant(); int h = 0;
            foreach (var p in ContextPatterns) { var m = Regex.Matches(lo, p); if (m.Count > 0) { h++; ev.Add(m[0].Value.Trim()); } }
            if (h >= 3) sc = 0.9; else if (h >= 2) sc = 0.7; else if (h >= 1) sc = 0.5;
            int wc = prompt.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wc >= 100) { sc = Math.Min(1.0, sc + 0.15); ev.Add($"substantial length ({wc} words)"); } else if (wc >= 50) sc = Math.Min(1.0, sc + 0.08);
            if (h == 0) sg.Add("Add context or background information (audience, purpose, scenario).");
            return new QualityDimensionScore { Dimension = QualityDimension.Context, Score = Clamp(sc), Evidence = ev.Take(3).ToList(), Suggestions = sg };
        }

        private QualityDimensionScore ScoreStructure(string prompt)
        {
            var ev = new List<string>(); var sg = new List<string>(); double sc = 0.3;
            int hd = Regex.Matches(prompt, @"^#{1,6}\s+\S", RegexOptions.Multiline).Count;
            if (hd >= 2) { sc += 0.25; ev.Add($"{hd} section headers"); } else if (hd == 1) { sc += 0.1; ev.Add("1 section header"); }
            int li = Regex.Matches(prompt, @"^[\s]*[-*]\s+\S", RegexOptions.Multiline).Count + Regex.Matches(prompt, @"^[\s]*\d+[.)]\s+\S", RegexOptions.Multiline).Count;
            if (li >= 3) { sc += 0.2; ev.Add($"{li} list items"); } else if (li >= 1) { sc += 0.1; ev.Add($"{li} list item(s)"); }
            int pg = Regex.Matches(prompt, @"\n\s*\n").Count + 1;
            if (pg >= 3) { sc += 0.15; ev.Add($"{pg} paragraphs"); } else if (pg >= 2) sc += 0.08;
            int ls = Regex.Matches(prompt, @"^[A-Z][A-Za-z\s]+:\s*$", RegexOptions.Multiline).Count;
            if (ls >= 2) { sc += 0.15; ev.Add($"{ls} labeled sections"); }
            if (sc < 0.5) sg.Add("Structure your prompt with headers, bullet points, or labeled sections.");
            return new QualityDimensionScore { Dimension = QualityDimension.Structure, Score = Clamp(sc), Evidence = ev, Suggestions = sg };
        }

        private static double Clamp(double v) => Math.Max(0.0, Math.Min(1.0, v));
    }
}
