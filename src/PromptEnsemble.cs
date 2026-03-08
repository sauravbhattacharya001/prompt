namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>Strategy for aggregating ensemble results.</summary>
    public enum EnsembleStrategy
    {
        /// <summary>Pick the response that appears most frequently.</summary>
        MajorityVote,
        /// <summary>Pick the response with the highest score.</summary>
        BestOfN,
        /// <summary>Return responses that agree; flag disagreements.</summary>
        Consensus,
        /// <summary>Pick a random response.</summary>
        Random,
        /// <summary>Use a custom aggregation function.</summary>
        Custom
    }

    /// <summary>Fuzzy matching mode for majority vote comparison.</summary>
    public enum FuzzyMatchMode
    {
        /// <summary>Exact string comparison.</summary>
        Exact,
        /// <summary>Case-insensitive, whitespace-normalized comparison.</summary>
        Normalized,
        /// <summary>Jaccard similarity on word tokens above a threshold.</summary>
        Jaccard
    }

    /// <summary>A single ensemble member configuration.</summary>
    public class EnsembleMember
    {
        /// <summary>Label for this member.</summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>The prompt text to send.</summary>
        public string Prompt { get; set; } = string.Empty;
        /// <summary>Optional model identifier.</summary>
        public string? Model { get; set; }
        /// <summary>Optional temperature override.</summary>
        public double? Temperature { get; set; }
        /// <summary>Optional metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>Configuration for an ensemble run.</summary>
    public class EnsembleConfig
    {
        /// <summary>Members to run.</summary>
        public List<EnsembleMember> Members { get; set; } = new();
        /// <summary>Aggregation strategy.</summary>
        public EnsembleStrategy Strategy { get; set; } = EnsembleStrategy.MajorityVote;
        /// <summary>Fuzzy match mode for MajorityVote.</summary>
        public FuzzyMatchMode MatchMode { get; set; } = FuzzyMatchMode.Normalized;
        /// <summary>Jaccard threshold (0.0-1.0).</summary>
        public double JaccardThreshold { get; set; } = 0.7;
        /// <summary>Consensus threshold (0.0-1.0).</summary>
        public double ConsensusThreshold { get; set; } = 0.5;
        /// <summary>Scoring function for BestOfN.</summary>
        [JsonIgnore] public Func<string, double>? Scorer { get; set; }
        /// <summary>Custom aggregation function.</summary>
        [JsonIgnore] public Func<List<EnsembleMemberResult>, EnsembleAggregation>? CustomAggregator { get; set; }

        /// <summary>N copies of same prompt (self-consistency).</summary>
        public static EnsembleConfig SelfConsistency(string prompt, int n, EnsembleStrategy strategy = EnsembleStrategy.MajorityVote)
        {
            if (n < 2) throw new ArgumentException("Need at least 2 members.", nameof(n));
            var c = new EnsembleConfig { Strategy = strategy };
            for (int i = 0; i < n; i++) c.Members.Add(new EnsembleMember { Label = $"run-{i + 1}", Prompt = prompt });
            return c;
        }

        /// <summary>Same prompt across multiple models.</summary>
        public static EnsembleConfig CrossModel(string prompt, params string[] models)
        {
            if (models.Length < 2) throw new ArgumentException("Need at least 2 models.", nameof(models));
            var c = new EnsembleConfig { Strategy = EnsembleStrategy.Consensus };
            foreach (var m in models) c.Members.Add(new EnsembleMember { Label = m, Prompt = prompt, Model = m });
            return c;
        }

        /// <summary>Multiple prompt variants (A/B testing).</summary>
        public static EnsembleConfig PromptVariants(params (string label, string prompt)[] variants)
        {
            if (variants.Length < 2) throw new ArgumentException("Need at least 2 variants.", nameof(variants));
            var c = new EnsembleConfig { Strategy = EnsembleStrategy.BestOfN };
            foreach (var (label, prompt) in variants) c.Members.Add(new EnsembleMember { Label = label, Prompt = prompt });
            return c;
        }
    }

    /// <summary>Result from a single ensemble member.</summary>
    public class EnsembleMemberResult
    {
        public string Label { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public double? Score { get; set; }
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public int ClusterIndex { get; set; } = -1;
    }

    /// <summary>Aggregated ensemble result.</summary>
    public class EnsembleAggregation
    {
        public string SelectedResponse { get; set; } = string.Empty;
        public string WinnerLabel { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public bool ConsensusReached { get; set; }
        public int ClusterCount { get; set; }
        public EnsembleStrategy Strategy { get; set; }
        public List<EnsembleMemberResult> Members { get; set; } = new();
        public Dictionary<int, List<string>> Clusters { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        /// <summary>Summary line.</summary>
        public string Summary => $"Ensemble ({Strategy}): winner=\"{WinnerLabel}\" confidence={Confidence:P0} clusters={ClusterCount} members={Members.Count}";
    }

    /// <summary>
    /// Runs multiple prompt configurations and aggregates results via majority vote,
    /// best-of-N scoring, consensus detection, or custom logic. Transport-agnostic:
    /// you provide responses, it aggregates.
    /// </summary>
    /// <example>
    /// <code>
    /// var ensemble = new PromptEnsemble(EnsembleConfig.SelfConsistency("What is 2+2?", 5));
    /// ensemble.AddResponses("4", "4", "4", "3", "4");
    /// var result = ensemble.Aggregate();
    /// // result.SelectedResponse == "4", result.Confidence == 0.8
    /// </code>
    /// </example>
    public class PromptEnsemble
    {
        private readonly EnsembleConfig _config;
        private readonly List<EnsembleMemberResult> _results = new();
        private readonly Random _rng;

        public PromptEnsemble(EnsembleConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (_config.Members.Count < 2) throw new ArgumentException("Ensemble requires at least 2 members.");
            _rng = new Random();
        }

        public PromptEnsemble(EnsembleConfig config, int seed) : this(config) { _rng = new Random(seed); }

        public EnsembleConfig Config => _config;
        public IReadOnlyList<EnsembleMemberResult> Results => _results.AsReadOnly();
        public int ResponseCount => _results.Count;
        public bool IsComplete => _results.Count >= _config.Members.Count;

        public void AddResponse(string label, string response, TimeSpan? duration = null)
        {
            _ = _config.Members.FirstOrDefault(m => m.Label == label) ?? throw new ArgumentException($"No member '{label}'.");
            if (_results.Any(r => r.Label == label)) throw new InvalidOperationException($"'{label}' already responded.");
            var result = new EnsembleMemberResult { Label = label, Response = response, Duration = duration ?? TimeSpan.Zero };
            if (_config.Scorer != null) { try { result.Score = _config.Scorer(response); } catch { result.Score = 0; } }
            _results.Add(result);
        }

        public void AddError(string label, string errorMessage, TimeSpan? duration = null)
        {
            _ = _config.Members.FirstOrDefault(m => m.Label == label) ?? throw new ArgumentException($"No member '{label}'.");
            if (_results.Any(r => r.Label == label)) throw new InvalidOperationException($"'{label}' already responded.");
            _results.Add(new EnsembleMemberResult { Label = label, IsError = true, ErrorMessage = errorMessage, Duration = duration ?? TimeSpan.Zero });
        }

        public void AddResponses(params string[] responses)
        {
            if (responses.Length > _config.Members.Count) throw new ArgumentException("More responses than members.");
            for (int i = 0; i < responses.Length; i++)
            {
                var label = _config.Members[i].Label;
                if (!_results.Any(r => r.Label == label)) AddResponse(label, responses[i]);
            }
        }

        public EnsembleAggregation Aggregate()
        {
            var valid = _results.Where(r => !r.IsError).ToList();
            if (valid.Count == 0) throw new InvalidOperationException("No valid responses to aggregate.");
            var agg = _config.Strategy switch
            {
                EnsembleStrategy.MajorityVote => AggregateMajorityVote(valid),
                EnsembleStrategy.BestOfN => AggregateBestOfN(valid),
                EnsembleStrategy.Consensus => AggregateConsensus(valid),
                EnsembleStrategy.Random => AggregateRandom(valid),
                EnsembleStrategy.Custom => AggregateCustom(valid),
                _ => throw new InvalidOperationException($"Unknown strategy: {_config.Strategy}")
            };
            agg.Members = _results.ToList();
            agg.Strategy = _config.Strategy;
            agg.TotalDuration = _results.Count > 0 ? TimeSpan.FromTicks(_results.Max(r => r.Duration.Ticks)) : TimeSpan.Zero;
            return agg;
        }

        public void Reset() => _results.Clear();

        public string ToJson(bool indented = true) => JsonSerializer.Serialize(Aggregate(), new JsonSerializerOptions
        {
            WriteIndented = indented,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        private EnsembleAggregation AggregateMajorityVote(List<EnsembleMemberResult> valid)
        {
            var clusters = ClusterResponses(valid);
            var largest = clusters.OrderByDescending(c => c.Value.Count).First();
            var winner = largest.Value.First();
            return new EnsembleAggregation
            {
                SelectedResponse = winner.Response, WinnerLabel = winner.Label,
                Confidence = (double)largest.Value.Count / valid.Count,
                ConsensusReached = largest.Value.Count > valid.Count / 2.0,
                ClusterCount = clusters.Count,
                Clusters = clusters.ToDictionary(kv => kv.Key, kv => kv.Value.Select(r => r.Label).ToList())
            };
        }

        private EnsembleAggregation AggregateBestOfN(List<EnsembleMemberResult> valid)
        {
            if (_config.Scorer != null)
                foreach (var r in valid.Where(r => r.Score == null))
                { try { r.Score = _config.Scorer(r.Response); } catch { r.Score = 0; } }
            var best = valid.OrderByDescending(r => r.Score ?? 0).First();
            var clusters = ClusterResponses(valid);
            return new EnsembleAggregation
            {
                SelectedResponse = best.Response, WinnerLabel = best.Label,
                Confidence = best.Score ?? 0, ConsensusReached = true,
                ClusterCount = clusters.Count,
                Clusters = clusters.ToDictionary(kv => kv.Key, kv => kv.Value.Select(r => r.Label).ToList())
            };
        }

        private EnsembleAggregation AggregateConsensus(List<EnsembleMemberResult> valid)
        {
            var clusters = ClusterResponses(valid);
            var largest = clusters.OrderByDescending(c => c.Value.Count).First();
            double agreement = (double)largest.Value.Count / valid.Count;
            bool reached = agreement >= _config.ConsensusThreshold;
            var winner = largest.Value.First();
            return new EnsembleAggregation
            {
                SelectedResponse = reached ? winner.Response : string.Empty,
                WinnerLabel = reached ? winner.Label : "(no consensus)",
                Confidence = agreement, ConsensusReached = reached,
                ClusterCount = clusters.Count,
                Clusters = clusters.ToDictionary(kv => kv.Key, kv => kv.Value.Select(r => r.Label).ToList())
            };
        }

        private EnsembleAggregation AggregateRandom(List<EnsembleMemberResult> valid)
        {
            var pick = valid[_rng.Next(valid.Count)];
            return new EnsembleAggregation
            {
                SelectedResponse = pick.Response, WinnerLabel = pick.Label,
                Confidence = 1.0 / valid.Count, ConsensusReached = false,
                ClusterCount = valid.Count,
                Clusters = valid.Select((r, i) => (r, i)).ToDictionary(x => x.i, x => new List<string> { x.r.Label })
            };
        }

        private EnsembleAggregation AggregateCustom(List<EnsembleMemberResult> valid)
        {
            if (_config.CustomAggregator == null) throw new InvalidOperationException("Custom strategy requires CustomAggregator.");
            return _config.CustomAggregator(valid);
        }

        private Dictionary<int, List<EnsembleMemberResult>> ClusterResponses(List<EnsembleMemberResult> valid)
        {
            var clusters = new Dictionary<int, List<EnsembleMemberResult>>();
            int next = 0;
            foreach (var result in valid)
            {
                int assigned = -1;
                foreach (var (idx, members) in clusters)
                    if (ResponsesMatch(members[0].Response, result.Response)) { assigned = idx; break; }
                if (assigned < 0) { assigned = next++; clusters[assigned] = new List<EnsembleMemberResult>(); }
                result.ClusterIndex = assigned;
                clusters[assigned].Add(result);
            }
            return clusters;
        }

        private bool ResponsesMatch(string a, string b) => _config.MatchMode switch
        {
            FuzzyMatchMode.Exact => a == b,
            FuzzyMatchMode.Normalized => Normalize(a) == Normalize(b),
            FuzzyMatchMode.Jaccard => JaccardSimilarity(a, b) >= _config.JaccardThreshold,
            _ => a == b
        };

        private static string Normalize(string s) =>
            string.Join(" ", s.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant().TrimEnd('.');

        private static double JaccardSimilarity(string a, string b)
        {
            var sa = new HashSet<string>(Tokenize(a)); var sb = new HashSet<string>(Tokenize(b));
            if (sa.Count == 0 && sb.Count == 0) return 1.0;
            int inter = sa.Intersect(sb).Count(); int union = sa.Union(sb).Count();
            return union == 0 ? 0 : (double)inter / union;
        }

        private static IEnumerable<string> Tokenize(string s) =>
            s.ToLowerInvariant().Split(default(char[]), StringSplitOptions.RemoveEmptyEntries)
             .Select(t => t.Trim('.', ',', '!', '?', ';', ':', '"', '\'', '(', ')'));
    }
}
