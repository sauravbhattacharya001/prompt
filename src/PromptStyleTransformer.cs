namespace Prompt
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Defines the target communication style for prompt transformation.
    /// </summary>
    public enum PromptStyle
    {
        /// <summary>Professional, polished language suitable for business contexts.</summary>
        Formal,

        /// <summary>Relaxed, conversational tone for friendly interactions.</summary>
        Casual,

        /// <summary>Dense, jargon-friendly language for expert audiences.</summary>
        Technical,

        /// <summary>Plain language avoiding jargon, suitable for general audiences.</summary>
        Simple,

        /// <summary>Brief, action-oriented directives with minimal prose.</summary>
        Concise,

        /// <summary>Expanded instructions with extra context and examples.</summary>
        Verbose,

        /// <summary>Step-by-step instructional format.</summary>
        Instructional,

        /// <summary>Socratic style — guides via questions rather than directives.</summary>
        Socratic
    }

    /// <summary>
    /// Result of a prompt style transformation.
    /// </summary>
    public class StyleTransformResult
    {
        /// <summary>Gets the original prompt text.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>Gets the transformed prompt text.</summary>
        public string Transformed { get; internal set; } = "";

        /// <summary>Gets the target style applied.</summary>
        public PromptStyle TargetStyle { get; internal set; }

        /// <summary>Gets the list of transformations applied.</summary>
        public IReadOnlyList<string> Transformations { get; internal set; }
            = Array.Empty<string>();

        /// <summary>Gets the estimated token count of the original.</summary>
        public int OriginalTokens { get; internal set; }

        /// <summary>Gets the estimated token count of the transformed text.</summary>
        public int TransformedTokens { get; internal set; }

        /// <summary>Gets the token count difference (positive = grew, negative = shrank).</summary>
        public int TokenDelta => TransformedTokens - OriginalTokens;
    }

    /// <summary>
    /// Transforms prompt text between communication styles using rule-based
    /// text transformations. Useful for adapting a single prompt to different
    /// audiences or contexts without rewriting from scratch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var transformer = new PromptStyleTransformer();
    /// var result = transformer.Transform(
    ///     "Please help me write a function that sorts a list.",
    ///     PromptStyle.Concise
    /// );
    /// // → "Help me write a function that sorts a list."
    ///
    /// var formal = transformer.Transform(
    ///     "Hey, can you fix this bug? It's breaking stuff.",
    ///     PromptStyle.Formal
    /// );
    /// // → "Greetings, could you address this defect? It is causing failures in items."
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptStyleTransformer
    {
        private static readonly (string pattern, string replacement)[] CasualToFormal = new[]
        {
            (@"\bHey\b", "Greetings"),
            (@"\bhey\b", "greetings"),
            (@"\bhi\b(?!gh|dd|er|ll|st|nd)", "hello"),
            (@"\bHi\b(?!gh|dd|er|ll|st|nd)", "Hello"),
            (@"\bcan you\b", "could you"),
            (@"\bCan you\b", "Could you"),
            (@"\bwanna\b", "want to"),
            (@"\bgonna\b", "going to"),
            (@"\bgotta\b", "have to"),
            (@"\bkinda\b", "somewhat"),
            (@"\bsorta\b", "somewhat"),
            (@"\bstuff\b", "items"),
            (@"\bthing\b", "element"),
            (@"\bthings\b", "elements"),
            (@"\bcool\b", "acceptable"),
            (@"\bawesome\b", "excellent"),
            (@"\bgreat\b", "excellent"),
            (@"\bfix\b", "address"),
            (@"\bFix\b", "Address"),
            (@"\bbug\b", "defect"),
            (@"\bbugs\b", "defects"),
            (@"\bbreaking\b", "causing failures in"),
            (@"\bIt's\b", "It is"),
            (@"\bit's\b", "it is"),
            (@"\bdon't\b", "do not"),
            (@"\bDon't\b", "Do not"),
            (@"\bcan't\b", "cannot"),
            (@"\bCan't\b", "Cannot"),
            (@"\bwon't\b", "will not"),
            (@"\bWon't\b", "Will not"),
            (@"\bI'm\b", "I am"),
            (@"\bwe're\b", "we are"),
            (@"\bthey're\b", "they are"),
            (@"\byou're\b", "you are"),
            (@"\bYou're\b", "You are"),
            (@"\blet's\b", "let us"),
            (@"\bLet's\b", "Let us"),
            (@"\bshouldn't\b", "should not"),
            (@"\bwouldn't\b", "would not"),
            (@"\bcouldn't\b", "could not"),
            (@"\bisn't\b", "is not"),
            (@"\baren't\b", "are not"),
            (@"\bwasn't\b", "was not"),
            (@"\bweren't\b", "were not"),
            (@"\bhasn't\b", "has not"),
            (@"\bhaven't\b", "have not"),
        };

        private static readonly (string pattern, string replacement)[] FormalToCasual = new[]
        {
            (@"\bGreetings\b", "Hey"),
            (@"\bgreetings\b", "hey"),
            (@"\bPlease\b", ""),
            (@"\bplease\b", ""),
            (@"\bkindly\b", ""),
            (@"\bKindly\b", ""),
            (@"\bshall\b", "will"),
            (@"\bShall\b", "Will"),
            (@"\btherefore\b", "so"),
            (@"\bTherefore\b", "So"),
            (@"\bhowever\b", "but"),
            (@"\bHowever\b", "But"),
            (@"\bfurthermore\b", "also"),
            (@"\bFurthermore\b", "Also"),
            (@"\bmoreover\b", "plus"),
            (@"\bMoreover\b", "Plus"),
            (@"\bnevertheless\b", "still"),
            (@"\bNevertheless\b", "Still"),
            (@"\bin order to\b", "to"),
            (@"\bIn order to\b", "To"),
            (@"\bprior to\b", "before"),
            (@"\bsubsequently\b", "then"),
            (@"\butilize\b", "use"),
            (@"\bUtilize\b", "Use"),
            (@"\bimplement\b", "build"),
            (@"\bdo not\b", "don't"),
            (@"\bDo not\b", "Don't"),
            (@"\bcannot\b", "can't"),
            (@"\bCannot\b", "Can't"),
            (@"\bwill not\b", "won't"),
            (@"\bWill not\b", "Won't"),
            (@"\bis not\b", "isn't"),
            (@"\bIs not\b", "Isn't"),
            (@"\bare not\b", "aren't"),
        };

        private static readonly (string pattern, string replacement)[] TechnicalToSimple = new[]
        {
            (@"\bimplement\b", "build"),
            (@"\binstantiate\b", "create"),
            (@"\bparameterize\b", "set up"),
            (@"\brefactor\b", "restructure"),
            (@"\bserialize\b", "convert to text"),
            (@"\bdeserialize\b", "read from text"),
            (@"\bendpoint\b", "URL"),
            (@"\bpayload\b", "data"),
            (@"\blatency\b", "delay"),
            (@"\bthroughput\b", "speed"),
            (@"\bbandwidth\b", "capacity"),
            (@"\bconcurrency\b", "parallel processing"),
            (@"\bidempotent\b", "repeatable without side effects"),
            (@"\bboilerplate\b", "standard code"),
            (@"\bdeprecated\b", "outdated"),
            (@"\bregression\b", "a problem that came back"),
            (@"\bmutex\b", "lock"),
            (@"\bsemaphore\b", "counter-based lock"),
            (@"\bpolymorphism\b", "shape-shifting behavior"),
            (@"\binheritance\b", "building on existing code"),
            (@"\babstraction\b", "simplification layer"),
            (@"\bencapsulation\b", "data hiding"),
            (@"\bAPI\b", "interface"),
            (@"\bSDK\b", "toolkit"),
        };

        private static readonly string[] PolitenessMarkers = new[]
        {
            @"^Please\s+",
            @"^Could you (please\s+)?",
            @"^Would you (mind\s+)?",
            @"^I would like you to\s+",
            @"^I'd appreciate it if you could\s+",
            @"^If you don't mind,?\s*",
            @"^If possible,?\s*",
            @"\s*Thank you\.?\s*$",
            @"\s*Thanks\.?\s*$",
            @"\s*please\s*$",
        };

        /// <summary>
        /// Transforms a prompt to the specified target style.
        /// </summary>
        /// <param name="prompt">The prompt text to transform.</param>
        /// <param name="targetStyle">The desired output style.</param>
        /// <returns>A <see cref="StyleTransformResult"/> with the transformed text and metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompt"/> is null.</exception>
        public StyleTransformResult Transform(string prompt, PromptStyle targetStyle)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            var transformations = new List<string>();
            string result = prompt;

            result = targetStyle switch
            {
                PromptStyle.Formal => ApplyFormal(result, transformations),
                PromptStyle.Casual => ApplyCasual(result, transformations),
                PromptStyle.Technical => ApplyTechnical(result, transformations),
                PromptStyle.Simple => ApplySimple(result, transformations),
                PromptStyle.Concise => ApplyConcise(result, transformations),
                PromptStyle.Verbose => ApplyVerbose(result, transformations),
                PromptStyle.Instructional => ApplyInstructional(result, transformations),
                PromptStyle.Socratic => ApplySocratic(result, transformations),
                _ => result
            };

            result = Regex.Replace(result, @"  +", " ").Trim();

            return new StyleTransformResult
            {
                Original = prompt,
                Transformed = result,
                TargetStyle = targetStyle,
                Transformations = transformations.AsReadOnly(),
                OriginalTokens = EstimateTokens(prompt),
                TransformedTokens = EstimateTokens(result)
            };
        }

        /// <summary>
        /// Transforms a prompt through a chain of styles in sequence.
        /// </summary>
        /// <param name="prompt">The prompt text to transform.</param>
        /// <param name="styles">The styles to apply in order.</param>
        /// <returns>A <see cref="StyleTransformResult"/> reflecting the final output.</returns>
        public StyleTransformResult TransformChain(string prompt, params PromptStyle[] styles)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (styles == null || styles.Length == 0)
                return Transform(prompt, PromptStyle.Formal);

            var allTransformations = new List<string>();
            string current = prompt;

            foreach (var style in styles)
            {
                var r = Transform(current, style);
                allTransformations.AddRange(r.Transformations);
                current = r.Transformed;
            }

            return new StyleTransformResult
            {
                Original = prompt,
                Transformed = current,
                TargetStyle = styles[^1],
                Transformations = allTransformations.AsReadOnly(),
                OriginalTokens = EstimateTokens(prompt),
                TransformedTokens = EstimateTokens(current)
            };
        }

        private string ApplyFormal(string text, List<string> log)
        {
            log.Add("Expanding contractions");
            log.Add("Replacing casual vocabulary with formal equivalents");
            return ApplyReplacements(text, CasualToFormal);
        }

        private string ApplyCasual(string text, List<string> log)
        {
            log.Add("Contracting formal phrases");
            log.Add("Replacing formal vocabulary with casual equivalents");
            return ApplyReplacements(text, FormalToCasual);
        }

        private string ApplyTechnical(string text, List<string> log)
        {
            log.Add("Replacing plain language with technical terminology");
            var reversed = new (string pattern, string replacement)[TechnicalToSimple.Length];
            for (int i = 0; i < TechnicalToSimple.Length; i++)
            {
                var (pat, rep) = TechnicalToSimple[i];
                reversed[i] = ($@"\b{Regex.Escape(rep)}\b", ExtractWord(pat));
            }
            return ApplyReplacements(text, reversed, ignoreCase: true);
        }

        private string ApplySimple(string text, List<string> log)
        {
            log.Add("Replacing technical jargon with plain language");
            text = ApplyReplacements(text, TechnicalToSimple, ignoreCase: true);

            log.Add("Breaking long sentences for readability");
            text = BreakLongSentences(text, 30);

            return text;
        }

        private string ApplyConcise(string text, List<string> log)
        {
            log.Add("Removing politeness markers and filler");
            foreach (var pattern in PolitenessMarkers)
            {
                text = Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase);
            }

            log.Add("Removing filler phrases");
            var fillers = new[]
            {
                @"\bbasically\b,?\s*", @"\bactually\b,?\s*",
                @"\bjust\b\s+", @"\breally\b\s+",
                @"\bsimply\b\s+", @"\bquite\b\s+",
                @"\bperhaps\b,?\s*", @"\bmaybe\b,?\s*",
                @"\bI think\b,?\s*", @"\bI believe\b,?\s*",
                @"\bIn my opinion,?\s*", @"\bAs you know,?\s*",
                @"\bAs mentioned,?\s*", @"\bIt is worth noting that\s+",
                @"\bIt should be noted that\s+",
            };
            foreach (var f in fillers)
            {
                text = Regex.Replace(text, f, "", RegexOptions.IgnoreCase);
            }

            text = text.Trim();
            if (text.Length > 0 && char.IsLower(text[0]))
            {
                text = char.ToUpper(text[0]) + text[1..];
            }

            return text;
        }

        private string ApplyVerbose(string text, List<string> log)
        {
            log.Add("Adding instructional context");

            if (!text.StartsWith("You are", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("As a", StringComparison.OrdinalIgnoreCase))
            {
                text = "Please carefully follow these instructions. " + text;
            }

            if (!text.TrimEnd().EndsWith('.') && !text.TrimEnd().EndsWith('?') && !text.TrimEnd().EndsWith('!'))
            {
                text += ".";
            }

            text += " Please be thorough and provide detailed output.";

            return text;
        }

        private string ApplyInstructional(string text, List<string> log)
        {
            log.Add("Converting to step-by-step instructional format");

            var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (sentences.Count <= 1)
                return text;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Follow these steps:");
            for (int i = 0; i < sentences.Count; i++)
            {
                var sentence = sentences[i].Trim().TrimEnd('.');
                sb.AppendLine($"{i + 1}. {sentence}.");
            }

            return sb.ToString().TrimEnd();
        }

        private string ApplySocratic(string text, List<string> log)
        {
            log.Add("Converting directives to guiding questions");

            var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var sb = new System.Text.StringBuilder();
            foreach (var s in sentences)
            {
                var trimmed = s.Trim().TrimEnd('.').TrimEnd('!');
                if (trimmed.EndsWith('?'))
                {
                    sb.Append(trimmed + " ");
                    continue;
                }

                var question = ConvertToQuestion(trimmed);
                sb.Append(question + " ");
            }

            return sb.ToString().Trim();
        }

        private static string ApplyReplacements(string text, (string pattern, string replacement)[] rules, bool ignoreCase = false)
        {
            var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            foreach (var (pattern, replacement) in rules)
            {
                text = Regex.Replace(text, pattern, m =>
                {
                    if (replacement.Length > 0 && char.IsUpper(m.Value[0]) && char.IsLower(replacement[0]))
                        return char.ToUpper(replacement[0]) + replacement[1..];
                    return replacement;
                }, options);
            }
            return text;
        }

        private static string ExtractWord(string regexPattern)
        {
            var match = Regex.Match(regexPattern, @"\\b(\w+)\\b");
            return match.Success ? match.Groups[1].Value : regexPattern;
        }

        private static string BreakLongSentences(string text, int wordThreshold)
        {
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
            var result = new System.Text.StringBuilder();

            foreach (var sentence in sentences)
            {
                var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > wordThreshold)
                {
                    int mid = words.Length / 2;
                    for (int i = mid - 3; i <= mid + 3 && i < words.Length; i++)
                    {
                        if (i >= 0 && (words[i].EndsWith(',') || words[i].EndsWith(';') ||
                            words[i].Equals("and", StringComparison.OrdinalIgnoreCase) ||
                            words[i].Equals("but", StringComparison.OrdinalIgnoreCase) ||
                            words[i].Equals("or", StringComparison.OrdinalIgnoreCase)))
                        {
                            mid = i + 1;
                            break;
                        }
                    }

                    var first = string.Join(' ', words.Take(mid));
                    var second = string.Join(' ', words.Skip(mid));
                    if (!first.TrimEnd().EndsWith('.'))
                        first = first.TrimEnd(',', ';') + ".";
                    if (second.Length > 0)
                        second = char.ToUpper(second[0]) + second[1..];
                    result.Append(first + " " + second + " ");
                }
                else
                {
                    result.Append(sentence + " ");
                }
            }

            return result.ToString().Trim();
        }

        private static string ConvertToQuestion(string statement)
        {
            if (Regex.IsMatch(statement, @"^(Write|Create|Build|Make|Generate|Design)\b", RegexOptions.IgnoreCase))
                return "How would you " + char.ToLower(statement[0]) + statement[1..] + "?";
            if (Regex.IsMatch(statement, @"^(Explain|Describe|Define)\b", RegexOptions.IgnoreCase))
                return "Can you " + char.ToLower(statement[0]) + statement[1..] + "?";
            if (Regex.IsMatch(statement, @"^(Use|Apply|Implement)\b", RegexOptions.IgnoreCase))
                return "What happens when you " + char.ToLower(statement[0]) + statement[1..] + "?";
            if (Regex.IsMatch(statement, @"^(Find|Identify|Determine|Analyze)\b", RegexOptions.IgnoreCase))
                return "What would you find if you " + char.ToLower(statement[0]) + statement[1..] + "?";

            return "What if you were to " + char.ToLower(statement[0]) + statement[1..] + "?";
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
