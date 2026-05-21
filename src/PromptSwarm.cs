namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Strategy for reaching consensus among swarm members.
    /// </summary>
    public enum SwarmConsensusStrategy
    {
        /// <summary>Pick the response chosen by the most members.</summary>
        MajorityVote,
        /// <summary>Use weighted scoring based on member confidence.</summary>
        WeightedConfidence,
        /// <summary>Members with highest historical accuracy dominate.</summary>
        MeritBased,
        /// <summary>Require unanimous agreement or escalate.</summary>
        Unanimous,
        /// <summary>Synthesize a blended response from all members.</summary>
        Synthesis
    }

    /// <summary>
    /// Role a swarm member plays during deliberation.
    /// </summary>
    public enum SwarmRole
    {
        /// <summary>Standard contributor.</summary>
        Contributor,
        /// <summary>Devil's advocate that challenges the majority.</summary>
        Challenger,
        /// <summary>Focuses on identifying risks and edge cases.</summary>
        Critic,
        /// <summary>Optimizes for creativity and novelty.</summary>
        Innovator,
        /// <summary>Validates factual accuracy.</summary>
        FactChecker
    }

    /// <summary>
    /// A single member of the prompt swarm with its own perspective.
    /// </summary>
    public class SwarmMember
    {
        /// <summary>Gets or sets the member's unique identifier.</summary>
        public string Id { get; set; } = "";

        /// <summary>Gets or sets the role this member plays.</summary>
        public SwarmRole Role { get; set; } = SwarmRole.Contributor;

        /// <summary>Gets or sets the system prompt / persona for this member.</summary>
        public string SystemPrompt { get; set; } = "";

        /// <summary>Gets or sets the member's confidence weight (0.0-1.0).</summary>
        public double Weight { get; set; } = 1.0;

        /// <summary>Gets or sets historical accuracy score (0.0-1.0), updated by feedback.</summary>
        public double Accuracy { get; set; } = 0.5;

        /// <summary>Gets the number of rounds this member has participated in.</summary>
        public int RoundsParticipated { get; internal set; }

        /// <summary>Gets the number of times this member's response was selected.</summary>
        public int TimesSelected { get; internal set; }
    }

    /// <summary>
    /// A response from one swarm member for a deliberation round.
    /// </summary>
    public class SwarmResponse
    {
        /// <summary>Gets the member who produced this response.</summary>
        public string MemberId { get; internal set; } = "";

        /// <summary>Gets the response text.</summary>
        public string Text { get; internal set; } = "";

        /// <summary>Gets the member's self-reported confidence (0.0-1.0).</summary>
        public double Confidence { get; internal set; }

        /// <summary>Gets the reasoning behind the response.</summary>
        public string Reasoning { get; internal set; } = "";

        /// <summary>Gets the dissent notes if the member disagrees with majority.</summary>
        public string? Dissent { get; internal set; }

        /// <summary>Gets votes received from other members.</summary>
        public int Votes { get; internal set; }
    }

    /// <summary>
    /// Result of a swarm deliberation round.
    /// </summary>
    public class SwarmDeliberation
    {
        /// <summary>Gets the original query.</summary>
        public string Query { get; internal set; } = "";

        /// <summary>Gets the consensus strategy used.</summary>
        public SwarmConsensusStrategy Strategy { get; internal set; }

        /// <summary>Gets all member responses.</summary>
        public List<SwarmResponse> Responses { get; internal set; } = new();

        /// <summary>Gets the winning/consensus response.</summary>
        public SwarmResponse? Winner { get; internal set; }

        /// <summary>Gets the consensus confidence (0.0-1.0).</summary>
        public double ConsensusConfidence { get; internal set; }

        /// <summary>Gets whether consensus was reached.</summary>
        public bool ConsensusReached { get; internal set; }

        /// <summary>Gets any dissenting opinions.</summary>
        public List<SwarmResponse> Dissents { get; internal set; } = new();

        /// <summary>Gets the agreement ratio (0.0-1.0).</summary>
        public double AgreementRatio { get; internal set; }

        /// <summary>Gets the number of deliberation rounds needed.</summary>
        public int RoundsNeeded { get; internal set; } = 1;

        /// <summary>Gets the synthesized output if using Synthesis strategy.</summary>
        public string? SynthesizedOutput { get; internal set; }
    }

    /// <summary>
    /// Swarm health metrics for monitoring collective performance.
    /// </summary>
    public class SwarmHealthReport
    {
        /// <summary>Gets the total members.</summary>
        public int TotalMembers { get; internal set; }

        /// <summary>Gets the active members (participated recently).</summary>
        public int ActiveMembers { get; internal set; }

        /// <summary>Gets the average member accuracy.</summary>
        public double AverageAccuracy { get; internal set; }

        /// <summary>Gets the average consensus confidence across deliberations.</summary>
        public double AverageConsensus { get; internal set; }

        /// <summary>Gets diversity score (0=homogeneous, 1=maximally diverse roles).</summary>
        public double DiversityScore { get; internal set; }

        /// <summary>Gets the total deliberations performed.</summary>
        public int TotalDeliberations { get; internal set; }

        /// <summary>Gets the consensus success rate.</summary>
        public double ConsensusRate { get; internal set; }

        /// <summary>Gets proactive recommendations.</summary>
        public List<string> Recommendations { get; internal set; } = new();

        /// <summary>Generates a text report.</summary>
        public string ToText()
        {
            var lines = new List<string>
            {
                "=== Swarm Health Report ===",
                $"Members: {ActiveMembers}/{TotalMembers} active",
                $"Avg Accuracy: {AverageAccuracy:P1}",
                $"Avg Consensus: {AverageConsensus:P1}",
                $"Diversity: {DiversityScore:P1}",
                $"Deliberations: {TotalDeliberations}",
                $"Consensus Rate: {ConsensusRate:P1}",
                ""
            };
            if (Recommendations.Count > 0)
            {
                lines.Add("Recommendations:");
                foreach (var r in Recommendations)
                    lines.Add($"  ⚡ {r}");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Multi-prompt collaborative intelligence engine. Multiple prompt variants
    /// deliberate on a query and reach consensus via configurable strategies.
    /// Supports role-based diversity, merit tracking, and autonomous health monitoring.
    /// </summary>
    public class PromptSwarm
    {
        private readonly List<SwarmMember> _members = new();
        private readonly List<SwarmDeliberation> _history = new();
        private SwarmConsensusStrategy _strategy = SwarmConsensusStrategy.MajorityVote;
        private double _consensusThreshold = 0.6;
        private int _maxRounds = 3;
        private readonly Random _rng = new();

        /// <summary>Gets the current members.</summary>
        public IReadOnlyList<SwarmMember> Members => _members.AsReadOnly();

        /// <summary>Gets the deliberation history.</summary>
        public IReadOnlyList<SwarmDeliberation> History => _history.AsReadOnly();

        /// <summary>
        /// Sets the consensus strategy.
        /// </summary>
        public PromptSwarm WithStrategy(SwarmConsensusStrategy strategy)
        {
            _strategy = strategy;
            return this;
        }

        /// <summary>
        /// Sets the consensus threshold (0.0-1.0).
        /// </summary>
        public PromptSwarm WithThreshold(double threshold)
        {
            _consensusThreshold = Math.Clamp(threshold, 0.0, 1.0);
            return this;
        }

        /// <summary>
        /// Sets the maximum deliberation rounds before forcing a decision.
        /// </summary>
        public PromptSwarm WithMaxRounds(int rounds)
        {
            _maxRounds = Math.Max(1, rounds);
            return this;
        }

        /// <summary>
        /// Adds a member to the swarm.
        /// </summary>
        public PromptSwarm AddMember(string id, SwarmRole role, string systemPrompt, double weight = 1.0)
        {
            _members.Add(new SwarmMember
            {
                Id = id,
                Role = role,
                SystemPrompt = systemPrompt,
                Weight = Math.Clamp(weight, 0.0, 1.0)
            });
            return this;
        }

        /// <summary>
        /// Adds a preset team of 5 members with diverse roles.
        /// </summary>
        public PromptSwarm AddDefaultTeam()
        {
            AddMember("analyst", SwarmRole.Contributor, "You are a precise analytical thinker. Give clear, well-structured responses.", 1.0);
            AddMember("challenger", SwarmRole.Challenger, "You are a devil's advocate. Challenge assumptions and find weaknesses.", 0.8);
            AddMember("critic", SwarmRole.Critic, "You are a careful critic. Identify risks, edge cases, and potential problems.", 0.9);
            AddMember("creative", SwarmRole.Innovator, "You are creative and unconventional. Offer novel perspectives.", 0.7);
            AddMember("verifier", SwarmRole.FactChecker, "You are a fact-checker. Validate claims and ensure accuracy.", 0.9);
            return this;
        }

        /// <summary>
        /// Removes a member by ID.
        /// </summary>
        public bool RemoveMember(string id)
        {
            return _members.RemoveAll(m => m.Id == id) > 0;
        }

        /// <summary>
        /// Runs a deliberation round with externally provided responses.
        /// Each entry maps member ID → (responseText, confidence, reasoning).
        /// </summary>
        public SwarmDeliberation Deliberate(
            string query,
            Dictionary<string, (string Text, double Confidence, string Reasoning)> memberResponses)
        {
            if (_members.Count == 0)
                throw new InvalidOperationException("Swarm has no members. Add members before deliberating.");

            var responses = new List<SwarmResponse>();
            foreach (var member in _members)
            {
                if (memberResponses.TryGetValue(member.Id, out var resp))
                {
                    responses.Add(new SwarmResponse
                    {
                        MemberId = member.Id,
                        Text = resp.Text,
                        Confidence = Math.Clamp(resp.Confidence, 0.0, 1.0),
                        Reasoning = resp.Reasoning
                    });
                    member.RoundsParticipated++;
                }
            }

            if (responses.Count == 0)
                throw new InvalidOperationException("No matching member responses provided.");

            var deliberation = ResolveConsensus(query, responses);
            _history.Add(deliberation);
            return deliberation;
        }

        /// <summary>
        /// Simulates a deliberation using text similarity clustering to find consensus.
        /// Useful for testing without an actual LLM backend.
        /// </summary>
        public SwarmDeliberation SimulateDeliberation(string query, List<string> candidateResponses)
        {
            if (_members.Count == 0)
                throw new InvalidOperationException("Swarm has no members.");

            var responses = new List<SwarmResponse>();
            for (int i = 0; i < Math.Min(_members.Count, candidateResponses.Count); i++)
            {
                var member = _members[i];
                responses.Add(new SwarmResponse
                {
                    MemberId = member.Id,
                    Text = candidateResponses[i],
                    Confidence = 0.5 + _rng.NextDouble() * 0.5,
                    Reasoning = $"Response from {member.Role} perspective"
                });
                member.RoundsParticipated++;
            }

            var deliberation = ResolveConsensus(query, responses);
            _history.Add(deliberation);
            return deliberation;
        }

        /// <summary>
        /// Provides feedback on a deliberation, updating member accuracy scores.
        /// </summary>
        public void ProvideFeedback(SwarmDeliberation deliberation, string correctResponseId)
        {
            foreach (var resp in deliberation.Responses)
            {
                var member = _members.FirstOrDefault(m => m.Id == resp.MemberId);
                if (member == null) continue;

                bool wasCorrect = resp.MemberId == correctResponseId;
                // Exponential moving average for accuracy
                member.Accuracy = member.Accuracy * 0.8 + (wasCorrect ? 1.0 : 0.0) * 0.2;

                if (wasCorrect)
                    member.TimesSelected++;
            }
        }

        /// <summary>
        /// Generates a health report for the swarm.
        /// </summary>
        public SwarmHealthReport GetHealthReport()
        {
            var report = new SwarmHealthReport
            {
                TotalMembers = _members.Count,
                ActiveMembers = _members.Count(m => m.RoundsParticipated > 0),
                AverageAccuracy = _members.Count > 0 ? _members.Average(m => m.Accuracy) : 0,
                TotalDeliberations = _history.Count,
                AverageConsensus = _history.Count > 0 ? _history.Average(d => d.ConsensusConfidence) : 0,
                ConsensusRate = _history.Count > 0 
                    ? (double)_history.Count(d => d.ConsensusReached) / _history.Count 
                    : 0
            };

            // Diversity score: how many distinct roles are represented
            var distinctRoles = _members.Select(m => m.Role).Distinct().Count();
            var totalRoles = Enum.GetValues(typeof(SwarmRole)).Length;
            report.DiversityScore = totalRoles > 0 ? (double)distinctRoles / totalRoles : 0;

            // Proactive recommendations
            if (report.DiversityScore < 0.4)
                report.Recommendations.Add("Low role diversity — add members with Challenger, Critic, or Innovator roles for better deliberation.");

            if (report.AverageAccuracy < 0.4)
                report.Recommendations.Add("Low average accuracy — consider replacing underperforming members or switching to MeritBased strategy.");

            if (report.ConsensusRate < 0.5 && report.TotalDeliberations >= 3)
                report.Recommendations.Add("Consensus rate below 50% — try lowering the threshold or switching to WeightedConfidence strategy.");

            if (_members.Count < 3)
                report.Recommendations.Add("Fewer than 3 members — swarm intelligence works best with diverse teams of 3-7 members.");

            if (_members.Count > 7)
                report.Recommendations.Add("Large swarm (>7) — consider splitting into sub-swarms for efficiency.");

            var idleMembers = _members.Where(m => m.RoundsParticipated == 0).ToList();
            if (idleMembers.Count > 0)
                report.Recommendations.Add($"{idleMembers.Count} member(s) have never participated — ensure all members receive queries.");

            var lowPerformers = _members.Where(m => m.Accuracy < 0.3 && m.RoundsParticipated >= 5).ToList();
            if (lowPerformers.Count > 0)
                report.Recommendations.Add($"{lowPerformers.Count} member(s) with <30% accuracy — consider replacing: {string.Join(", ", lowPerformers.Select(m => m.Id))}");

            return report;
        }

        /// <summary>
        /// Exports the swarm configuration as a dictionary for serialization.
        /// </summary>
        public Dictionary<string, object> Export()
        {
            return new Dictionary<string, object>
            {
                ["strategy"] = _strategy.ToString(),
                ["threshold"] = _consensusThreshold,
                ["maxRounds"] = _maxRounds,
                ["members"] = _members.Select(m => new Dictionary<string, object>
                {
                    ["id"] = m.Id,
                    ["role"] = m.Role.ToString(),
                    ["systemPrompt"] = m.SystemPrompt,
                    ["weight"] = m.Weight,
                    ["accuracy"] = m.Accuracy,
                    ["roundsParticipated"] = m.RoundsParticipated,
                    ["timesSelected"] = m.TimesSelected
                }).ToList(),
                ["deliberationCount"] = _history.Count
            };
        }

        private SwarmDeliberation ResolveConsensus(string query, List<SwarmResponse> responses)
        {
            var deliberation = new SwarmDeliberation
            {
                Query = query,
                Strategy = _strategy,
                Responses = responses,
                RoundsNeeded = 1
            };

            switch (_strategy)
            {
                case SwarmConsensusStrategy.MajorityVote:
                    ResolveMajorityVote(deliberation);
                    break;
                case SwarmConsensusStrategy.WeightedConfidence:
                    ResolveWeightedConfidence(deliberation);
                    break;
                case SwarmConsensusStrategy.MeritBased:
                    ResolveMeritBased(deliberation);
                    break;
                case SwarmConsensusStrategy.Unanimous:
                    ResolveUnanimous(deliberation);
                    break;
                case SwarmConsensusStrategy.Synthesis:
                    ResolveSynthesis(deliberation);
                    break;
            }

            return deliberation;
        }

        private void ResolveMajorityVote(SwarmDeliberation d)
        {
            // Group by text similarity (exact match for simplicity; real impl would cluster)
            var groups = d.Responses.GroupBy(r => NormalizeForComparison(r.Text))
                .OrderByDescending(g => g.Count())
                .ToList();

            var topGroup = groups.First();
            var winner = topGroup.MaxBy(r => r.Confidence)!;
            winner.Votes = topGroup.Count();

            d.Winner = winner;
            d.AgreementRatio = (double)topGroup.Count() / d.Responses.Count;
            d.ConsensusConfidence = d.AgreementRatio * winner.Confidence;
            d.ConsensusReached = d.AgreementRatio >= _consensusThreshold;

            d.Dissents = d.Responses
                .Where(r => NormalizeForComparison(r.Text) != NormalizeForComparison(winner.Text))
                .ToList();
            foreach (var dissent in d.Dissents)
                dissent.Dissent = $"Disagreed with majority ({dissent.MemberId})";

            if (d.Winner != null)
            {
                var member = _members.FirstOrDefault(m => m.Id == d.Winner.MemberId);
                if (member != null) member.TimesSelected++;
            }
        }

        private void ResolveWeightedConfidence(SwarmDeliberation d)
        {
            // Score each response by confidence × member weight
            double bestScore = -1;
            SwarmResponse? best = null;

            foreach (var resp in d.Responses)
            {
                var member = _members.FirstOrDefault(m => m.Id == resp.MemberId);
                double weight = member?.Weight ?? 1.0;
                double score = resp.Confidence * weight;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = resp;
                }
            }

            d.Winner = best;
            double totalWeight = d.Responses.Sum(r =>
            {
                var m = _members.FirstOrDefault(mm => mm.Id == r.MemberId);
                return (m?.Weight ?? 1.0) * r.Confidence;
            });
            d.ConsensusConfidence = totalWeight > 0 ? bestScore / totalWeight : 0;
            d.AgreementRatio = d.ConsensusConfidence;
            d.ConsensusReached = d.ConsensusConfidence >= _consensusThreshold;
            d.Dissents = d.Responses.Where(r => r != best).ToList();

            if (d.Winner != null)
            {
                var member = _members.FirstOrDefault(m => m.Id == d.Winner.MemberId);
                if (member != null) member.TimesSelected++;
            }
        }

        private void ResolveMeritBased(SwarmDeliberation d)
        {
            // Weight by historical accuracy
            double bestScore = -1;
            SwarmResponse? best = null;

            foreach (var resp in d.Responses)
            {
                var member = _members.FirstOrDefault(m => m.Id == resp.MemberId);
                double accuracy = member?.Accuracy ?? 0.5;
                double score = resp.Confidence * accuracy;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = resp;
                }
            }

            d.Winner = best;
            d.ConsensusConfidence = bestScore;
            d.AgreementRatio = d.Responses.Count > 0 ? 1.0 / d.Responses.Count : 0;
            d.ConsensusReached = bestScore >= _consensusThreshold;
            d.Dissents = d.Responses.Where(r => r != best).ToList();

            if (d.Winner != null)
            {
                var member = _members.FirstOrDefault(m => m.Id == d.Winner.MemberId);
                if (member != null) member.TimesSelected++;
            }
        }

        private void ResolveUnanimous(SwarmDeliberation d)
        {
            var groups = d.Responses.GroupBy(r => NormalizeForComparison(r.Text)).ToList();
            bool unanimous = groups.Count == 1;

            var winner = d.Responses.MaxBy(r => r.Confidence)!;
            d.Winner = winner;
            d.AgreementRatio = unanimous ? 1.0 : (double)groups.Max(g => g.Count()) / d.Responses.Count;
            d.ConsensusConfidence = unanimous ? d.Responses.Average(r => r.Confidence) : 0;
            d.ConsensusReached = unanimous;

            if (!unanimous)
            {
                d.Dissents = d.Responses
                    .Where(r => NormalizeForComparison(r.Text) != NormalizeForComparison(winner.Text))
                    .ToList();
                foreach (var dissent in d.Dissents)
                    dissent.Dissent = "Unanimous consensus not reached";
            }

            if (d.Winner != null)
            {
                var member = _members.FirstOrDefault(m => m.Id == d.Winner.MemberId);
                if (member != null) member.TimesSelected++;
            }
        }

        private void ResolveSynthesis(SwarmDeliberation d)
        {
            // Combine all unique points from responses
            var allSentences = d.Responses
                .SelectMany(r => r.Text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(s => s.Length > 10)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var synthesized = string.Join(". ", allSentences);
            if (synthesized.Length > 0 && !synthesized.EndsWith('.'))
                synthesized += ".";

            d.SynthesizedOutput = synthesized;
            d.Winner = d.Responses.MaxBy(r => r.Confidence)!;
            d.AgreementRatio = 1.0; // Synthesis includes everyone
            d.ConsensusConfidence = d.Responses.Average(r => r.Confidence);
            d.ConsensusReached = true; // Synthesis always produces output
            d.Dissents = new List<SwarmResponse>(); // No dissent in synthesis

            if (d.Winner != null)
            {
                var member = _members.FirstOrDefault(m => m.Id == d.Winner.MemberId);
                if (member != null) member.TimesSelected++;
            }
        }

        private static string NormalizeForComparison(string text)
        {
            return text.Trim().ToLowerInvariant();
        }
    }
}
