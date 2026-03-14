namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Builds and applies custom evaluation rubrics (scorecards) for grading
    /// prompt outputs against user-defined criteria with weighted scoring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="PromptResponseEvaluator"/> which uses fixed heuristic
    /// dimensions, PromptScorecardBuilder lets you define your own criteria
    /// with custom scoring functions, weights, and grade thresholds.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var scorecard = new PromptScorecardBuilder("Code Review Quality")
    ///     .AddCriterion("correctness", "Code produces correct output", 3.0)
    ///     .AddCriterion("readability", "Code is clean and well-named", 2.0)
    ///     .AddCriterion("efficiency", "No unnecessary operations", 1.0)
    ///     .WithGradeThresholds(new Dictionary&lt;string, double&gt;
    ///     {
    ///         ["Excellent"] = 0.9, ["Good"] = 0.7,
    ///         ["Acceptable"] = 0.5, ["Poor"] = 0.0
    ///     })
    ///     .Build();
    ///
    /// var result = scorecard.Score(new Dictionary&lt;string, double&gt;
    /// {
    ///     ["correctness"] = 0.95, ["readability"] = 0.8, ["efficiency"] = 0.7
    /// });
    /// // result.WeightedScore ~= 0.867
    /// // result.Grade == "Good"
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptScorecardBuilder
    {
        private readonly string _name;
        private readonly List<ScorecardCriterion> _criteria = new();
        private Dictionary<string, double> _gradeThresholds = new()
        {
            ["A"] = 0.9, ["B"] = 0.8, ["C"] = 0.7, ["D"] = 0.6, ["F"] = 0.0
        };
        private string _description = "";
        private readonly List<string> _tags = new();

        /// <summary>Creates a new scorecard builder with the given name.</summary>
        public PromptScorecardBuilder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Scorecard name cannot be empty.", nameof(name));
            _name = name;
        }

        /// <summary>Sets an optional description for the scorecard.</summary>
        public PromptScorecardBuilder WithDescription(string description)
        {
            _description = description ?? "";
            return this;
        }

        /// <summary>Adds a tag for categorisation.</summary>
        public PromptScorecardBuilder AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !_tags.Contains(tag))
                _tags.Add(tag);
            return this;
        }

        /// <summary>
        /// Adds a scoring criterion with a name, description, and weight.
        /// Higher weight means greater contribution to the final score.
        /// </summary>
        public PromptScorecardBuilder AddCriterion(string name, string description, double weight = 1.0)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Criterion name cannot be empty.", nameof(name));
            if (weight < 0)
                throw new ArgumentException("Weight must be non-negative.", nameof(weight));
            if (_criteria.Any(c => c.Name == name))
                throw new InvalidOperationException($"Criterion '{name}' already exists.");

            _criteria.Add(new ScorecardCriterion(name, description ?? "", weight));
            return this;
        }

        /// <summary>
        /// Adds a criterion with a custom auto-score function that evaluates
        /// the response text and returns a 0.0–1.0 score.
        /// </summary>
        public PromptScorecardBuilder AddAutoScoredCriterion(
            string name, string description, Func<string, double> scorer, double weight = 1.0)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Criterion name cannot be empty.", nameof(name));
            if (scorer == null)
                throw new ArgumentNullException(nameof(scorer));
            if (weight < 0)
                throw new ArgumentException("Weight must be non-negative.", nameof(weight));
            if (_criteria.Any(c => c.Name == name))
                throw new InvalidOperationException($"Criterion '{name}' already exists.");

            _criteria.Add(new ScorecardCriterion(name, description ?? "", weight, scorer));
            return this;
        }

        /// <summary>
        /// Replaces the default grade thresholds. Each entry maps a grade label
        /// to a minimum score (0.0–1.0). Thresholds are evaluated top-down.
        /// </summary>
        public PromptScorecardBuilder WithGradeThresholds(Dictionary<string, double> thresholds)
        {
            if (thresholds == null || thresholds.Count == 0)
                throw new ArgumentException("At least one grade threshold is required.", nameof(thresholds));
            _gradeThresholds = new Dictionary<string, double>(thresholds);
            return this;
        }

        /// <summary>Builds an immutable <see cref="Scorecard"/> from the current configuration.</summary>
        public Scorecard Build()
        {
            if (_criteria.Count == 0)
                throw new InvalidOperationException("At least one criterion is required.");

            return new Scorecard(
                _name,
                _description,
                new List<string>(_tags),
                _criteria.Select(c => c.Clone()).ToList(),
                new Dictionary<string, double>(_gradeThresholds));
        }

        /// <summary>Serialises the current builder state to JSON.</summary>
        public string ToJson()
        {
            var dto = new ScorecardDto
            {
                Name = _name,
                Description = _description,
                Tags = new List<string>(_tags),
                Criteria = _criteria.Select(c => new CriterionDto
                {
                    Name = c.Name,
                    Description = c.Description,
                    Weight = c.Weight,
                    HasAutoScorer = c.AutoScorer != null
                }).ToList(),
                GradeThresholds = new Dictionary<string, double>(_gradeThresholds)
            };
            return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Loads scorecard configuration from JSON (auto-scorers are not restored).</summary>
        public static PromptScorecardBuilder FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty.", nameof(json));

            var dto = JsonSerializer.Deserialize<ScorecardDto>(json)
                ?? throw new ArgumentException("Invalid scorecard JSON.", nameof(json));

            var builder = new PromptScorecardBuilder(dto.Name)
                .WithDescription(dto.Description ?? "");

            foreach (var tag in dto.Tags ?? new List<string>())
                builder.AddTag(tag);

            foreach (var c in dto.Criteria ?? new List<CriterionDto>())
                builder.AddCriterion(c.Name, c.Description, c.Weight);

            if (dto.GradeThresholds?.Count > 0)
                builder.WithGradeThresholds(dto.GradeThresholds);

            return builder;
        }
    }

    /// <summary>An immutable scorecard that grades responses against defined criteria.</summary>
    public class Scorecard
    {
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<string> Tags { get; }
        internal IReadOnlyList<ScorecardCriterion> Criteria { get; }
        public IReadOnlyDictionary<string, double> GradeThresholds { get; }

        internal Scorecard(string name, string description, List<string> tags,
            List<ScorecardCriterion> criteria, Dictionary<string, double> thresholds)
        {
            Name = name;
            Description = description;
            Tags = tags.AsReadOnly();
            Criteria = criteria.AsReadOnly();
            GradeThresholds = thresholds;
        }

        /// <summary>Returns the criterion names expected by this scorecard.</summary>
        public IReadOnlyList<string> CriterionNames =>
            Criteria.Select(c => c.Name).ToList().AsReadOnly();

        /// <summary>
        /// Scores a response using manually provided per-criterion scores (0.0–1.0).
        /// Missing criteria default to 0.0.
        /// </summary>
        public ScorecardResult Score(Dictionary<string, double> criterionScores)
        {
            if (criterionScores == null) throw new ArgumentNullException(nameof(criterionScores));
            return ComputeResult(criterionScores, null);
        }

        /// <summary>
        /// Auto-scores a response text using registered auto-scorer functions.
        /// Criteria without auto-scorers use the fallback scores if provided, else 0.0.
        /// </summary>
        public ScorecardResult AutoScore(string responseText, Dictionary<string, double>? fallbackScores = null)
        {
            if (responseText == null) throw new ArgumentNullException(nameof(responseText));
            return ComputeResult(fallbackScores ?? new Dictionary<string, double>(), responseText);
        }

        /// <summary>
        /// Scores multiple responses and returns ranked results (best first).
        /// </summary>
        public List<RankedResult> Compare(Dictionary<string, Dictionary<string, double>> labeledScores)
        {
            if (labeledScores == null || labeledScores.Count == 0)
                throw new ArgumentException("At least one labeled score set is required.");

            return labeledScores
                .Select(kvp => new RankedResult(kvp.Key, Score(kvp.Value)))
                .OrderByDescending(r => r.Result.WeightedScore)
                .Select((r, i) => new RankedResult(r.Label, r.Result, i + 1))
                .ToList();
        }

        private ScorecardResult ComputeResult(Dictionary<string, double> manualScores, string? responseText)
        {
            double totalWeight = 0;
            double weightedSum = 0;
            var details = new List<CriterionResult>();

            foreach (var criterion in Criteria)
            {
                double score;
                string source;

                if (responseText != null && criterion.AutoScorer != null)
                {
                    score = Math.Clamp(criterion.AutoScorer(responseText), 0.0, 1.0);
                    source = "auto";
                }
                else if (manualScores.TryGetValue(criterion.Name, out var manual))
                {
                    score = Math.Clamp(manual, 0.0, 1.0);
                    source = "manual";
                }
                else
                {
                    score = 0.0;
                    source = "default";
                }

                totalWeight += criterion.Weight;
                weightedSum += score * criterion.Weight;
                details.Add(new CriterionResult(criterion.Name, criterion.Description,
                    score, criterion.Weight, source));
            }

            double weightedScore = totalWeight > 0 ? weightedSum / totalWeight : 0.0;
            string grade = ResolveGrade(weightedScore);

            return new ScorecardResult(Name, weightedScore, grade, details);
        }

        private string ResolveGrade(double score)
        {
            foreach (var kvp in GradeThresholds.OrderByDescending(t => t.Value))
            {
                if (score >= kvp.Value) return kvp.Key;
            }
            return GradeThresholds.OrderBy(t => t.Value).First().Key;
        }

        /// <summary>Generates a human-readable text report for a result.</summary>
        public static string FormatReport(ScorecardResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"═══ Scorecard: {result.ScorecardName} ═══");
            sb.AppendLine($"Grade: {result.Grade}  |  Score: {result.WeightedScore:P1}");
            sb.AppendLine(new string('─', 50));

            foreach (var cr in result.CriterionResults)
            {
                string bar = new string('█', (int)(cr.Score * 20)) + new string('░', 20 - (int)(cr.Score * 20));
                sb.AppendLine($"  {cr.Name,-20} {bar} {cr.Score:P0} (w={cr.Weight:F1}, {cr.Source})");
            }

            sb.AppendLine(new string('─', 50));
            return sb.ToString();
        }
    }

    /// <summary>Result of scoring a response against a scorecard.</summary>
    public class ScorecardResult
    {
        public string ScorecardName { get; }
        public double WeightedScore { get; }
        public string Grade { get; }
        public IReadOnlyList<CriterionResult> CriterionResults { get; }

        [JsonIgnore]
        public bool Passed => WeightedScore >= 0.5;

        internal ScorecardResult(string name, double score, string grade, List<CriterionResult> details)
        {
            ScorecardName = name;
            WeightedScore = Math.Round(score, 4);
            Grade = grade;
            CriterionResults = details.AsReadOnly();
        }

        /// <summary>Serialises result to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Score detail for a single criterion.</summary>
    public class CriterionResult
    {
        public string Name { get; }
        public string Description { get; }
        public double Score { get; }
        public double Weight { get; }
        public string Source { get; }

        internal CriterionResult(string name, string description, double score, double weight, string source)
        {
            Name = name;
            Description = description;
            Score = Math.Round(score, 4);
            Weight = weight;
            Source = source;
        }
    }

    /// <summary>A ranked comparison entry.</summary>
    public class RankedResult
    {
        public string Label { get; }
        public ScorecardResult Result { get; }
        public int Rank { get; }

        internal RankedResult(string label, ScorecardResult result, int rank = 0)
        {
            Label = label;
            Result = result;
            Rank = rank;
        }
    }

    internal class ScorecardCriterion
    {
        public string Name { get; }
        public string Description { get; }
        public double Weight { get; }
        public Func<string, double>? AutoScorer { get; }

        public ScorecardCriterion(string name, string description, double weight, Func<string, double>? scorer = null)
        {
            Name = name;
            Description = description;
            Weight = weight;
            AutoScorer = scorer;
        }

        public ScorecardCriterion Clone() => new(Name, Description, Weight, AutoScorer);
    }

    internal class ScorecardDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
        [JsonPropertyName("criteria")] public List<CriterionDto> Criteria { get; set; } = new();
        [JsonPropertyName("gradeThresholds")] public Dictionary<string, double> GradeThresholds { get; set; } = new();
    }

    internal class CriterionDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("weight")] public double Weight { get; set; } = 1.0;
        [JsonPropertyName("hasAutoScorer")] public bool HasAutoScorer { get; set; }
    }
}
