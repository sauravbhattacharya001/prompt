using System.Text.RegularExpressions;
namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a named deployment environment (e.g., dev, staging, prod)
    /// with variable overrides, model configuration, and metadata.
    /// </summary>
    public class PromptEnvironment
    {
        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("variables")]
        public IReadOnlyDictionary<string, string> Variables { get; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("maxTokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("locked")]
        public bool Locked { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        private readonly Dictionary<string, string> _variables;

        /// <summary>
        /// Creates a new environment with optional variable overrides.
        /// </summary>
        /// <param name="name">Environment name (alphanumeric, hyphens, underscores).</param>
        /// <param name="variables">Optional variable overrides for this environment.</param>
        /// <param name="description">Optional description.</param>
        public PromptEnvironment(string name,
            IDictionary<string, string>? variables = null,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Environment name cannot be empty.", nameof(name));

            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9_-]+$", RegexOptions.None, TimeSpan.FromMilliseconds(500)))
                throw new ArgumentException(
                    "Environment name must contain only letters, digits, hyphens, and underscores.",
                    nameof(name));

            Name = name.ToLowerInvariant();
            _variables = variables != null
                ? new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Variables = _variables;
            Description = description;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        /// <summary>Sets a variable override for this environment.</summary>
        public void SetVariable(string key, string value)
        {
            if (Locked) throw new InvalidOperationException($"Environment '{Name}' is locked.");
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Variable key cannot be empty.", nameof(key));
            _variables[key] = value ?? throw new ArgumentNullException(nameof(value));
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Removes a variable override.</summary>
        public bool RemoveVariable(string key)
        {
            if (Locked) throw new InvalidOperationException($"Environment '{Name}' is locked.");
            var removed = _variables.Remove(key);
            if (removed) UpdatedAt = DateTime.UtcNow;
            return removed;
        }
    }

    /// <summary>
    /// Represents a promotion record tracking when a prompt was promoted
    /// from one environment to another.
    /// </summary>
    public class PromotionRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; }

        [JsonPropertyName("promptName")]
        public string PromptName { get; }

        [JsonPropertyName("fromEnvironment")]
        public string FromEnvironment { get; }

        [JsonPropertyName("toEnvironment")]
        public string ToEnvironment { get; }

        [JsonPropertyName("templateSnapshot")]
        public string TemplateSnapshot { get; }

        [JsonPropertyName("variablesSnapshot")]
        public IReadOnlyDictionary<string, string> VariablesSnapshot { get; }

        [JsonPropertyName("promotedAt")]
        public DateTime PromotedAt { get; }

        [JsonPropertyName("promotedBy")]
        public string? PromotedBy { get; }

        [JsonPropertyName("notes")]
        public string? Notes { get; }

        public PromotionRecord(string promptName, string from, string to,
            string templateSnapshot, IDictionary<string, string> variablesSnapshot,
            string? promotedBy = null, string? notes = null)
        {
            Id = Guid.NewGuid().ToString("N")[..12];
            PromptName = promptName;
            FromEnvironment = from;
            ToEnvironment = to;
            TemplateSnapshot = templateSnapshot;
            VariablesSnapshot = new Dictionary<string, string>(variablesSnapshot);
            PromotedAt = DateTime.UtcNow;
            PromotedBy = promotedBy;
            Notes = notes;
        }
    }

    /// <summary>
    /// Manages prompt templates across multiple deployment environments.
    /// Supports environment-specific variable overrides, promotion workflows
    /// (dev → staging → prod), rollback, comparison, and auditing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usage:
    /// <code>
    /// var mgr = new PromptEnvironmentManager();
    ///
    /// // Define environments
    /// mgr.AddEnvironment(new PromptEnvironment("dev",
    ///     new Dictionary&lt;string, string&gt; { ["tone"] = "casual", ["model"] = "gpt-3.5-turbo" }));
    /// mgr.AddEnvironment(new PromptEnvironment("staging",
    ///     new Dictionary&lt;string, string&gt; { ["tone"] = "professional" }));
    /// mgr.AddEnvironment(new PromptEnvironment("prod",
    ///     new Dictionary&lt;string, string&gt; { ["tone"] = "formal" },
    ///     description: "Production environment"));
    ///
    /// // Register a prompt with a template
    /// var template = new PromptTemplate("You are a {{tone}} assistant for {{domain}}.",
    ///     new Dictionary&lt;string, string&gt; { ["domain"] = "general" });
    /// mgr.RegisterPrompt("assistant", template);
    ///
    /// // Render for a specific environment
    /// string devPrompt = mgr.Render("assistant", "dev");
    /// // → "You are a casual assistant for general."
    ///
    /// // Promote dev → staging → prod
    /// mgr.Promote("assistant", "dev", "staging", promotedBy: "alice");
    /// mgr.Promote("assistant", "staging", "prod", promotedBy: "bob");
    ///
    /// // Rollback to previous promotion
    /// mgr.Rollback("assistant", "prod");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptEnvironmentManager
    {
        private readonly Dictionary<string, PromptEnvironment> _environments = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PromptTemplate> _prompts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, PromptTemplate>> _envPromptOverrides = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PromotionRecord> _promotionHistory = new();
        private readonly List<string> _promotionPipeline = new();

        /// <summary>All registered environments.</summary>
        public IReadOnlyList<PromptEnvironment> Environments =>
            _environments.Values.ToList().AsReadOnly();

        /// <summary>All registered prompt names.</summary>
        public IReadOnlyList<string> PromptNames =>
            _prompts.Keys.ToList().AsReadOnly();

        /// <summary>Promotion history log.</summary>
        public IReadOnlyList<PromotionRecord> PromotionHistory =>
            _promotionHistory.AsReadOnly();

        /// <summary>The ordered promotion pipeline (e.g., dev → staging → prod).</summary>
        public IReadOnlyList<string> PromotionPipeline =>
            _promotionPipeline.AsReadOnly();

        /// <summary>
        /// Sets the promotion pipeline order.
        /// </summary>
        /// <param name="stages">Ordered environment names (e.g., "dev", "staging", "prod").</param>
        public void SetPromotionPipeline(params string[] stages)
        {
            if (stages == null || stages.Length < 2)
                throw new ArgumentException("Pipeline must have at least 2 stages.");
            foreach (var s in stages)
            {
                if (!_environments.ContainsKey(s))
                    throw new ArgumentException($"Environment '{s}' not found.");
            }
            _promotionPipeline.Clear();
            _promotionPipeline.AddRange(stages.Select(s => s.ToLowerInvariant()));
        }

        /// <summary>Adds a new environment.</summary>
        public void AddEnvironment(PromptEnvironment env)
        {
            if (env == null) throw new ArgumentNullException(nameof(env));
            if (_environments.ContainsKey(env.Name))
                throw new InvalidOperationException($"Environment '{env.Name}' already exists.");
            _environments[env.Name] = env;
        }

        /// <summary>Gets an environment by name.</summary>
        public PromptEnvironment GetEnvironment(string name)
        {
            if (!_environments.TryGetValue(name, out var env))
                throw new KeyNotFoundException($"Environment '{name}' not found.");
            return env;
        }

        /// <summary>Removes an environment.</summary>
        public bool RemoveEnvironment(string name)
        {
            if (_environments.TryGetValue(name, out var env) && env.Locked)
                throw new InvalidOperationException($"Environment '{name}' is locked.");
            _envPromptOverrides.Remove(name);
            _promotionPipeline.RemoveAll(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
            return _environments.Remove(name);
        }

        /// <summary>Registers a prompt template (base version).</summary>
        public void RegisterPrompt(string name, PromptTemplate template)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Prompt name cannot be empty.", nameof(name));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            _prompts[name] = template;
        }

        /// <summary>Sets an environment-specific template override for a prompt.</summary>
        public void SetPromptOverride(string promptName, string envName, PromptTemplate template)
        {
            if (!_prompts.ContainsKey(promptName))
                throw new KeyNotFoundException($"Prompt '{promptName}' not registered.");
            if (!_environments.ContainsKey(envName))
                throw new KeyNotFoundException($"Environment '{envName}' not found.");
            if (_environments[envName].Locked)
                throw new InvalidOperationException($"Environment '{envName}' is locked.");

            if (!_envPromptOverrides.ContainsKey(envName))
                _envPromptOverrides[envName] = new Dictionary<string, PromptTemplate>(StringComparer.OrdinalIgnoreCase);
            _envPromptOverrides[envName][promptName] = template ?? throw new ArgumentNullException(nameof(template));
        }

        /// <summary>
        /// Renders a prompt for a specific environment.
        /// Uses the env-specific override if available, otherwise the base template.
        /// Environment variables are merged with any additional render-time variables.
        /// </summary>
        public string Render(string promptName, string envName,
            IDictionary<string, string>? additionalVars = null)
        {
            if (!_prompts.ContainsKey(promptName))
                throw new KeyNotFoundException($"Prompt '{promptName}' not registered.");
            if (!_environments.TryGetValue(envName, out var env))
                throw new KeyNotFoundException($"Environment '{envName}' not found.");

            // Pick override or base template
            PromptTemplate template = _prompts[promptName];
            if (_envPromptOverrides.TryGetValue(envName, out var overrides) &&
                overrides.TryGetValue(promptName, out var overrideTemplate))
            {
                template = overrideTemplate;
            }

            // Merge: base defaults < env variables < additional vars
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in env.Variables) merged[kv.Key] = kv.Value;
            if (additionalVars != null)
                foreach (var kv in additionalVars) merged[kv.Key] = kv.Value;

            return template.Render(merged);
        }

        /// <summary>
        /// Promotes a prompt from one environment to another by copying
        /// its template override and recording the promotion.
        /// </summary>
        public PromotionRecord Promote(string promptName, string fromEnv, string toEnv,
            string? promotedBy = null, string? notes = null)
        {
            if (!_prompts.ContainsKey(promptName))
                throw new KeyNotFoundException($"Prompt '{promptName}' not registered.");
            if (!_environments.ContainsKey(fromEnv))
                throw new KeyNotFoundException($"Environment '{fromEnv}' not found.");
            if (!_environments.TryGetValue(toEnv, out var targetEnv))
                throw new KeyNotFoundException($"Environment '{toEnv}' not found.");
            if (targetEnv.Locked)
                throw new InvalidOperationException($"Environment '{toEnv}' is locked.");
            if (string.Equals(fromEnv, toEnv, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Cannot promote to the same environment.");

            // Validate pipeline order if pipeline is set
            if (_promotionPipeline.Count > 0)
            {
                int fromIdx = _promotionPipeline.FindIndex(s => string.Equals(s, fromEnv, StringComparison.OrdinalIgnoreCase));
                int toIdx = _promotionPipeline.FindIndex(s => string.Equals(s, toEnv, StringComparison.OrdinalIgnoreCase));
                if (fromIdx >= 0 && toIdx >= 0 && toIdx != fromIdx + 1)
                    throw new InvalidOperationException(
                        $"Pipeline violation: cannot promote directly from '{fromEnv}' to '{toEnv}'. " +
                        $"Next stage after '{fromEnv}' is '{_promotionPipeline[fromIdx + 1]}'.");
            }

            // Get the source template
            PromptTemplate sourceTemplate = _prompts[promptName];
            if (_envPromptOverrides.TryGetValue(fromEnv, out var fromOverrides) &&
                fromOverrides.TryGetValue(promptName, out var fromTemplate))
            {
                sourceTemplate = fromTemplate;
            }

            // Copy to target
            SetPromptOverride(promptName, toEnv, sourceTemplate);

            // Record
            var record = new PromotionRecord(
                promptName, fromEnv, toEnv,
                sourceTemplate.Template,
                new Dictionary<string, string>(_environments[fromEnv].Variables),
                promotedBy, notes);
            _promotionHistory.Add(record);

            return record;
        }

        /// <summary>
        /// Rolls back a prompt in an environment to its state before
        /// the most recent promotion into that environment.
        /// </summary>
        public PromotionRecord? Rollback(string promptName, string envName)
        {
            if (!_environments.TryGetValue(envName, out var env))
                throw new KeyNotFoundException($"Environment '{envName}' not found.");
            if (env.Locked)
                throw new InvalidOperationException($"Environment '{envName}' is locked.");

            // Find the two most recent promotions into this env for this prompt
            var promotions = _promotionHistory
                .Where(p => string.Equals(p.PromptName, promptName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.ToEnvironment, envName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.PromotedAt)
                .ToList();

            if (promotions.Count < 2)
            {
                // Remove override entirely, revert to base
                if (_envPromptOverrides.TryGetValue(envName, out var overrides))
                    overrides.Remove(promptName);
                return promotions.FirstOrDefault();
            }

            // Restore the previous promotion's template
            var previous = promotions[1];
            var previousTemplate = new PromptTemplate(previous.TemplateSnapshot);
            SetPromptOverride(promptName, envName, previousTemplate);

            return previous;
        }

        /// <summary>
        /// Compares a prompt's rendering across two environments.
        /// </summary>
        public EnvironmentComparison Compare(string promptName, string envA, string envB,
            IDictionary<string, string>? additionalVars = null)
        {
            string renderA = Render(promptName, envA, additionalVars);
            string renderB = Render(promptName, envB, additionalVars);
            var envAObj = GetEnvironment(envA);
            var envBObj = GetEnvironment(envB);

            var allKeys = envAObj.Variables.Keys
                .Union(envBObj.Variables.Keys, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var diffs = new List<VariableDiff>();
            foreach (var key in allKeys)
            {
                envAObj.Variables.TryGetValue(key, out var valA);
                envBObj.Variables.TryGetValue(key, out var valB);
                if (!string.Equals(valA, valB, StringComparison.Ordinal))
                    diffs.Add(new VariableDiff(key, valA, valB));
            }

            return new EnvironmentComparison(envA, envB, promptName, renderA, renderB, diffs);
        }

        /// <summary>
        /// Gets the promotion history for a specific prompt.
        /// </summary>
        public IReadOnlyList<PromotionRecord> GetPromotionHistory(string promptName) =>
            _promotionHistory
                .Where(p => string.Equals(p.PromptName, promptName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.PromotedAt)
                .ToList()
                .AsReadOnly();

        /// <summary>
        /// Gets a summary of which prompts are deployed to which environments.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> GetDeploymentMatrix()
        {
            var matrix = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var promptName in _prompts.Keys)
            {
                var envs = new List<string>();
                foreach (var envName in _environments.Keys)
                {
                    if (_envPromptOverrides.TryGetValue(envName, out var overrides) &&
                        overrides.ContainsKey(promptName))
                    {
                        envs.Add(envName);
                    }
                    else
                    {
                        envs.Add(envName + " (base)");
                    }
                }
                matrix[promptName] = envs.AsReadOnly();
            }
            return matrix;
        }

        /// <summary>
        /// Validates that a prompt renders correctly in all environments.
        /// Returns a dictionary of environment → error message (empty if all valid).
        /// </summary>
        public IReadOnlyDictionary<string, string?> ValidateAcrossEnvironments(string promptName,
            IDictionary<string, string>? additionalVars = null)
        {
            var results = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var envName in _environments.Keys)
            {
                try
                {
                    Render(promptName, envName, additionalVars);
                    results[envName] = null;
                }
                catch (Exception ex)
                {
                    results[envName] = ex.Message;
                }
            }
            return results;
        }

        /// <summary>
        /// Exports the full state as JSON.
        /// </summary>
        public string ExportJson()
        {
            var state = new
            {
                environments = _environments.Values.Select(e => new
                {
                    e.Name,
                    e.Description,
                    e.Model,
                    e.MaxTokens,
                    e.Temperature,
                    e.Locked,
                    variables = e.Variables,
                    e.CreatedAt,
                    e.UpdatedAt
                }),
                prompts = _prompts.Select(kv => new { name = kv.Key, template = kv.Value.Template }),
                overrides = _envPromptOverrides.SelectMany(env =>
                    env.Value.Select(p => new
                    {
                        environment = env.Key,
                        prompt = p.Key,
                        template = p.Value.Template
                    })),
                pipeline = _promotionPipeline,
                promotionHistory = _promotionHistory.Select(r => new
                {
                    r.Id,
                    r.PromptName,
                    r.FromEnvironment,
                    r.ToEnvironment,
                    r.TemplateSnapshot,
                    r.VariablesSnapshot,
                    r.PromotedAt,
                    r.PromotedBy,
                    r.Notes
                })
            };

            return JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }

    /// <summary>
    /// Represents a variable difference between two environments.
    /// </summary>
    public class VariableDiff
    {
        public string Key { get; }
        public string? ValueA { get; }
        public string? ValueB { get; }

        public VariableDiff(string key, string? valueA, string? valueB)
        {
            Key = key;
            ValueA = valueA;
            ValueB = valueB;
        }
    }

    /// <summary>
    /// Result of comparing a prompt across two environments.
    /// </summary>
    public class EnvironmentComparison
    {
        public string EnvironmentA { get; }
        public string EnvironmentB { get; }
        public string PromptName { get; }
        public string RenderA { get; }
        public string RenderB { get; }
        public IReadOnlyList<VariableDiff> VariableDiffs { get; }
        public bool AreIdentical => RenderA == RenderB;

        public EnvironmentComparison(string envA, string envB, string promptName,
            string renderA, string renderB, IReadOnlyList<VariableDiff> diffs)
        {
            EnvironmentA = envA;
            EnvironmentB = envB;
            PromptName = promptName;
            RenderA = renderA;
            RenderB = renderB;
            VariableDiffs = diffs;
        }
    }
}
