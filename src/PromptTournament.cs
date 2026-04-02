namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Evaluation criteria for comparing prompts.
    /// </summary>
    public enum TournamentCriterion
    {
        /// <summary>How clear and unambiguous the prompt is.</summary>
        Clarity,
        /// <summary>How concise the prompt is (fewer tokens = better).</summary>
        Conciseness,
        /// <summary>How specific and detailed the instructions are.</summary>
        Specificity,
        /// <summary>How well the prompt constrains output format.</summary>
        OutputControl,
        /// <summary>How robust against prompt injection.</summary>
        Safety,
        /// <summary>Overall readability score.</summary>
        Readability,
        /// <summary>Custom user-defined criterion.</summary>
        Custom
    }

    /// <summary>
    /// Weighted criterion for tournament evaluation.
    /// </summary>
    public record TournamentWeight(
        TournamentCriterion Criterion,
        double Weight = 1.0,
        string? CustomName = null
    )
    {
        /// <summary>Display name for this criterion.</summary>
        public string DisplayName => Criterion == TournamentCriterion.Custom
            ? CustomName ?? "Custom"
            : Criterion.ToString();
    }

    /// <summary>
    /// Score for a single criterion evaluation.
    /// </summary>
    public record CriterionScore(
        TournamentCriterion Criterion,
        double Score,
        string? Reasoning = null,
        string? CustomName = null
    );

    /// <summary>
    /// Result of a head-to-head match between two prompts.
    /// </summary>
    public class MatchResult
    {
        /// <summary>Index of the first contender.</summary>
        public int ContenderA { get; init; }

        /// <summary>Index of the second contender.</summary>
        public int ContenderB { get; init; }

        /// <summary>Index of the winner.</summary>
        public int Winner { get; init; }

        /// <summary>Score breakdown for contender A.</summary>
        public List<CriterionScore> ScoresA { get; init; } = new();

        /// <summary>Score breakdown for contender B.</summary>
        public List<CriterionScore> ScoresB { get; init; } = new();

        /// <summary>Weighted total for A.</summary>
        public double TotalA { get; init; }

        /// <summary>Weighted total for B.</summary>
        public double TotalB { get; init; }

        /// <summary>Round number in the tournament bracket.</summary>
        public int Round { get; init; }
    }

    /// <summary>
    /// A contender in the tournament with accumulated stats.
    /// </summary>
    public class TournamentContender
    {
        /// <summary>Original prompt text.</summary>
        public string Prompt { get; init; } = "";

        /// <summary>Optional label for display.</summary>
        public string? Label { get; init; }

        /// <summary>Index in the original contender list.</summary>
        public int Index { get; init; }

        /// <summary>Number of wins.</summary>
        public int Wins { get; set; }

        /// <summary>Number of losses.</summary>
        public int Losses { get; set; }

        /// <summary>Cumulative weighted score across all matches.</summary>
        public double CumulativeScore { get; set; }

        /// <summary>All criterion scores received.</summary>
        public List<CriterionScore> AllScores { get; } = new();

        /// <summary>Display name (label or truncated prompt).</summary>
        public string DisplayName => Label ?? (Prompt.Length <= 40 ? Prompt : Prompt[..37] + "...");
    }

    /// <summary>
    /// Final tournament results with rankings and bracket history.
    /// </summary>
    public class TournamentResult
    {
        /// <summary>Ordered rankings (best first).</summary>
        public List<TournamentContender> Rankings { get; init; } = new();

        /// <summary>All match results in order.</summary>
        public List<MatchResult> Matches { get; init; } = new();

        /// <summary>The winning prompt.</summary>
        public TournamentContender? Champion => Rankings.FirstOrDefault();

        /// <summary>Number of rounds played.</summary>
        public int TotalRounds { get; init; }

        /// <summary>Criteria used for evaluation.</summary>
        public List<TournamentWeight> Criteria { get; init; } = new();

        /// <summary>
        /// Renders a human-readable bracket summary.
        /// </summary>
        public string RenderBracket()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════╗");
            sb.AppendLine("║        PROMPT TOURNAMENT             ║");
            sb.AppendLine("╠══════════════════════════════════════╣");

            var byRound = Matches.GroupBy(m => m.Round).OrderBy(g => g.Key);
            foreach (var round in byRound)
            {
                sb.AppendLine($"║  Round {round.Key}                            ║");
                sb.AppendLine("║──────────────────────────────────────║");
                foreach (var match in round)
                {
                    var a = Rankings.FirstOrDefault(r => r.Index == match.ContenderA);
                    var b = Rankings.FirstOrDefault(r => r.Index == match.ContenderB);
                    var winner = match.Winner == match.ContenderA ? "A" : "B";
                    sb.AppendLine($"║  {Truncate(a?.DisplayName ?? "?", 14)} vs {Truncate(b?.DisplayName ?? "?", 14)} → {winner} ║");
                }
            }

            sb.AppendLine("╠══════════════════════════════════════╣");
            sb.AppendLine($"║  🏆 Champion: {Truncate(Champion?.DisplayName ?? "N/A", 22)} ║");
            sb.AppendLine("╚══════════════════════════════════════╝");

            sb.AppendLine();
            sb.AppendLine("Final Rankings:");
            for (int i = 0; i < Rankings.Count; i++)
            {
                var r = Rankings[i];
                var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"#{i + 1}" };
                sb.AppendLine($"  {medal} {r.DisplayName} (W:{r.Wins} L:{r.Losses} Score:{r.CumulativeScore:F1})");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports results as JSON.
        /// </summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s.PadRight(max) : s[..(max - 2)] + "..";
    }

    /// <summary>
    /// Runs prompt tournaments — elimination brackets or round-robin competitions
    /// that rank prompt variants by weighted criteria scoring.
    /// 
    /// Usage:
    ///   var tournament = new PromptTournament()
    ///       .AddContender("Summarize this article in 3 bullet points.", "Bullets")
    ///       .AddContender("Give me a brief summary of the article.", "Brief")
    ///       .AddContender("Extract the key insights from this text.", "Insights")
    ///       .WithCriterion(TournamentCriterion.Clarity, 2.0)
    ///       .WithCriterion(TournamentCriterion.Conciseness, 1.5)
    ///       .WithCriterion(TournamentCriterion.Specificity, 1.0);
    ///   
    ///   // Run with built-in heuristic scoring
    ///   var result = tournament.RunRoundRobin();
    ///   Console.WriteLine(result.RenderBracket());
    ///   
    ///   // Or run elimination bracket
    ///   var result2 = tournament.RunElimination();
    ///   
    ///   // Or provide custom scoring
    ///   var result3 = tournament.RunRoundRobin((prompt, criterion) => myCustomScore);
    /// </summary>
    public class PromptTournament
    {
        private readonly List<TournamentContender> _contenders = new();
        private readonly List<TournamentWeight> _criteria = new();
        private Random _rng = new(42);

        /// <summary>
        /// Adds a prompt contender to the tournament.
        /// </summary>
        public PromptTournament AddContender(string prompt, string? label = null)
        {
            _contenders.Add(new TournamentContender
            {
                Prompt = prompt,
                Label = label,
                Index = _contenders.Count
            });
            return this;
        }

        /// <summary>
        /// Adds an evaluation criterion with weight.
        /// </summary>
        public PromptTournament WithCriterion(TournamentCriterion criterion, double weight = 1.0, string? customName = null)
        {
            _criteria.Add(new TournamentWeight(criterion, weight, customName));
            return this;
        }

        /// <summary>
        /// Sets the random seed for reproducible tournaments.
        /// </summary>
        public PromptTournament WithSeed(int seed)
        {
            _rng = new Random(seed);
            return this;
        }

        /// <summary>
        /// Runs a round-robin tournament where every contender faces every other.
        /// Uses built-in heuristic scoring or a custom scorer function.
        /// </summary>
        /// <param name="customScorer">Optional: (prompt, criterion) → score (0-10).</param>
        public TournamentResult RunRoundRobin(Func<string, TournamentCriterion, double>? customScorer = null)
        {
            ValidateSetup();
            var scorer = customScorer ?? DefaultScore;
            var matches = new List<MatchResult>();
            int round = 1;

            for (int i = 0; i < _contenders.Count; i++)
            {
                for (int j = i + 1; j < _contenders.Count; j++)
                {
                    var match = RunMatch(_contenders[i], _contenders[j], round, scorer);
                    matches.Add(match);
                }
                round++;
            }

            return BuildResult(matches, round - 1);
        }

        /// <summary>
        /// Runs a single-elimination bracket tournament.
        /// Contenders are seeded by their initial order.
        /// </summary>
        /// <param name="customScorer">Optional: (prompt, criterion) → score (0-10).</param>
        public TournamentResult RunElimination(Func<string, TournamentCriterion, double>? customScorer = null)
        {
            ValidateSetup();
            var scorer = customScorer ?? DefaultScore;
            var matches = new List<MatchResult>();

            // Pad to power of 2 with byes
            var bracket = new List<TournamentContender>(_contenders);
            var shuffled = bracket.OrderBy(_ => _rng.Next()).ToList();

            int round = 1;
            var currentRound = new List<TournamentContender>(shuffled);

            while (currentRound.Count > 1)
            {
                var nextRound = new List<TournamentContender>();

                for (int i = 0; i < currentRound.Count; i += 2)
                {
                    if (i + 1 >= currentRound.Count)
                    {
                        // Bye — auto-advance
                        nextRound.Add(currentRound[i]);
                        continue;
                    }

                    var match = RunMatch(currentRound[i], currentRound[i + 1], round, scorer);
                    matches.Add(match);

                    var winner = match.Winner == currentRound[i].Index
                        ? currentRound[i]
                        : currentRound[i + 1];
                    nextRound.Add(winner);
                }

                currentRound = nextRound;
                round++;
            }

            return BuildResult(matches, round - 1);
        }

        private MatchResult RunMatch(
            TournamentContender a,
            TournamentContender b,
            int round,
            Func<string, TournamentCriterion, double> scorer)
        {
            var scoresA = _criteria.Select(c => new CriterionScore(
                c.Criterion,
                scorer(a.Prompt, c.Criterion),
                null,
                c.CustomName
            )).ToList();

            var scoresB = _criteria.Select(c => new CriterionScore(
                c.Criterion,
                scorer(b.Prompt, c.Criterion),
                null,
                c.CustomName
            )).ToList();

            double totalA = _criteria.Zip(scoresA, (w, s) => w.Weight * s.Score).Sum();
            double totalB = _criteria.Zip(scoresB, (w, s) => w.Weight * s.Score).Sum();

            bool aWins = totalA >= totalB;
            if (aWins) { a.Wins++; b.Losses++; }
            else { b.Wins++; a.Losses++; }

            a.CumulativeScore += totalA;
            b.CumulativeScore += totalB;
            a.AllScores.AddRange(scoresA);
            b.AllScores.AddRange(scoresB);

            return new MatchResult
            {
                ContenderA = a.Index,
                ContenderB = b.Index,
                Winner = aWins ? a.Index : b.Index,
                ScoresA = scoresA,
                ScoresB = scoresB,
                TotalA = totalA,
                TotalB = totalB,
                Round = round
            };
        }

        private TournamentResult BuildResult(List<MatchResult> matches, int totalRounds)
        {
            var rankings = _contenders
                .OrderByDescending(c => c.Wins)
                .ThenByDescending(c => c.CumulativeScore)
                .ToList();

            return new TournamentResult
            {
                Rankings = rankings,
                Matches = matches,
                TotalRounds = totalRounds,
                Criteria = new List<TournamentWeight>(_criteria)
            };
        }

        private void ValidateSetup()
        {
            if (_contenders.Count < 2)
                throw new InvalidOperationException("Tournament requires at least 2 contenders.");
            if (_criteria.Count == 0)
            {
                // Default criteria
                WithCriterion(TournamentCriterion.Clarity);
                WithCriterion(TournamentCriterion.Conciseness);
                WithCriterion(TournamentCriterion.Specificity);
            }
        }

        /// <summary>
        /// Built-in heuristic scorer based on prompt text analysis.
        /// Returns a score from 0 to 10.
        /// </summary>
        private double DefaultScore(string prompt, TournamentCriterion criterion)
        {
            return criterion switch
            {
                TournamentCriterion.Clarity => ScoreClarity(prompt),
                TournamentCriterion.Conciseness => ScoreConciseness(prompt),
                TournamentCriterion.Specificity => ScoreSpecificity(prompt),
                TournamentCriterion.OutputControl => ScoreOutputControl(prompt),
                TournamentCriterion.Safety => ScoreSafety(prompt),
                TournamentCriterion.Readability => ScoreReadability(prompt),
                _ => 5.0 // Custom criteria need a custom scorer
            };
        }

        private double ScoreClarity(string prompt)
        {
            double score = 5.0;
            // Reward: clear sentence structure (periods, proper length)
            var sentences = prompt.Split('.', '!', '?').Where(s => s.Trim().Length > 0).ToList();
            if (sentences.Count >= 1) score += 1.0;
            if (sentences.All(s => s.Trim().Split(' ').Length <= 25)) score += 1.0; // Not overly long
            // Penalize: excessive parentheses, nested clauses
            int parenCount = prompt.Count(c => c == '(');
            if (parenCount > 2) score -= 1.0;
            // Reward: imperative mood (starts with verb-like words)
            var starters = new[] { "summarize", "list", "explain", "describe", "analyze", "write", "create", "generate", "extract", "translate", "compare", "identify", "provide", "give", "tell", "show", "find" };
            if (starters.Any(s => prompt.TrimStart().StartsWith(s, StringComparison.OrdinalIgnoreCase))) score += 1.5;
            // Penalize ambiguity markers
            var ambiguous = new[] { "maybe", "perhaps", "somehow", "something like", "sort of", "kind of" };
            foreach (var a in ambiguous)
                if (prompt.Contains(a, StringComparison.OrdinalIgnoreCase)) score -= 0.5;

            return Math.Clamp(score, 0, 10);
        }

        private double ScoreConciseness(string prompt)
        {
            int words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            // Sweet spot: 10-30 words
            if (words <= 5) return 6.0;
            if (words <= 15) return 9.0;
            if (words <= 30) return 8.0;
            if (words <= 60) return 6.0;
            if (words <= 100) return 4.0;
            return 2.0;
        }

        private double ScoreSpecificity(string prompt)
        {
            double score = 4.0;
            // Reward: numbers, specific formats
            if (System.Text.RegularExpressions.Regex.IsMatch(prompt, @"\d+")) score += 1.5;
            // Reward: format specifiers
            var formats = new[] { "json", "csv", "markdown", "bullet", "table", "xml", "yaml", "list", "paragraph" };
            if (formats.Any(f => prompt.Contains(f, StringComparison.OrdinalIgnoreCase))) score += 2.0;
            // Reward: constraints
            var constraints = new[] { "must", "should", "exactly", "only", "no more than", "at least", "between", "maximum", "minimum" };
            foreach (var c in constraints)
                if (prompt.Contains(c, StringComparison.OrdinalIgnoreCase)) score += 0.5;
            // Reward: examples
            if (prompt.Contains("example", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("e.g.", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("such as", StringComparison.OrdinalIgnoreCase))
                score += 1.0;

            return Math.Clamp(score, 0, 10);
        }

        private double ScoreOutputControl(string prompt)
        {
            double score = 3.0;
            // Format specification
            var formats = new[] { "json", "csv", "markdown", "xml", "yaml", "html", "table", "bullet points", "numbered list" };
            foreach (var f in formats)
                if (prompt.Contains(f, StringComparison.OrdinalIgnoreCase)) score += 2.0;
            // Length constraints
            if (System.Text.RegularExpressions.Regex.IsMatch(prompt, @"\d+\s*(word|sentence|paragraph|line|item|bullet|point)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                score += 2.0;
            // Structure keywords
            var structure = new[] { "format", "structure", "template", "schema", "layout" };
            if (structure.Any(s => prompt.Contains(s, StringComparison.OrdinalIgnoreCase))) score += 1.5;

            return Math.Clamp(score, 0, 10);
        }

        private double ScoreSafety(string prompt)
        {
            double score = 7.0;
            // Reward: safety guardrails
            var guardrails = new[] { "do not", "don't", "never", "avoid", "refuse", "safe", "appropriate", "responsible" };
            foreach (var g in guardrails)
                if (prompt.Contains(g, StringComparison.OrdinalIgnoreCase)) score += 0.5;
            // Reward: role boundaries
            if (prompt.Contains("you are", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("act as", StringComparison.OrdinalIgnoreCase))
                score += 1.0;
            // Penalize: instruction override patterns
            var risky = new[] { "ignore previous", "disregard", "bypass", "jailbreak", "pretend you have no" };
            foreach (var r in risky)
                if (prompt.Contains(r, StringComparison.OrdinalIgnoreCase)) score -= 2.0;

            return Math.Clamp(score, 0, 10);
        }

        private double ScoreReadability(string prompt)
        {
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0;

            double avgWordLen = words.Average(w => w.Length);
            var sentences = prompt.Split('.', '!', '?').Where(s => s.Trim().Length > 0).ToList();
            double avgSentLen = sentences.Count > 0 ? words.Length / (double)sentences.Count : words.Length;

            double score = 7.0;
            // Penalize very long words (jargon)
            if (avgWordLen > 7) score -= 1.5;
            // Penalize very long sentences
            if (avgSentLen > 25) score -= 1.5;
            if (avgSentLen > 40) score -= 1.5;
            // Reward moderate sentence length
            if (avgSentLen >= 8 && avgSentLen <= 20) score += 1.5;
            // Reward line breaks / structure
            if (prompt.Contains('\n')) score += 1.0;

            return Math.Clamp(score, 0, 10);
        }
    }
}
