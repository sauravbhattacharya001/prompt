namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Communication style for prompt transformation.
    /// </summary>
    public enum PromptStyle
    {
        /// <summary>Professional, polished tone with complete sentences.</summary>
        Formal,
        /// <summary>Relaxed, conversational tone.</summary>
        Casual,
        /// <summary>Minimal words, direct instructions.</summary>
        Concise,
        /// <summary>Detailed, explanatory style with examples.</summary>
        Verbose,
        /// <summary>Technical jargon, precise terminology.</summary>
        Technical,
        /// <summary>Step-by-step instructional format.</summary>
        Instructional,
        /// <summary>Friendly, encouraging tone.</summary>
        Friendly
    }

    /// <summary>
    /// Result of a style transfer operation.
    /// </summary>
    public class StyleTransferResult
    {
        /// <summary>Gets the original prompt text.</summary>
        public string Original { get; internal set; } = "";

        /// <summary>Gets the transformed prompt text.</summary>
        public string Transformed { get; internal set; } = "";

        /// <summary>Gets the source style (detected or specified).</summary>
        public PromptStyle SourceStyle { get; internal set; }

        /// <summary>Gets the target style applied.</summary>
        public PromptStyle TargetStyle { get; internal set; }

        /// <summary>Gets the list of transformations applied.</summary>
        public IReadOnlyList<string> Changes { get; internal set; } = Array.Empty<string>();

        /// <summary>Gets the character count difference (positive = longer).</summary>
        public int LengthDelta => Transformed.Length - Original.Length;
    }

    /// <summary>
    /// Transforms prompt text between different communication styles using
    /// heuristic, regex-based rewriting rules. No LLM calls required.
    /// </summary>
    public static class PromptStyleTransfer
    {
        private static readonly Regex PleasePat = new Regex(
            @"\b(please|kindly|if you could|would you mind)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FormalPat = new Regex(
            @"\b(hereby|therefore|furthermore|consequently|henceforth|pursuant|accordingly)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CasualPat = new Regex(
            @"\b(hey|gonna|wanna|gotta|kinda|sorta|yeah|yep|nope|cool|awesome|stuff|things)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TechPat = new Regex(
            @"\b(API|SDK|JSON|XML|HTTP|async|endpoint|parameter|schema|payload|latency|throughput)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex StepPat = new Regex(
            @"(^\s*\d+[\.\)]\s|^\s*step\s+\d+|^\s*first[,:]|^\s*then[,:]|^\s*finally[,:])",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Detects the most likely communication style of the given text.
        /// </summary>
        public static PromptStyle DetectStyle(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return PromptStyle.Concise;

            var scores = new Dictionary<PromptStyle, int>
            {
                [PromptStyle.Formal] = 0,
                [PromptStyle.Casual] = 0,
                [PromptStyle.Concise] = 0,
                [PromptStyle.Verbose] = 0,
                [PromptStyle.Technical] = 0,
                [PromptStyle.Instructional] = 0,
                [PromptStyle.Friendly] = 0
            };

            scores[PromptStyle.Formal] += FormalPat.Matches(text).Count * 3;
            scores[PromptStyle.Formal] += PleasePat.Matches(text).Count;
            scores[PromptStyle.Casual] += CasualPat.Matches(text).Count * 3;
            scores[PromptStyle.Technical] += TechPat.Matches(text).Count * 3;
            scores[PromptStyle.Instructional] += StepPat.Matches(text).Count * 3;

            var words = text.Split(new[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 15) scores[PromptStyle.Concise] += 4;
            if (words.Length > 80) scores[PromptStyle.Verbose] += 4;

            var friendlyPat = new Regex(@"\b(thanks|thank you|appreciate|great|wonderful)\b|!\s",
                RegexOptions.IgnoreCase);
            scores[PromptStyle.Friendly] += friendlyPat.Matches(text).Count * 2;

            return scores.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>
        /// Transforms the given prompt text to the target style.
        /// </summary>
        public static StyleTransferResult Transfer(string text, PromptStyle targetStyle)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var sourceStyle = DetectStyle(text);
            var changes = new List<string>();
            var result = text;

            switch (targetStyle)
            {
                case PromptStyle.Formal: result = _toFormal(result, changes); break;
                case PromptStyle.Casual: result = _toCasual(result, changes); break;
                case PromptStyle.Concise: result = _toConcise(result, changes); break;
                case PromptStyle.Verbose: result = _toVerbose(result, changes); break;
                case PromptStyle.Technical: result = _toTechnical(result, changes); break;
                case PromptStyle.Instructional: result = _toInstructional(result, changes); break;
                case PromptStyle.Friendly: result = _toFriendly(result, changes); break;
            }

            result = Regex.Replace(result, @"[ \t]{2,}", " ").Trim();

            return new StyleTransferResult
            {
                Original = text,
                Transformed = result,
                SourceStyle = sourceStyle,
                TargetStyle = targetStyle,
                Changes = changes
            };
        }

        /// <summary>Returns all available styles with descriptions.</summary>
        public static IReadOnlyDictionary<PromptStyle, string> AvailableStyles()
        {
            return new Dictionary<PromptStyle, string>
            {
                [PromptStyle.Formal] = "Professional, polished tone with complete sentences",
                [PromptStyle.Casual] = "Relaxed, conversational tone",
                [PromptStyle.Concise] = "Minimal words, direct instructions",
                [PromptStyle.Verbose] = "Detailed, explanatory style",
                [PromptStyle.Technical] = "Technical jargon, precise terminology",
                [PromptStyle.Instructional] = "Step-by-step instructional format",
                [PromptStyle.Friendly] = "Friendly, encouraging tone"
            };
        }

        private static string _toFormal(string text, List<string> changes)
        {
            var r = text;
            var contractions = new (string pat, string rep)[]
            {
                (@"\bdon't\b", "do not"), (@"\bcan't\b", "cannot"),
                (@"\bwon't\b", "will not"), (@"\bit's\b", "it is"),
                (@"\bI'm\b", "I am"), (@"\bwe're\b", "we are"),
                (@"\bthey're\b", "they are"), (@"\byou're\b", "you are"),
                (@"\bI've\b", "I have"), (@"\bwe've\b", "we have"),
                (@"\bdidn't\b", "did not"), (@"\bisn't\b", "is not"),
                (@"\baren't\b", "are not"), (@"\bwasn't\b", "was not"),
                (@"\bweren't\b", "were not"), (@"\bcouldn't\b", "could not"),
                (@"\bshouldn't\b", "should not"), (@"\bwouldn't\b", "would not"),
            };
            foreach (var (pat, rep) in contractions)
            {
                var prev = r;
                r = Regex.Replace(r, pat, rep, RegexOptions.IgnoreCase);
                if (r != prev && !changes.Contains("Expanded contractions"))
                    changes.Add("Expanded contractions");
            }

            var replacements = new (string pat, string rep)[]
            {
                (@"\bhey\b", "greetings"), (@"\byeah\b", "yes"),
                (@"\bnope\b", "no"), (@"\bgonna\b", "going to"),
                (@"\bwanna\b", "want to"), (@"\bgotta\b", "have to"),
                (@"\bkinda\b", "somewhat"), (@"\bsorta\b", "somewhat"),
                (@"\bcool\b", "acceptable"), (@"\bawesome\b", "excellent"),
                (@"\bstuff\b", "material"), (@"\bthings\b", "items"),
            };
            foreach (var (pat, rep) in replacements)
            {
                var prev = r;
                r = Regex.Replace(r, pat, rep, RegexOptions.IgnoreCase);
                if (r != prev && !changes.Contains("Replaced casual term with formal equivalent"))
                    changes.Add("Replaced casual term with formal equivalent");
            }

            if (!PleasePat.IsMatch(r) && r.Length > 0 && char.IsUpper(r[0]))
            {
                r = "Please " + char.ToLower(r[0]) + r.Substring(1);
                changes.Add("Added politeness marker");
            }

            return r;
        }

        private static string _toCasual(string text, List<string> changes)
        {
            var r = text;
            var contractions = new (string pat, string rep)[]
            {
                (@"\bdo not\b", "don't"), (@"\bcannot\b", "can't"),
                (@"\bwill not\b", "won't"), (@"\bit is\b", "it's"),
                (@"\bI am\b", "I'm"), (@"\bwe are\b", "we're"),
                (@"\bthey are\b", "they're"), (@"\byou are\b", "you're"),
                (@"\bdid not\b", "didn't"), (@"\bis not\b", "isn't"),
                (@"\bare not\b", "aren't"),
            };
            foreach (var (pat, rep) in contractions)
            {
                var prev = r;
                r = Regex.Replace(r, pat, rep, RegexOptions.IgnoreCase);
                if (r != prev && !changes.Contains("Used contractions"))
                    changes.Add("Used contractions");
            }

            var formalWords = new (string pat, string rep)[]
            {
                (@"\bfurthermore\b", "also"), (@"\bconsequently\b", "so"),
                (@"\btherefore\b", "so"), (@"\baccordingly\b", "so"),
                (@"\bhenceforth\b", "from now on"), (@"\bhereby\b", ""),
                (@"\bpursuant to\b", "based on"), (@"\butilize\b", "use"),
                (@"\bfacilitate\b", "help"), (@"\bcommence\b", "start"),
                (@"\bterminate\b", "end"), (@"\bimplement\b", "set up"),
            };
            foreach (var (pat, rep) in formalWords)
            {
                var prev = r;
                r = Regex.Replace(r, pat, rep, RegexOptions.IgnoreCase);
                if (r != prev && !changes.Contains("Simplified formal vocabulary"))
                    changes.Add("Simplified formal vocabulary");
            }

            var prev2 = r;
            r = Regex.Replace(r, @"^Please\s+", "", RegexOptions.IgnoreCase);
            if (r != prev2) changes.Add("Removed politeness prefix");

            return r;
        }

        private static string _toConcise(string text, List<string> changes)
        {
            var r = text;
            var fillers = new string[]
            {
                @"\bI would like you to\b", @"\bCould you please\b",
                @"\bPlease\b", @"\bKindly\b", @"\bif you could\b",
                @"\bwould you mind\b", @"\bI think that\b",
                @"\bIt seems like\b", @"\bIn my opinion\b",
                @"\bBasically\b", @"\bEssentially\b",
                @"\bAs a matter of fact\b",
                @"\bIt is important to note that\b",
                @"\bIt should be noted that\b",
                @"\bIn order to\b",
            };
            foreach (var pat in fillers)
            {
                var prev = r;
                r = Regex.Replace(r, pat + @"\s*", "", RegexOptions.IgnoreCase);
                if (r != prev && !changes.Contains("Removed filler phrases"))
                    changes.Add("Removed filler phrases");
            }

            var prev3 = r;
            r = Regex.Replace(r, @"\s*(Thank you|Thanks)[\.\!]?\s*$", "", RegexOptions.IgnoreCase);
            if (r != prev3) changes.Add("Removed trailing pleasantry");

            if (r.Length > 0 && char.IsLower(r[0]))
            {
                r = char.ToUpper(r[0]) + r.Substring(1);
                changes.Add("Capitalized start");
            }

            return r;
        }

        private static string _toVerbose(string text, List<string> changes)
        {
            var r = text;
            if (!r.TrimStart().StartsWith("I would like", StringComparison.OrdinalIgnoreCase) &&
                !r.TrimStart().StartsWith("Please", StringComparison.OrdinalIgnoreCase) &&
                !r.TrimStart().StartsWith("Could", StringComparison.OrdinalIgnoreCase))
            {
                r = "I would like you to " + char.ToLower(r[0]) + r.Substring(1);
                changes.Add("Added request framing");
            }
            if (!r.Contains("detail") && !r.Contains("explain") && !r.Contains("elaborate"))
            {
                r = r.TrimEnd('.', ' ') + ". Please provide detailed explanations where applicable.";
                changes.Add("Added detail request");
            }
            if (r.Length > 0 && !r.EndsWith(".") && !r.EndsWith("!") && !r.EndsWith("?"))
                r += ".";
            return r;
        }

        private static string _toTechnical(string text, List<string> changes)
        {
            var r = text;
            var techTerms = new (string pat, string rep)[]
            {
                (@"\bsend back\b", "return"), (@"\bgive me\b", "output"),
                (@"\bshow me\b", "display"), (@"\bbreak down\b", "decompose"),
                (@"\bset up\b", "configure"), (@"\bput together\b", "compose"),
                (@"\bspeed up\b", "optimize"), (@"\bslow down\b", "throttle"),
                (@"\bcheck\b", "validate"), (@"\bfix\b", "remediate"),
                (@"\berror\b", "exception"), (@"\blist\b", "enumerate"),
            };
            foreach (var (pat, rep) in techTerms)
            {
                var prev = r;
                r = Regex.Replace(r, pat, rep, RegexOptions.IgnoreCase);
                if (r != prev && !changes.Contains("Used precise technical terminology"))
                    changes.Add("Used precise technical terminology");
            }
            if (!r.Contains("shall") && !r.Contains("must") && r.Length > 0 && char.IsUpper(r[0]))
            {
                r = "The system shall " + char.ToLower(r[0]) + r.Substring(1);
                changes.Add("Added specification framing");
            }
            return r;
        }

        private static string _toInstructional(string text, List<string> changes)
        {
            var r = text;
            var sentences = Regex.Split(r, @"(?<=[\.!\?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (sentences.Length > 1)
            {
                r = string.Join("\n", sentences.Select((s, i) => $"{i + 1}. {s.TrimStart()}"));
                changes.Add("Converted to numbered steps");
            }
            else if (sentences.Length == 1)
            {
                var parts = Regex.Split(r, @",\s+(?:and\s+|then\s+)?|(?:\s+and\s+|\s+then\s+)")
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (parts.Length > 1)
                {
                    r = string.Join("\n", parts.Select((s, i) =>
                    {
                        var clean = s.Trim().TrimEnd('.');
                        return $"{i + 1}. {char.ToUpper(clean[0])}{clean.Substring(1)}.";
                    }));
                    changes.Add("Split compound sentence into numbered steps");
                }
            }
            return r;
        }

        private static string _toFriendly(string text, List<string> changes)
        {
            var r = text;
            if (!Regex.IsMatch(r, @"^(hey|hi|hello|thanks|great)", RegexOptions.IgnoreCase))
            {
                r = "Hey! " + r;
                changes.Add("Added friendly greeting");
            }
            var softeners = new (string pat, string rep)[]
            {
                (@"\bYou must\b", "It would be great if you could"),
                (@"\bDo not\b", "Try to avoid"),
                (@"\bDon't\b", "Try not to"),
                (@"\bNever\b", "It's best not to"),
                (@"\bFailure to\b", "If you happen to miss"),
            };
            foreach (var (pat, rep) in softeners)
            {
                var prev = r;
                r = Regex.Replace(r, pat, rep);
                if (r != prev && !changes.Contains("Softened directive language"))
                    changes.Add("Softened directive language");
            }
            if (!Regex.IsMatch(r, @"(thanks|thank you|appreciate|!\s*$)", RegexOptions.IgnoreCase))
            {
                r = r.TrimEnd('.', ' ') + ". Thanks so much! \U0001f60a";
                changes.Add("Added encouraging closer");
            }
            return r;
        }
    }
}
