namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Strategy the autopilot uses to refine prompts each generation.
    /// </summary>
    /// <summary>
    /// Strategy the autopilot uses to refine prompts each generation.
    /// </summary>
    public enum AutopilotStrategy
    {
        /// <summary>Fix the most severe issue first.</summary>
        WorstFirst,
        /// <summary>Fix all issues in a single pass.</summary>
        Shotgun,
        /// <summary>Focus on one dimension per generation (rotating).</summary>
        Rotating,
        /// <summary>Apply only high-confidence, low-risk fixes.</summary>
        Conservative,
        /// <summary>Aggressive rewriting targeting maximum score gain.</summary>
        Aggressive
    }

    /// <summary>
    /// A single diagnosis finding from autopilot analysis.
    /// </summary>
    public class AutopilotDiagnosis
    {
        /// <summary>Gets the dimension this diagnosis targets.</summary>
        public string Dimension { get; internal set; } = "";

        /// <summary>Gets the severity (0.0=trivial, 1.0=critical).</summary>
        public double Severity { get; internal set; }

        /// <summary>Gets the human-readable description.</summary>
        public string Description { get; internal set; } = "";

        /// <summary>Gets the suggested fix.</summary>
        public string Fix { get; internal set; } = "";

        /// <summary>Gets whether the fix was applied.</summary>
        public bool Applied { get; internal set; }
    }

    /// <summary>
    /// Scores across multiple quality dimensions.
    /// </summary>
    public class AutopilotScorecard
    {
        /// <summary>Clarity score (0–100).</summary>
        public int Clarity { get; internal set; }

        /// <summary>Specificity score (0–100).</summary>
        public int Specificity { get; internal set; }

        /// <summary>Structure score (0–100).</summary>
        public int Structure { get; internal set; }

        /// <summary>Safety score (0–100).</summary>
        public int Safety { get; internal set; }

        /// <summary>OutputGuidance score (0–100).</summary>
        public int OutputGuidance { get; internal set; }

        /// <summary>Conciseness score (0–100).</summary>
        public int Conciseness { get; internal set; }

        /// <summary>Overall weighted score (0–100).</summary>
        public int Overall => (int)Math.Round(
            Clarity * 0.20 + Specificity * 0.20 + Structure * 0.20 +
            Safety * 0.15 + OutputGuidance * 0.15 + Conciseness * 0.10);

        /// <summary>Returns a dictionary of dimension name to score.</summary>
        public Dictionary<string, int> ToDictionary() => new Dictionary<string, int>
        {
            ["Clarity"] = Clarity,
            ["Specificity"] = Specificity,
            ["Structure"] = Structure,
            ["Safety"] = Safety,
            ["OutputGuidance"] = OutputGuidance,
            ["Conciseness"] = Conciseness,
            ["Overall"] = Overall
        };
    }

    /// <summary>
    /// A snapshot of the prompt at a given generation.
    /// </summary>
    public class AutopilotGeneration
    {
        /// <summary>Gets the generation number (0 = original).</summary>
        public int Number { get; internal set; }

        /// <summary>Gets the prompt text at this generation.</summary>
        public string PromptText { get; internal set; } = "";

        /// <summary>Gets the scorecard at this generation.</summary>
        public AutopilotScorecard Score { get; internal set; } = new AutopilotScorecard();

        /// <summary>Gets diagnoses found at this generation.</summary>
        public List<AutopilotDiagnosis> Diagnoses { get; internal set; } = new List<AutopilotDiagnosis>();

        /// <summary>Gets the delta from previous generation's overall score.</summary>
        public int Delta { get; internal set; }
    }

    /// <summary>
    /// Final result from the autopilot refinement run.
    /// </summary>
    public class AutopilotResult
    {
        /// <summary>Gets the original prompt text.</summary>
        public string OriginalPrompt { get; internal set; } = "";

        /// <summary>Gets the final refined prompt text.</summary>
        public string RefinedPrompt { get; internal set; } = "";

        /// <summary>Gets the generation history.</summary>
        public List<AutopilotGeneration> Generations { get; internal set; } = new List<AutopilotGeneration>();

        /// <summary>Gets the initial overall score.</summary>
        public int InitialScore => Generations.FirstOrDefault()?.Score.Overall ?? 0;

        /// <summary>Gets the final overall score.</summary>
        public int FinalScore => Generations.LastOrDefault()?.Score.Overall ?? 0;

        /// <summary>Gets the total score improvement.</summary>
        public int Improvement => FinalScore - InitialScore;

        /// <summary>Gets the total number of fixes applied.</summary>
        public int TotalFixesApplied => Generations.SelectMany(g => g.Diagnoses).Count(d => d.Applied);

        /// <summary>Gets the reason the autopilot stopped.</summary>
        public string StopReason { get; internal set; } = "";

        /// <summary>Gets whether the target score was reached.</summary>
        public bool TargetReached { get; internal set; }

        /// <summary>Renders the full run as a human-readable report.</summary>
        public string ToReport()
        {
            var lines = new List<string>
            {
                "╔══════════════════════════════════════════╗",
                "║        PROMPT AUTOPILOT REPORT           ║",
                "╚══════════════════════════════════════════╝",
                "",
                $"Generations: {Generations.Count}  |  Fixes applied: {TotalFixesApplied}",
                $"Score: {InitialScore} → {FinalScore}  ({(Improvement >= 0 ? "+" : "")}{Improvement})",
                $"Target reached: {(TargetReached ? "YES ✓" : "NO")}  |  Stop reason: {StopReason}",
                ""
            };

            foreach (var gen in Generations)
            {
                lines.Add($"── Generation {gen.Number} ──  (Overall: {gen.Score.Overall}, Δ{(gen.Delta >= 0 ? "+" : "")}{gen.Delta})");
                var sc = gen.Score;
                lines.Add($"  Clarity={sc.Clarity}  Specificity={sc.Specificity}  Structure={sc.Structure}  Safety={sc.Safety}  Output={sc.OutputGuidance}  Concise={sc.Conciseness}");

                if (gen.Diagnoses.Any())
                {
                    foreach (var d in gen.Diagnoses)
                    {
                        var tag = d.Applied ? "✓ FIXED" : "· NOTED";
                        lines.Add($"  [{tag}] [{d.Dimension}] (sev {d.Severity:F1}) {d.Description}");
                    }
                }
                else
                {
                    lines.Add("  No issues found.");
                }
                lines.Add("");
            }

            lines.Add("── Final Prompt ──");
            lines.Add(Generations.LastOrDefault()?.PromptText ?? "(empty)");
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Configuration for an autopilot run.
    /// </summary>
    public class AutopilotConfig
    {
        /// <summary>Maximum generations before stopping. Default 10.</summary>
        public int MaxGenerations { get; set; } = 10;

        /// <summary>Target overall score (0–100). Stops when reached. Default 85.</summary>
        public int TargetScore { get; set; } = 85;

        /// <summary>Minimum improvement per generation to continue. Default 1.</summary>
        public int MinImprovement { get; set; } = 1;

        /// <summary>Refinement strategy. Default WorstFirst.</summary>
        public AutopilotStrategy Strategy { get; set; } = AutopilotStrategy.WorstFirst;

        /// <summary>Dimensions to skip during refinement.</summary>
        public HashSet<string> SkipDimensions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maximum prompt length in characters. Autopilot won't expand beyond this. Default 4000.</summary>
        public int MaxPromptLength { get; set; } = 4000;
    }

    /// <summary>
    /// Autonomous iterative prompt refinement engine.
    /// Analyzes, diagnoses, and auto-improves prompts over multiple generations
    /// until a quality threshold is reached or no further improvement is possible.
    /// </summary>
    public static class PromptAutopilot
    {
        // ── Preset configs ──

        /// <summary>Quick pass with 3 generations and low target.</summary>
        public static AutopilotConfig QuickFix => new AutopilotConfig
        {
            MaxGenerations = 3, TargetScore = 70, Strategy = AutopilotStrategy.WorstFirst
        };

        /// <summary>Balanced refinement (default settings).</summary>
        public static AutopilotConfig Balanced => new AutopilotConfig();

        /// <summary>Deep polish with aggressive strategy and high target.</summary>
        public static AutopilotConfig DeepPolish => new AutopilotConfig
        {
            MaxGenerations = 15, TargetScore = 95, Strategy = AutopilotStrategy.Aggressive
        };

        /// <summary>Conservative mode that avoids changing meaning.</summary>
        public static AutopilotConfig SafeMode => new AutopilotConfig
        {
            MaxGenerations = 5, TargetScore = 80, Strategy = AutopilotStrategy.Conservative
        };

        /// <summary>
        /// Run the autopilot refinement loop on the given prompt.
        /// </summary>
        /// <param name="prompt">The prompt text to refine.</param>
        /// <param name="config">Optional configuration. Uses Balanced if null.</param>
        /// <returns>The autopilot result with full generation history.</returns>
        public static AutopilotResult Refine(string prompt, AutopilotConfig? config = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

            var cfg = config ?? Balanced;
            var result = new AutopilotResult { OriginalPrompt = prompt };
            var current = prompt;
            int prevScore = 0;
            int staleCount = 0;
            int rotatingIndex = 0;
            var dimensions = new[] { "Clarity", "Specificity", "Structure", "Safety", "OutputGuidance", "Conciseness" };

            for (int gen = 0; gen <= cfg.MaxGenerations; gen++)
            {
                var scorecard = Score(current);
                var diagnoses = Diagnose(current, scorecard, cfg.SkipDimensions);
                int delta = gen == 0 ? 0 : scorecard.Overall - prevScore;

                var generation = new AutopilotGeneration
                {
                    Number = gen,
                    PromptText = current,
                    Score = scorecard,
                    Diagnoses = diagnoses,
                    Delta = delta
                };
                result.Generations.Add(generation);

                // Check stop conditions
                if (scorecard.Overall >= cfg.TargetScore)
                {
                    result.StopReason = $"Target score {cfg.TargetScore} reached";
                    result.TargetReached = true;
                    break;
                }

                if (gen == cfg.MaxGenerations)
                {
                    result.StopReason = $"Max generations ({cfg.MaxGenerations}) reached";
                    break;
                }

                if (gen > 0 && delta < cfg.MinImprovement)
                {
                    staleCount++;
                    if (staleCount >= 2)
                    {
                        result.StopReason = "No improvement for 2 consecutive generations";
                        break;
                    }
                }
                else
                {
                    staleCount = 0;
                }

                if (!diagnoses.Any())
                {
                    result.StopReason = "No issues to fix";
                    break;
                }

                // Apply fixes based on strategy
                var toFix = SelectFixes(diagnoses, cfg.Strategy, dimensions, ref rotatingIndex);
                current = ApplyFixes(current, toFix, cfg.MaxPromptLength);

                foreach (var d in toFix) d.Applied = true;
                prevScore = scorecard.Overall;
            }

            result.RefinedPrompt = result.Generations.Last().PromptText;
            return result;
        }

        // ── Scoring ──

        /// <summary>Score a prompt across all quality dimensions.</summary>
        public static AutopilotScorecard Score(string prompt)
        {
            return new AutopilotScorecard
            {
                Clarity = ScoreClarity(prompt),
                Specificity = ScoreSpecificity(prompt),
                Structure = ScoreStructure(prompt),
                Safety = ScoreSafety(prompt),
                OutputGuidance = ScoreOutputGuidance(prompt),
                Conciseness = ScoreConciseness(prompt)
            };
        }

        private static int ScoreClarity(string p)
        {
            int score = 50;
            // Shorter sentences improve clarity
            var sentences = Regex.Split(p, @"[.!?]+").Where(s => s.Trim().Length > 0).ToList();
            double avgLen = sentences.Any() ? sentences.Average(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length) : 0;
            if (avgLen > 0 && avgLen <= 20) score += 15;
            else if (avgLen <= 30) score += 5;

            // Active voice indicators
            if (!Regex.IsMatch(p, @"\b(is|are|was|were)\s+(being\s+)?\w+ed\b", RegexOptions.IgnoreCase))
                score += 10;

            // Clear instruction verbs
            var instructionVerbs = new[] { "write", "list", "describe", "explain", "generate", "create", "analyze", "summarize", "compare", "evaluate", "provide", "return", "output" };
            int verbCount = instructionVerbs.Count(v => Regex.IsMatch(p, $@"\b{v}\b", RegexOptions.IgnoreCase));
            score += Math.Min(verbCount * 5, 15);

            // No ambiguous words
            var ambiguous = new[] { "maybe", "perhaps", "somehow", "something like", "sort of", "kind of", "stuff", "things" };
            int ambiguousCount = ambiguous.Count(a => p.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);
            score -= ambiguousCount * 5;

            return Math.Max(0, Math.Min(100, score));
        }

        private static int ScoreSpecificity(string p)
        {
            int score = 40;
            // Numbers/quantities
            if (Regex.IsMatch(p, @"\b\d+\b")) score += 10;

            // Quoted examples
            if (p.Contains('"') || p.Contains("```") || p.Contains("example")) score += 10;

            // Named entities/proper nouns
            if (Regex.IsMatch(p, @"\b[A-Z][a-z]+(?:\s[A-Z][a-z]+)+\b")) score += 5;

            // Constraints
            var constraints = new[] { "must", "should", "only", "exactly", "no more than", "at least", "between", "limit", "maximum", "minimum" };
            int cCount = constraints.Count(c => p.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0);
            score += Math.Min(cCount * 5, 20);

            // Persona/role
            if (Regex.IsMatch(p, @"\b(you are|act as|role|persona|expert)\b", RegexOptions.IgnoreCase))
                score += 10;

            // Length bonus for detail
            if (p.Length > 200) score += 5;
            if (p.Length > 500) score += 5;

            return Math.Max(0, Math.Min(100, score));
        }

        private static int ScoreStructure(string p)
        {
            int score = 40;
            // Has sections/headers
            if (Regex.IsMatch(p, @"^#+\s", RegexOptions.Multiline)) score += 15;
            // Has lists
            if (Regex.IsMatch(p, @"^[\-\*\d]+[.)]\s", RegexOptions.Multiline)) score += 10;
            // Has line breaks / paragraphs
            if (p.Contains("\n\n")) score += 10;
            // Has delimiters
            if (p.Contains("---") || p.Contains("===") || p.Contains("```")) score += 10;
            // Reasonable length (not a single blob)
            int lines = p.Split('\n').Length;
            if (lines >= 3 && lines <= 100) score += 10;
            // Not just one huge paragraph
            if (lines < 3 && p.Length > 300) score -= 10;

            return Math.Max(0, Math.Min(100, score));
        }

        private static int ScoreSafety(string p)
        {
            int score = 50;
            // Boundary statements
            var boundaries = new[] { "do not", "don't", "never", "avoid", "refrain", "must not" };
            int bCount = boundaries.Count(b => p.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0);
            score += Math.Min(bCount * 8, 24);

            // Guardrails
            if (Regex.IsMatch(p, @"\b(if unsure|when uncertain|if you don'?t know|decline)\b", RegexOptions.IgnoreCase))
                score += 15;

            // Scope limitation
            if (Regex.IsMatch(p, @"\b(only|scope|limited to|within|stay)\b", RegexOptions.IgnoreCase))
                score += 10;

            return Math.Max(0, Math.Min(100, score));
        }

        private static int ScoreOutputGuidance(string p)
        {
            int score = 30;
            // Format specification
            var formats = new[] { "json", "xml", "csv", "markdown", "bullet", "table", "list", "format", "schema" };
            int fCount = formats.Count(f => p.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
            score += Math.Min(fCount * 8, 24);

            // Length guidance
            if (Regex.IsMatch(p, @"\b(short|brief|concise|detailed|comprehensive|one.?line|paragraph|sentences?\b.*\d|words?\b.*\d|\d+\s*words?)", RegexOptions.IgnoreCase))
                score += 15;

            // Example output
            if (Regex.IsMatch(p, @"\b(example|sample|like this|for instance|e\.g\.)\b", RegexOptions.IgnoreCase))
                score += 15;

            // Structured output markers
            if (p.Contains("```") || p.Contains("{") || Regex.IsMatch(p, @"\bfield\b", RegexOptions.IgnoreCase))
                score += 10;

            return Math.Max(0, Math.Min(100, score));
        }

        private static int ScoreConciseness(string p)
        {
            int score = 80;
            // Penalty for excessive length
            if (p.Length > 3000) score -= 20;
            else if (p.Length > 2000) score -= 10;

            // Penalty for filler words
            var fillers = new[] { "please note that", "it is important to", "in order to", "as a matter of fact", "basically", "essentially", "actually", "in terms of" };
            int fillerCount = fillers.Count(f => p.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
            score -= fillerCount * 5;

            // Penalty for repetition (duplicate sentences)
            var sents = Regex.Split(p, @"[.!?]+").Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 10).ToList();
            int dupes = sents.Count - sents.Distinct().Count();
            score -= dupes * 10;

            // Too short is also bad (under-specified)
            if (p.Length < 20) score -= 20;

            return Math.Max(0, Math.Min(100, score));
        }

        // ── Diagnosis ──

        private static List<AutopilotDiagnosis> Diagnose(string prompt, AutopilotScorecard sc, HashSet<string> skip)
        {
            var results = new List<AutopilotDiagnosis>();

            if (!skip.Contains("Clarity"))
            {
                if (sc.Clarity < 60)
                {
                    var ambiguous = new[] { "maybe", "perhaps", "somehow", "something like", "sort of", "kind of", "stuff", "things" };
                    var found = ambiguous.Where(a => prompt.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    if (found.Any())
                        results.Add(new AutopilotDiagnosis { Dimension = "Clarity", Severity = 0.7, Description = $"Ambiguous language: {string.Join(", ", found)}", Fix = "remove_ambiguous" });

                    if (!Regex.IsMatch(prompt, @"\b(write|list|describe|explain|generate|create|analyze|summarize|compare|evaluate|provide|return|output)\b", RegexOptions.IgnoreCase))
                        results.Add(new AutopilotDiagnosis { Dimension = "Clarity", Severity = 0.8, Description = "No clear instruction verb found", Fix = "add_instruction_verb" });
                }
            }

            if (!skip.Contains("Specificity"))
            {
                if (sc.Specificity < 60)
                {
                    if (!Regex.IsMatch(prompt, @"\b(you are|act as|role|persona|expert)\b", RegexOptions.IgnoreCase))
                        results.Add(new AutopilotDiagnosis { Dimension = "Specificity", Severity = 0.6, Description = "No role/persona defined", Fix = "add_role" });

                    if (!Regex.IsMatch(prompt, @"\b\d+\b"))
                        results.Add(new AutopilotDiagnosis { Dimension = "Specificity", Severity = 0.5, Description = "No numeric constraints or quantities", Fix = "add_constraints" });
                }
            }

            if (!skip.Contains("Structure"))
            {
                if (sc.Structure < 60)
                {
                    if (!Regex.IsMatch(prompt, @"^[\-\*\d]+[.)]\s", RegexOptions.Multiline) && !Regex.IsMatch(prompt, @"^#+\s", RegexOptions.Multiline))
                        results.Add(new AutopilotDiagnosis { Dimension = "Structure", Severity = 0.6, Description = "No lists or headers for organization", Fix = "add_structure" });

                    if (prompt.Split('\n').Length < 3 && prompt.Length > 200)
                        results.Add(new AutopilotDiagnosis { Dimension = "Structure", Severity = 0.7, Description = "Long prompt in single block without breaks", Fix = "add_breaks" });
                }
            }

            if (!skip.Contains("Safety"))
            {
                if (sc.Safety < 60)
                {
                    if (!Regex.IsMatch(prompt, @"\b(do not|don't|never|avoid|must not)\b", RegexOptions.IgnoreCase))
                        results.Add(new AutopilotDiagnosis { Dimension = "Safety", Severity = 0.8, Description = "No boundary/constraint statements", Fix = "add_boundaries" });

                    if (!Regex.IsMatch(prompt, @"\b(if unsure|when uncertain|if you don'?t know)\b", RegexOptions.IgnoreCase))
                        results.Add(new AutopilotDiagnosis { Dimension = "Safety", Severity = 0.6, Description = "No uncertainty guardrail", Fix = "add_guardrail" });
                }
            }

            if (!skip.Contains("OutputGuidance"))
            {
                if (sc.OutputGuidance < 60)
                {
                    if (!Regex.IsMatch(prompt, @"\b(json|xml|csv|markdown|bullet|table|list|format|schema)\b", RegexOptions.IgnoreCase))
                        results.Add(new AutopilotDiagnosis { Dimension = "OutputGuidance", Severity = 0.7, Description = "No output format specified", Fix = "add_format" });

                    if (!Regex.IsMatch(prompt, @"\b(example|sample|like this|for instance|e\.g\.)\b", RegexOptions.IgnoreCase))
                        results.Add(new AutopilotDiagnosis { Dimension = "OutputGuidance", Severity = 0.5, Description = "No example output provided", Fix = "add_example" });
                }
            }

            if (!skip.Contains("Conciseness"))
            {
                if (sc.Conciseness < 60)
                {
                    var fillers = new[] { "please note that", "it is important to", "in order to", "as a matter of fact", "basically", "essentially", "actually", "in terms of" };
                    var found = fillers.Where(f => prompt.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    if (found.Any())
                        results.Add(new AutopilotDiagnosis { Dimension = "Conciseness", Severity = 0.5, Description = $"Filler phrases: {string.Join(", ", found)}", Fix = "remove_fillers" });

                    var sents = Regex.Split(prompt, @"[.!?]+").Select(s => s.Trim()).Where(s => s.Length > 10).ToList();
                    var dupes = sents.GroupBy(s => s.ToLowerInvariant()).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    if (dupes.Any())
                        results.Add(new AutopilotDiagnosis { Dimension = "Conciseness", Severity = 0.6, Description = "Duplicate sentences detected", Fix = "remove_duplicates" });
                }
            }

            return results.OrderByDescending(d => d.Severity).ToList();
        }

        // ── Fix selection ──

        private static List<AutopilotDiagnosis> SelectFixes(List<AutopilotDiagnosis> diagnoses, AutopilotStrategy strategy, string[] dimensions, ref int rotatingIndex)
        {
            switch (strategy)
            {
                case AutopilotStrategy.WorstFirst:
                    return diagnoses.Take(1).ToList();

                case AutopilotStrategy.Shotgun:
                    return diagnoses.ToList();

                case AutopilotStrategy.Rotating:
                    var dim = dimensions[rotatingIndex % dimensions.Length];
                    rotatingIndex++;
                    var forDim = diagnoses.Where(d => d.Dimension.Equals(dim, StringComparison.OrdinalIgnoreCase)).ToList();
                    return forDim.Any() ? forDim : diagnoses.Take(1).ToList();

                case AutopilotStrategy.Conservative:
                    return diagnoses.Where(d => d.Severity <= 0.6).Take(2).ToList();

                case AutopilotStrategy.Aggressive:
                    return diagnoses.Where(d => d.Severity >= 0.5).ToList();

                default:
                    return diagnoses.Take(1).ToList();
            }
        }

        // ── Fix application ──

        private static string ApplyFixes(string prompt, List<AutopilotDiagnosis> fixes, int maxLen)
        {
            var result = prompt;

            foreach (var fix in fixes)
            {
                result = fix.Fix switch
                {
                    "remove_ambiguous" => RemoveAmbiguous(result),
                    "add_instruction_verb" => AddInstructionVerb(result),
                    "add_role" => AddRole(result),
                    "add_constraints" => AddConstraints(result),
                    "add_structure" => AddStructure(result),
                    "add_breaks" => AddBreaks(result),
                    "add_boundaries" => AddBoundaries(result),
                    "add_guardrail" => AddGuardrail(result),
                    "add_format" => AddFormat(result),
                    "add_example" => AddExample(result),
                    "remove_fillers" => RemoveFillers(result),
                    "remove_duplicates" => RemoveDuplicates(result),
                    _ => result
                };

                // Enforce max length
                if (result.Length > maxLen)
                    result = result.Substring(0, maxLen);
            }

            return result;
        }

        private static string RemoveAmbiguous(string p)
        {
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["maybe"] = "", ["perhaps"] = "", ["somehow"] = "",
                ["something like"] = "specifically", ["sort of"] = "",
                ["kind of"] = "", ["stuff"] = "content", ["things"] = "items"
            };

            foreach (var kvp in replacements)
                p = Regex.Replace(p, $@"\b{Regex.Escape(kvp.Key)}\b", kvp.Value, RegexOptions.IgnoreCase);

            return Regex.Replace(p, @"\s{2,}", " ").Trim();
        }

        private static string AddInstructionVerb(string p)
        {
            // Prepend a clear instruction if none exists
            if (!p.TrimStart().StartsWith("You", StringComparison.OrdinalIgnoreCase))
                return "Provide the following:\n\n" + p;
            return p;
        }

        private static string AddRole(string p)
        {
            return "You are an expert assistant.\n\n" + p;
        }

        private static string AddConstraints(string p)
        {
            return p + "\n\nLimit your response to 3-5 key points.";
        }

        private static string AddStructure(string p)
        {
            // Split long text into numbered steps if it has multiple sentences
            var sentences = Regex.Split(p, @"(?<=[.!?])\s+").Where(s => s.Trim().Length > 0).ToArray();
            if (sentences.Length >= 3)
            {
                return string.Join("\n", sentences.Select((s, i) => $"{i + 1}. {s.Trim()}"));
            }
            return "## Task\n\n" + p;
        }

        private static string AddBreaks(string p)
        {
            // Insert paragraph breaks at sentence boundaries in long blocks
            var sentences = Regex.Split(p, @"(?<=[.!?])\s+").Where(s => s.Trim().Length > 0).ToArray();
            if (sentences.Length >= 4)
            {
                int mid = sentences.Length / 2;
                var first = string.Join(" ", sentences.Take(mid));
                var second = string.Join(" ", sentences.Skip(mid));
                return first + "\n\n" + second;
            }
            return p;
        }

        private static string AddBoundaries(string p)
        {
            return p + "\n\nDo not include information outside the requested scope.";
        }

        private static string AddGuardrail(string p)
        {
            return p + "\n\nIf unsure about any aspect, state your uncertainty rather than guessing.";
        }

        private static string AddFormat(string p)
        {
            return p + "\n\nFormat your response as a structured list.";
        }

        private static string AddExample(string p)
        {
            return p + "\n\nFor example, a response might look like:\n- Point 1: [explanation]\n- Point 2: [explanation]";
        }

        private static string RemoveFillers(string p)
        {
            var fillers = new[] { "please note that ", "it is important to note that ", "in order to ", "as a matter of fact, ", "basically, ", "essentially, ", "actually, ", "in terms of " };
            foreach (var f in fillers)
                p = Regex.Replace(p, Regex.Escape(f), "", RegexOptions.IgnoreCase);
            return Regex.Replace(p, @"\s{2,}", " ").Trim();
        }

        private static string RemoveDuplicates(string p)
        {
            var sentences = Regex.Split(p, @"(?<=[.!?])\s+").Where(s => s.Trim().Length > 0).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unique = new List<string>();
            foreach (var s in sentences)
            {
                if (seen.Add(s.Trim()))
                    unique.Add(s.Trim());
            }
            return string.Join(" ", unique);
        }
    }
}
