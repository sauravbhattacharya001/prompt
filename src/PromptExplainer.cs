namespace Prompt
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Identified prompting technique found within a prompt.
    /// </summary>
    public class ExplainerTechnique
    {
        /// <summary>Gets the technique name (e.g., "Chain-of-Thought", "Few-Shot").</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets a short description of the technique.</summary>
        public string Description { get; internal set; } = "";

        /// <summary>Gets the confidence level (0.0–1.0) that this technique is present.</summary>
        public double Confidence { get; internal set; }

        /// <summary>Gets the text evidence that triggered detection.</summary>
        public string Evidence { get; internal set; } = "";

        /// <summary>Gets the character offset where the technique was detected.</summary>
        public int Offset { get; internal set; }
    }

    /// <summary>
    /// A logical section identified within a prompt.
    /// </summary>
    public class ExplainerSection
    {
        /// <summary>Gets the section type (e.g., "Role", "Context", "Instruction", "Constraint", "Output Format", "Example").</summary>
        public string Type { get; internal set; } = "";

        /// <summary>Gets the raw text of this section.</summary>
        public string Text { get; internal set; } = "";

        /// <summary>Gets the starting line number (1-based).</summary>
        public int StartLine { get; internal set; }

        /// <summary>Gets the ending line number (1-based).</summary>
        public int EndLine { get; internal set; }

        /// <summary>Gets an explanation of what this section does.</summary>
        public string Explanation { get; internal set; } = "";
    }

    /// <summary>
    /// Improvement suggestion for a prompt.
    /// </summary>
    public class ExplainerSuggestion
    {
        /// <summary>Gets the suggestion category.</summary>
        public string Category { get; internal set; } = "";

        /// <summary>Gets the suggestion text.</summary>
        public string Message { get; internal set; } = "";

        /// <summary>Gets the severity: Info, Warning, or Critical.</summary>
        public string Severity { get; internal set; } = "Info";
    }

    /// <summary>
    /// Complete analysis result from <see cref="PromptExplainer"/>.
    /// </summary>
    public class ExplainResult
    {
        /// <summary>Gets the original prompt text.</summary>
        public string Prompt { get; internal set; } = "";

        /// <summary>Gets the identified sections.</summary>
        public IReadOnlyList<ExplainerSection> Sections { get; internal set; } = Array.Empty<ExplainerSection>();

        /// <summary>Gets detected prompting techniques.</summary>
        public IReadOnlyList<ExplainerTechnique> Techniques { get; internal set; } = Array.Empty<ExplainerTechnique>();

        /// <summary>Gets improvement suggestions.</summary>
        public IReadOnlyList<ExplainerSuggestion> Suggestions { get; internal set; } = Array.Empty<ExplainerSuggestion>();

        /// <summary>Gets the overall complexity score (1–10).</summary>
        public int ComplexityScore { get; internal set; }

        /// <summary>Gets estimated token count.</summary>
        public int EstimatedTokens { get; internal set; }

        /// <summary>Gets a one-line summary of what the prompt does.</summary>
        public string Summary { get; internal set; } = "";

        /// <summary>
        /// Returns a human-readable report of the analysis.
        /// </summary>
        public string ToReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══ Prompt Analysis Report ═══");
            sb.AppendLine();
            sb.AppendLine($"Summary: {Summary}");
            sb.AppendLine($"Complexity: {ComplexityScore}/10 | Tokens: ~{EstimatedTokens}");
            sb.AppendLine();

            if (Sections.Count > 0)
            {
                sb.AppendLine("── Sections ──");
                foreach (var s in Sections)
                {
                    sb.AppendLine($"  [{s.Type}] Lines {s.StartLine}–{s.EndLine}: {s.Explanation}");
                }
                sb.AppendLine();
            }

            if (Techniques.Count > 0)
            {
                sb.AppendLine("── Techniques Detected ──");
                foreach (var t in Techniques)
                {
                    sb.AppendLine($"  • {t.Name} ({t.Confidence:P0}): {t.Description}");
                }
                sb.AppendLine();
            }

            if (Suggestions.Count > 0)
            {
                sb.AppendLine("── Suggestions ──");
                foreach (var s in Suggestions)
                {
                    var icon = s.Severity == "Critical" ? "🔴" : s.Severity == "Warning" ? "🟡" : "🔵";
                    sb.AppendLine($"  {icon} [{s.Category}] {s.Message}");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Analyzes prompts to identify sections, techniques, and improvement opportunities.
    /// Useful for understanding complex prompts, teaching prompt engineering, and auditing quality.
    /// </summary>
    public class PromptExplainer
    {
        private static readonly (string Pattern, string Name, string Description)[] TechniquePatterns = new[]
        {
            (@"(?i)\b(think|reason|let'?s think)\s+(step[- ]by[- ]step|through|carefully|about)", "Chain-of-Thought",
                "Encourages the model to reason through the problem incrementally"),
            (@"(?i)\b(example|for instance|e\.g\.|here'?s? (?:an? )?(?:example|sample))\s*[:\n]", "Few-Shot",
                "Provides examples to guide the model's response pattern"),
            (@"(?i)^(?:you are|act as|pretend you'?re|imagine you'?re|role:\s*)\s*(?:a |an )", "Role Assignment",
                "Assigns a persona or role to guide the model's behavior"),
            (@"(?i)\b(format|output|respond|reply|answer)\s+(?:as|in|using|with)\s+(json|xml|csv|yaml|markdown|bullet|table|list)", "Output Formatting",
                "Specifies the desired output structure or format"),
            (@"(?i)\b(do not|don'?t|never|avoid|must not|refrain from)\b", "Negative Constraint",
                "Uses negative instructions to restrict model behavior"),
            (@"(?i)\b(first|then|next|finally|step \d|1\.|2\.|3\.)", "Sequential Instructions",
                "Breaks the task into ordered steps"),
            (@"(?i)\b(delimit|triple|```|<\w+>|###|---|===|\[\[|\]\])", "Delimiter Usage",
                "Uses delimiters to separate sections or mark boundaries"),
            (@"(?i)\b(rate|score|evaluate|assess|rank)\s+.{0,30}\b(1[- ](?:to|through)[- ](?:5|10)|scale|rating)", "Evaluation Framework",
                "Asks the model to evaluate or score against criteria"),
            (@"(?i)\b(before|after)\s+(?:answering|responding|you (?:answer|respond))", "Meta-Instruction",
                "Provides instructions about how to process the request itself"),
            (@"(?i)\b(if\s+.{3,40}then|when\s+.{3,40}(?:do|respond|say|use))\b", "Conditional Logic",
                "Includes conditional branches for different scenarios"),
            (@"(?i)\b(system|user|assistant)\s*:", "Chat Role Markers",
                "Uses explicit chat completion role markers"),
            (@"(?i)\b(few[- ]shot|zero[- ]shot|one[- ]shot|chain[- ]of[- ]thought|CoT|ReAct|ToT|tree[- ]of[- ]thought)\b", "Named Technique Reference",
                "Explicitly references a known prompting technique by name"),
        };

        private static readonly (string Pattern, string SectionType, string ExplanationTemplate)[] SectionPatterns = new[]
        {
            (@"(?im)^(?:you are|act as|role:)\s*", "Role", "Defines the persona or role for the AI"),
            (@"(?im)^(?:context|background|situation|given):\s*", "Context", "Provides background information"),
            (@"(?im)^(?:task|instruction|objective|goal|please|your (?:task|job|goal)):\s*", "Instruction", "States the main task or objective"),
            (@"(?im)^(?:constraint|rule|requirement|limitation|do not|don't)s?:\s*", "Constraint", "Defines boundaries or restrictions"),
            (@"(?im)^(?:output|format|response|expected)(?:\s+format)?:\s*", "Output Format", "Specifies desired output structure"),
            (@"(?im)^(?:example|sample|demonstration|e\.g\.)s?:\s*", "Example", "Provides examples for guidance"),
            (@"(?im)^(?:note|tip|hint|important|warning|caveat):\s*", "Note", "Adds clarifying notes or warnings"),
        };

        /// <summary>
        /// Analyzes a prompt and returns a detailed breakdown of its structure,
        /// techniques, and improvement suggestions.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>An <see cref="ExplainResult"/> with the full analysis.</returns>
        public ExplainResult Explain(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new ExplainResult
                {
                    Prompt = prompt ?? "",
                    Summary = "Empty prompt",
                    ComplexityScore = 0,
                    EstimatedTokens = 0,
                };
            }

            var techniques = DetectTechniques(prompt);
            var sections = DetectSections(prompt);
            var suggestions = GenerateSuggestions(prompt, techniques, sections);
            var tokens = EstimateTokens(prompt);
            var complexity = CalculateComplexity(prompt, techniques, sections);
            var summary = GenerateSummary(prompt, sections, techniques);

            return new ExplainResult
            {
                Prompt = prompt,
                Sections = sections,
                Techniques = techniques,
                Suggestions = suggestions,
                ComplexityScore = complexity,
                EstimatedTokens = tokens,
                Summary = summary,
            };
        }

        private List<ExplainerTechnique> DetectTechniques(string prompt)
        {
            var results = new List<ExplainerTechnique>();
            foreach (var (pattern, name, desc) in TechniquePatterns)
            {
                var matches = Regex.Matches(prompt, pattern);
                if (matches.Count > 0)
                {
                    var best = matches[0];
                    // Higher confidence with more matches
                    double confidence = Math.Min(1.0, 0.6 + matches.Count * 0.15);
                    results.Add(new ExplainerTechnique
                    {
                        Name = name,
                        Description = desc,
                        Confidence = confidence,
                        Evidence = best.Value.Trim(),
                        Offset = best.Index,
                    });
                }
            }
            return results.OrderByDescending(t => t.Confidence).ToList();
        }

        private List<ExplainerSection> DetectSections(string prompt)
        {
            var lines = prompt.Split('\n');
            var sections = new List<ExplainerSection>();
            var currentType = "General";
            var currentStart = 1;
            var currentLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string? matchedType = null;
                string? explanation = null;

                foreach (var (pattern, sectionType, explTemplate) in SectionPatterns)
                {
                    if (Regex.IsMatch(line, pattern))
                    {
                        matchedType = sectionType;
                        explanation = explTemplate;
                        break;
                    }
                }

                // Also detect markdown-style headers as section boundaries
                if (matchedType == null && Regex.IsMatch(line, @"^#{1,3}\s+\S"))
                {
                    matchedType = "Heading";
                    explanation = "Section header: " + line.TrimStart('#', ' ');
                }

                if (matchedType != null && currentLines.Count > 0)
                {
                    sections.Add(new ExplainerSection
                    {
                        Type = currentType,
                        Text = string.Join("\n", currentLines),
                        StartLine = currentStart,
                        EndLine = i, // previous line
                        Explanation = GetSectionExplanation(currentType, currentLines),
                    });
                    currentType = matchedType;
                    currentStart = i + 1;
                    currentLines = new List<string> { line };
                }
                else
                {
                    if (matchedType != null) currentType = matchedType;
                    currentLines.Add(line);
                }
            }

            if (currentLines.Count > 0)
            {
                sections.Add(new ExplainerSection
                {
                    Type = currentType,
                    Text = string.Join("\n", currentLines),
                    StartLine = currentStart,
                    EndLine = lines.Length,
                    Explanation = GetSectionExplanation(currentType, currentLines),
                });
            }

            return sections;
        }

        private string GetSectionExplanation(string type, List<string> lines)
        {
            var textLen = string.Join("", lines).Length;
            return type switch
            {
                "Role" => $"Assigns a persona to the model ({lines.Count} lines)",
                "Context" => $"Provides background context ({textLen} chars)",
                "Instruction" => $"Core task instruction ({lines.Count} lines)",
                "Constraint" => $"Behavioral constraints and restrictions",
                "Output Format" => $"Output structure specification",
                "Example" => $"Example(s) for few-shot guidance",
                "Note" => $"Supplementary note or caveat",
                "Heading" => lines.Count > 0 ? lines[0].TrimStart('#', ' ') : "Section header",
                _ => $"General prompt content ({lines.Count} lines, {textLen} chars)",
            };
        }

        private List<ExplainerSuggestion> GenerateSuggestions(
            string prompt,
            List<ExplainerTechnique> techniques,
            List<ExplainerSection> sections)
        {
            var suggestions = new List<ExplainerSuggestion>();
            var tokens = EstimateTokens(prompt);

            // Check for missing role assignment
            if (!techniques.Any(t => t.Name == "Role Assignment"))
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Structure",
                    Message = "Consider adding a role assignment (e.g., 'You are a ...') to guide the model's behavior.",
                    Severity = "Info",
                });
            }

            // Check for missing output format
            if (!techniques.Any(t => t.Name == "Output Formatting"))
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Clarity",
                    Message = "No explicit output format specified. Consider defining the expected response format.",
                    Severity = "Info",
                });
            }

            // Warn about very short prompts
            if (tokens < 20)
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Completeness",
                    Message = "Very short prompt — consider adding more context or examples for better results.",
                    Severity = "Warning",
                });
            }

            // Warn about very long prompts
            if (tokens > 2000)
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Efficiency",
                    Message = $"Prompt is ~{tokens} tokens. Consider minifying with PromptMinifier or splitting into sections.",
                    Severity = "Warning",
                });
            }

            // Check for excessive negative constraints
            var negCount = Regex.Matches(prompt, @"(?i)\b(do not|don'?t|never|must not)\b").Count;
            if (negCount > 5)
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Best Practice",
                    Message = $"Found {negCount} negative constraints. Consider rephrasing some as positive instructions for clarity.",
                    Severity = "Warning",
                });
            }

            // Check for ambiguous pronouns
            var pronounMatches = Regex.Matches(prompt, @"(?i)\b(it|this|that|these|those|they)\b(?!\s+(?:is|are|was|were|should|must|will|can))");
            if (pronounMatches.Count > 8)
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Clarity",
                    Message = "High usage of pronouns (it/this/that) which may be ambiguous. Consider using specific nouns.",
                    Severity = "Info",
                });
            }

            // Suggest chain-of-thought for complex tasks
            if (!techniques.Any(t => t.Name == "Chain-of-Thought") &&
                (Regex.IsMatch(prompt, @"(?i)\b(analyze|compare|evaluate|explain|solve|debug|reason)\b")))
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Technique",
                    Message = "This appears to be a complex reasoning task. Consider adding 'Think step by step' for better results.",
                    Severity = "Info",
                });
            }

            // Check for no examples on classification/extraction tasks
            if (!techniques.Any(t => t.Name == "Few-Shot") &&
                Regex.IsMatch(prompt, @"(?i)\b(classify|categorize|extract|identify|label|tag)\b"))
            {
                suggestions.Add(new ExplainerSuggestion
                {
                    Category = "Technique",
                    Message = "Classification/extraction tasks benefit greatly from few-shot examples.",
                    Severity = "Warning",
                });
            }

            return suggestions;
        }

        private int CalculateComplexity(string prompt, List<ExplainerTechnique> techniques, List<ExplainerSection> sections)
        {
            int score = 1;
            var tokens = EstimateTokens(prompt);

            // Length contribution
            if (tokens > 50) score++;
            if (tokens > 200) score++;
            if (tokens > 500) score++;
            if (tokens > 1000) score++;

            // Technique count
            score += Math.Min(3, techniques.Count);

            // Section variety
            var uniqueSections = sections.Select(s => s.Type).Distinct().Count();
            if (uniqueSections > 2) score++;

            return Math.Min(10, score);
        }

        private string GenerateSummary(string prompt, List<ExplainerSection> sections, List<ExplainerTechnique> techniques)
        {
            var parts = new List<string>();

            // Identify the core intent from the first instruction-like section
            var instruction = sections.FirstOrDefault(s => s.Type == "Instruction");
            if (instruction != null)
            {
                var firstLine = instruction.Text.Split('\n').First().Trim();
                if (firstLine.Length > 80) firstLine = firstLine[..77] + "...";
                parts.Add(firstLine);
            }
            else
            {
                // Use first non-empty line
                var firstLine = prompt.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "Unknown";
                if (firstLine.Length > 60) firstLine = firstLine[..57] + "...";
                parts.Add(firstLine);
            }

            if (techniques.Count > 0)
            {
                var topTechniques = string.Join(", ", techniques.Take(3).Select(t => t.Name));
                parts.Add($"Uses: {topTechniques}");
            }

            return string.Join(" | ", parts);
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Rough estimate: ~4 characters per token for English
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
