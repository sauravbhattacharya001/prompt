using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prompt
{
    /// <summary>
    /// Defines a single feature flag that controls prompt content.
    /// </summary>
    public class PromptFeatureFlag
    {
        /// <summary>Gets or sets the unique flag name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the flag description.</summary>
        public string Description { get; set; } = "";

        /// <summary>Gets or sets whether the flag is enabled globally.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the rollout percentage (0-100).
        /// When set, the flag is only active for this percentage of users/sessions.
        /// </summary>
        public int RolloutPercent { get; set; } = 100;

        /// <summary>
        /// Gets or sets the list of allowed audience segments.
        /// If empty, the flag applies to all audiences.
        /// </summary>
        public List<string> Audiences { get; set; } = new();

        /// <summary>
        /// Gets or sets the prompt content to include when this flag is active.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Gets or sets the fallback content when the flag is inactive.
        /// </summary>
        public string FallbackContent { get; set; } = "";

        /// <summary>Gets or sets the creation timestamp.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets optional metadata tags.</summary>
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Context for evaluating feature flags against a specific user/session.
    /// </summary>
    public class FlagEvaluationContext
    {
        /// <summary>Gets or sets the user or session identifier for consistent hashing.</summary>
        public string UserId { get; set; } = "";

        /// <summary>Gets or sets the audience segments this user belongs to.</summary>
        public List<string> Segments { get; set; } = new();

        /// <summary>Gets or sets additional properties for custom evaluation rules.</summary>
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    /// <summary>
    /// Result of evaluating a feature flag.
    /// </summary>
    public class FlagEvaluationResult
    {
        /// <summary>Gets or sets the flag name.</summary>
        public string FlagName { get; set; } = "";

        /// <summary>Gets or sets whether the flag resolved to active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Gets or sets the reason the flag resolved this way.</summary>
        public string Reason { get; set; } = "";

        /// <summary>Gets or sets the content that was selected.</summary>
        public string SelectedContent { get; set; } = "";
    }

    /// <summary>
    /// Manages feature flags for prompts, enabling gradual rollout, audience targeting,
    /// and consistent hashing for percentage-based rollouts.
    /// </summary>
    /// <example>
    /// <code>
    /// var manager = new PromptFeatureFlagManager();
    /// manager.Register(new PromptFeatureFlag
    /// {
    ///     Name = "chain-of-thought",
    ///     Content = "Think step by step before answering.",
    ///     RolloutPercent = 50
    /// });
    /// 
    /// var ctx = new FlagEvaluationContext { UserId = "user-123" };
    /// string prompt = manager.BuildPrompt("Answer the question.", ctx);
    /// </code>
    /// </example>
    public class PromptFeatureFlagManager
    {
        private readonly Dictionary<string, PromptFeatureFlag> _flags = new();
        private readonly List<FlagEvaluationResult> _evaluationLog = new();
        private bool _trackEvaluations = false;

        /// <summary>Gets the number of registered flags.</summary>
        public int FlagCount => _flags.Count;

        /// <summary>
        /// Enables or disables evaluation tracking for analytics.
        /// </summary>
        public void SetTracking(bool enabled) => _trackEvaluations = enabled;

        /// <summary>
        /// Registers a feature flag. Overwrites if a flag with the same name exists.
        /// </summary>
        public void Register(PromptFeatureFlag flag)
        {
            if (flag == null) throw new ArgumentNullException(nameof(flag));
            if (string.IsNullOrWhiteSpace(flag.Name))
                throw new ArgumentException("Flag name is required.");
            _flags[flag.Name] = flag;
        }

        /// <summary>
        /// Removes a flag by name. Returns true if removed.
        /// </summary>
        public bool Remove(string name) => _flags.Remove(name);

        /// <summary>
        /// Gets a flag by name, or null if not found.
        /// </summary>
        public PromptFeatureFlag? GetFlag(string name) =>
            _flags.TryGetValue(name, out var f) ? f : null;

        /// <summary>
        /// Returns all registered flags.
        /// </summary>
        public IReadOnlyList<PromptFeatureFlag> GetAllFlags() => _flags.Values.ToList();

        /// <summary>
        /// Evaluates a single flag for the given context.
        /// </summary>
        public FlagEvaluationResult Evaluate(string flagName, FlagEvaluationContext context)
        {
            if (!_flags.TryGetValue(flagName, out var flag))
            {
                return new FlagEvaluationResult
                {
                    FlagName = flagName,
                    IsActive = false,
                    Reason = "Flag not found",
                    SelectedContent = ""
                };
            }

            return EvaluateFlag(flag, context);
        }

        /// <summary>
        /// Evaluates all flags and returns results.
        /// </summary>
        public List<FlagEvaluationResult> EvaluateAll(FlagEvaluationContext context)
        {
            return _flags.Values.Select(f => EvaluateFlag(f, context)).ToList();
        }

        /// <summary>
        /// Builds a complete prompt by evaluating all flags and appending active content
        /// to the base prompt.
        /// </summary>
        /// <param name="basePrompt">The base prompt text.</param>
        /// <param name="context">The evaluation context.</param>
        /// <param name="separator">Separator between prompt sections.</param>
        /// <returns>The assembled prompt with active flag content.</returns>
        public string BuildPrompt(string basePrompt, FlagEvaluationContext context,
            string separator = "\n\n")
        {
            var parts = new List<string> { basePrompt };
            foreach (var flag in _flags.Values)
            {
                var result = EvaluateFlag(flag, context);
                if (!string.IsNullOrEmpty(result.SelectedContent))
                    parts.Add(result.SelectedContent);
            }
            return string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        /// <summary>
        /// Gets the evaluation log (requires tracking enabled).
        /// </summary>
        public IReadOnlyList<FlagEvaluationResult> GetEvaluationLog() =>
            _evaluationLog.AsReadOnly();

        /// <summary>
        /// Clears the evaluation log.
        /// </summary>
        public void ClearLog() => _evaluationLog.Clear();

        /// <summary>
        /// Exports all flags to JSON.
        /// </summary>
        public string ExportJson()
        {
            return JsonSerializer.Serialize(_flags.Values.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Imports flags from JSON. Existing flags with the same name are overwritten.
        /// </summary>
        public int ImportJson(string json)
        {
            var flags = JsonSerializer.Deserialize<List<PromptFeatureFlag>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (flags == null) return 0;
            foreach (var f in flags) _flags[f.Name] = f;
            return flags.Count;
        }

        /// <summary>
        /// Returns a summary of all flags and their current state.
        /// </summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Feature Flags: {_flags.Count} registered");
            sb.AppendLine(new string('-', 60));
            foreach (var flag in _flags.Values.OrderBy(f => f.Name))
            {
                var status = flag.Enabled ? $"ON ({flag.RolloutPercent}%)" : "OFF";
                var audiences = flag.Audiences.Count > 0
                    ? string.Join(", ", flag.Audiences) : "all";
                sb.AppendLine($"  {flag.Name}: {status} | audiences: {audiences}");
                if (!string.IsNullOrEmpty(flag.Description))
                    sb.AppendLine($"    {flag.Description}");
            }
            return sb.ToString();
        }

        private FlagEvaluationResult EvaluateFlag(PromptFeatureFlag flag, FlagEvaluationContext context)
        {
            var result = new FlagEvaluationResult { FlagName = flag.Name };

            // Check global enable
            if (!flag.Enabled)
            {
                result.IsActive = false;
                result.Reason = "Flag disabled";
                result.SelectedContent = flag.FallbackContent;
            }
            // Check audience targeting
            else if (flag.Audiences.Count > 0 &&
                     !flag.Audiences.Any(a => context.Segments.Contains(a, StringComparer.OrdinalIgnoreCase)))
            {
                result.IsActive = false;
                result.Reason = "Audience mismatch";
                result.SelectedContent = flag.FallbackContent;
            }
            // Check rollout percentage
            else if (flag.RolloutPercent < 100)
            {
                var hash = GetConsistentHash(flag.Name, context.UserId);
                var bucket = hash % 100;
                result.IsActive = bucket < flag.RolloutPercent;
                result.Reason = result.IsActive
                    ? $"Rollout hit (bucket {bucket} < {flag.RolloutPercent}%)"
                    : $"Rollout miss (bucket {bucket} >= {flag.RolloutPercent}%)";
                result.SelectedContent = result.IsActive ? flag.Content : flag.FallbackContent;
            }
            else
            {
                result.IsActive = true;
                result.Reason = "Fully enabled";
                result.SelectedContent = flag.Content;
            }

            if (_trackEvaluations)
                _evaluationLog.Add(result);

            return result;
        }

        /// <summary>
        /// Produces a consistent hash (0-99) for a flag+userId pair so the same user
        /// always gets the same result for a given flag.
        /// </summary>
        private static int GetConsistentHash(string flagName, string userId)
        {
            var input = $"{flagName}:{userId}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var value = BitConverter.ToUInt32(bytes, 0);
            return (int)(value % 100);
        }
    }
}
