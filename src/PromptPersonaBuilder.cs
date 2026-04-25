namespace Prompt
{
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Defines the communication style for a persona.
    /// </summary>
    public enum PersonaTone
    {
        /// <summary>Warm and approachable.</summary>
        Friendly,
        /// <summary>Formal and precise.</summary>
        Professional,
        /// <summary>Casual and relaxed.</summary>
        Casual,
        /// <summary>Direct and to-the-point.</summary>
        Concise,
        /// <summary>Detailed and thorough.</summary>
        Verbose,
        /// <summary>Encouraging and supportive.</summary>
        Supportive,
        /// <summary>Socratic and question-driven.</summary>
        Socratic,
        /// <summary>Humorous and witty.</summary>
        Witty
    }

    /// <summary>
    /// Represents a knowledge domain with expertise level.
    /// </summary>
    public record KnowledgeDomain(
        string Name,
        int ExpertiseLevel,
        string? Description = null
    )
    {
        /// <summary>Expertise level label (1-10 scale).</summary>
        public string ExpertiseLabel => ExpertiseLevel switch
        {
            <= 2 => "Novice",
            <= 4 => "Intermediate",
            <= 6 => "Advanced",
            <= 8 => "Expert",
            _ => "World-class"
        };
    }

    /// <summary>
    /// Represents a behavioral constraint or rule for the persona.
    /// </summary>
    public record PersonaConstraint(
        string Rule,
        string Severity = "must",
        string? Reason = null
    );

    /// <summary>
    /// Represents a complete persona definition.
    /// </summary>
    public class PersonaDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("background")]
        public string? Background { get; set; }

        [JsonPropertyName("tone")]
        public PersonaTone Tone { get; set; } = PersonaTone.Professional;

        [JsonPropertyName("domains")]
        public List<KnowledgeDomain> Domains { get; set; } = new();

        [JsonPropertyName("constraints")]
        public List<PersonaConstraint> Constraints { get; set; } = new();

        [JsonPropertyName("examplePhrases")]
        public List<string> ExamplePhrases { get; set; } = new();

        [JsonPropertyName("audience")]
        public string? TargetAudience { get; set; }

        [JsonPropertyName("responseFormat")]
        public string? PreferredResponseFormat { get; set; }

        [JsonPropertyName("customSections")]
        public Dictionary<string, string> CustomSections { get; set; } = new();
    }

    /// <summary>
    /// Fluent builder for constructing detailed AI persona/system prompts.
    /// Creates structured, consistent system messages that define how an
    /// AI should behave, what it knows, and how it communicates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Persona prompts are the foundation of effective AI applications.
    /// This builder ensures all critical aspects are covered: identity,
    /// expertise, communication style, constraints, and audience targeting.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var persona = new PromptPersonaBuilder()
    ///     .WithName("CodeReviewer")
    ///     .WithRole("Senior software engineer specializing in code review")
    ///     .WithBackground("15 years of experience in C#, Python, and Go")
    ///     .WithTone(PersonaTone.Professional)
    ///     .AddDomain("C#", 9, "Deep knowledge of .NET ecosystem")
    ///     .AddDomain("Design Patterns", 8)
    ///     .AddConstraint("Always explain why, not just what", "must")
    ///     .AddConstraint("Suggest alternatives when pointing out issues", "should")
    ///     .ForAudience("mid-level developers seeking to improve code quality")
    ///     .WithResponseFormat("Use bullet points for issues, code blocks for suggestions")
    ///     .AddExamplePhrase("Let's look at this from a maintainability perspective...")
    ///     .Build();
    ///
    /// string systemPrompt = persona.Render();
    /// // Use as system message in your LLM calls
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptPersonaBuilder
    {
        private readonly PersonaDefinition _definition = new();

        /// <summary>Sets the persona's name/identifier.</summary>
        public PromptPersonaBuilder WithName(string name)
        {
            _definition.Name = name ?? throw new ArgumentNullException(nameof(name));
            return this;
        }

        /// <summary>Sets the persona's role description.</summary>
        public PromptPersonaBuilder WithRole(string role)
        {
            _definition.Role = role ?? throw new ArgumentNullException(nameof(role));
            return this;
        }

        /// <summary>Sets the persona's background/experience.</summary>
        public PromptPersonaBuilder WithBackground(string background)
        {
            _definition.Background = background;
            return this;
        }

        /// <summary>Sets the communication tone.</summary>
        public PromptPersonaBuilder WithTone(PersonaTone tone)
        {
            _definition.Tone = tone;
            return this;
        }

        /// <summary>Adds a knowledge domain with expertise level (1-10).</summary>
        public PromptPersonaBuilder AddDomain(string name, int expertiseLevel, string? description = null)
        {
            if (expertiseLevel < 1 || expertiseLevel > 10)
                throw new ArgumentOutOfRangeException(nameof(expertiseLevel), "Expertise level must be between 1 and 10.");
            _definition.Domains.Add(new KnowledgeDomain(name, expertiseLevel, description));
            return this;
        }

        /// <summary>Adds a behavioral constraint or rule.</summary>
        public PromptPersonaBuilder AddConstraint(string rule, string severity = "must", string? reason = null)
        {
            _definition.Constraints.Add(new PersonaConstraint(rule, severity, reason));
            return this;
        }

        /// <summary>Adds an example phrase that demonstrates the persona's voice.</summary>
        public PromptPersonaBuilder AddExamplePhrase(string phrase)
        {
            _definition.ExamplePhrases.Add(phrase);
            return this;
        }

        /// <summary>Sets the target audience for the persona.</summary>
        public PromptPersonaBuilder ForAudience(string audience)
        {
            _definition.TargetAudience = audience;
            return this;
        }

        /// <summary>Sets the preferred response format.</summary>
        public PromptPersonaBuilder WithResponseFormat(string format)
        {
            _definition.PreferredResponseFormat = format;
            return this;
        }

        /// <summary>Adds a custom named section to the persona.</summary>
        public PromptPersonaBuilder AddSection(string title, string content)
        {
            _definition.CustomSections[title] = content;
            return this;
        }

        /// <summary>Builds the persona and returns a renderable result.</summary>
        public PersonaResult Build()
        {
            if (string.IsNullOrWhiteSpace(_definition.Name))
                throw new InvalidOperationException("Persona name is required. Call WithName() first.");
            if (string.IsNullOrWhiteSpace(_definition.Role))
                throw new InvalidOperationException("Persona role is required. Call WithRole() first.");

            return new PersonaResult(_definition);
        }

        /// <summary>
        /// Creates a persona from a JSON string.
        /// </summary>
        public static PersonaResult FromJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json);
            var definition = JsonSerializer.Deserialize<PersonaDefinition>(json)
                ?? throw new ArgumentException("Invalid persona JSON.", nameof(json));
            return new PersonaResult(definition);
        }

        /// <summary>
        /// Creates a persona from a JSON file.
        /// </summary>
        public static async Task<PersonaResult> FromFileAsync(string path, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path cannot be null or empty.", nameof(path));

            path = Path.GetFullPath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Persona file not found: {path}", path);

            SerializationGuards.ThrowIfFileTooLarge(path);

            var json = await File.ReadAllTextAsync(path, ct);
            return FromJson(json);
        }

        /// <summary>
        /// Provides pre-built persona presets for common use cases.
        /// </summary>
        public static class Presets
        {
            /// <summary>A helpful coding assistant persona.</summary>
            public static PersonaResult CodingAssistant() => new PromptPersonaBuilder()
                .WithName("CodingAssistant")
                .WithRole("Expert software developer and code reviewer")
                .WithBackground("Deep experience across multiple languages and paradigms")
                .WithTone(PersonaTone.Professional)
                .AddDomain("Software Engineering", 9)
                .AddDomain("Code Review", 8)
                .AddDomain("Debugging", 8)
                .AddConstraint("Always provide working code examples", "should")
                .AddConstraint("Explain trade-offs when multiple approaches exist", "must")
                .AddConstraint("Never generate code with known security vulnerabilities", "must")
                .ForAudience("developers of all skill levels")
                .WithResponseFormat("Use code blocks with language tags. Lead with the solution, then explain.")
                .Build();

            /// <summary>A Socratic teaching tutor.</summary>
            public static PersonaResult Tutor() => new PromptPersonaBuilder()
                .WithName("Tutor")
                .WithRole("Patient and encouraging educational tutor")
                .WithBackground("Experienced teacher skilled at breaking down complex topics")
                .WithTone(PersonaTone.Socratic)
                .AddDomain("Education", 9)
                .AddDomain("Explanation", 9)
                .AddConstraint("Guide through questions rather than giving answers directly", "should")
                .AddConstraint("Celebrate progress and effort", "must")
                .AddConstraint("Adapt difficulty to the learner's level", "must")
                .ForAudience("students and self-learners")
                .WithResponseFormat("Use step-by-step breakdowns. Ask guiding questions.")
                .Build();

            /// <summary>A technical documentation writer.</summary>
            public static PersonaResult TechnicalWriter() => new PromptPersonaBuilder()
                .WithName("TechnicalWriter")
                .WithRole("Senior technical writer specializing in developer documentation")
                .WithBackground("Experience writing API docs, guides, and tutorials for major platforms")
                .WithTone(PersonaTone.Concise)
                .AddDomain("Technical Writing", 9)
                .AddDomain("API Documentation", 8)
                .AddDomain("Developer Experience", 7)
                .AddConstraint("Use clear, unambiguous language", "must")
                .AddConstraint("Include code examples for every concept", "should")
                .AddConstraint("Follow the style: explain what, then why, then how", "must")
                .ForAudience("developers integrating APIs and libraries")
                .WithResponseFormat("Use headers, code blocks, and tables. Keep paragraphs short.")
                .Build();

            /// <summary>A creative writing collaborator.</summary>
            public static PersonaResult CreativeWriter() => new PromptPersonaBuilder()
                .WithName("CreativeWriter")
                .WithRole("Imaginative creative writing partner and story collaborator")
                .WithBackground("Widely read across fiction genres with strong narrative craft skills")
                .WithTone(PersonaTone.Friendly)
                .AddDomain("Creative Writing", 9)
                .AddDomain("Storytelling", 9)
                .AddDomain("Character Development", 8)
                .AddConstraint("Offer suggestions rather than rewriting the user's work", "should")
                .AddConstraint("Respect the author's voice and intent", "must")
                .AddConstraint("Provide specific, actionable feedback", "must")
                .ForAudience("aspiring and experienced fiction writers")
                .WithResponseFormat("Use prose for creative content, bullet points for feedback.")
                .Build();
        }
    }

    /// <summary>
    /// The result of building a persona, with rendering and serialization support.
    /// </summary>
    public class PersonaResult
    {
        private readonly PersonaDefinition _definition;

        internal PersonaResult(PersonaDefinition definition)
        {
            _definition = definition;
        }

        /// <summary>The persona's name.</summary>
        public string Name => _definition.Name;

        /// <summary>The persona's role.</summary>
        public string Role => _definition.Role;

        /// <summary>The underlying persona definition.</summary>
        public PersonaDefinition Definition => _definition;

        /// <summary>
        /// Renders the persona as a formatted system prompt string.
        /// </summary>
        public string Render()
        {
            var sb = new StringBuilder();

            // Identity
            sb.AppendLine($"# Persona: {_definition.Name}");
            sb.AppendLine();
            sb.AppendLine($"**Role:** {_definition.Role}");

            if (!string.IsNullOrWhiteSpace(_definition.Background))
            {
                sb.AppendLine($"**Background:** {_definition.Background}");
            }

            sb.AppendLine($"**Communication Style:** {ToneDescription(_definition.Tone)}");
            sb.AppendLine();

            // Target audience
            if (!string.IsNullOrWhiteSpace(_definition.TargetAudience))
            {
                sb.AppendLine($"**Target Audience:** {_definition.TargetAudience}");
                sb.AppendLine();
            }

            // Knowledge domains
            if (_definition.Domains.Count > 0)
            {
                sb.AppendLine("## Expertise");
                foreach (var domain in _definition.Domains.OrderByDescending(d => d.ExpertiseLevel))
                {
                    var bar = new string('█', domain.ExpertiseLevel) + new string('░', 10 - domain.ExpertiseLevel);
                    var desc = domain.Description != null ? $" — {domain.Description}" : "";
                    sb.AppendLine($"- {domain.Name} [{bar}] {domain.ExpertiseLabel}{desc}");
                }
                sb.AppendLine();
            }

            // Constraints
            if (_definition.Constraints.Count > 0)
            {
                sb.AppendLine("## Rules");
                foreach (var c in _definition.Constraints)
                {
                    var prefix = c.Severity.ToUpperInvariant();
                    var reason = c.Reason != null ? $" (Reason: {c.Reason})" : "";
                    sb.AppendLine($"- **{prefix}:** {c.Rule}{reason}");
                }
                sb.AppendLine();
            }

            // Response format
            if (!string.IsNullOrWhiteSpace(_definition.PreferredResponseFormat))
            {
                sb.AppendLine("## Response Format");
                sb.AppendLine(_definition.PreferredResponseFormat);
                sb.AppendLine();
            }

            // Example phrases
            if (_definition.ExamplePhrases.Count > 0)
            {
                sb.AppendLine("## Voice Examples");
                sb.AppendLine("When responding, your tone should sound like:");
                foreach (var phrase in _definition.ExamplePhrases)
                {
                    sb.AppendLine($"> \"{phrase}\"");
                }
                sb.AppendLine();
            }

            // Custom sections
            foreach (var (title, content) in _definition.CustomSections)
            {
                sb.AppendLine($"## {title}");
                sb.AppendLine(content);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders a compact version of the persona (fewer tokens).
        /// </summary>
        public string RenderCompact()
        {
            var parts = new List<string>
            {
                $"You are {_definition.Name}, {_definition.Role}."
            };

            if (!string.IsNullOrWhiteSpace(_definition.Background))
                parts.Add($"Background: {_definition.Background}.");

            parts.Add($"Tone: {_definition.Tone}.");

            if (_definition.Domains.Count > 0)
            {
                var domains = string.Join(", ", _definition.Domains
                    .OrderByDescending(d => d.ExpertiseLevel)
                    .Select(d => $"{d.Name} ({d.ExpertiseLabel})"));
                parts.Add($"Expertise: {domains}.");
            }

            if (_definition.Constraints.Count > 0)
            {
                var rules = string.Join(" ", _definition.Constraints
                    .Select(c => $"{c.Severity.ToUpperInvariant()}: {c.Rule}."));
                parts.Add($"Rules: {rules}");
            }

            if (!string.IsNullOrWhiteSpace(_definition.TargetAudience))
                parts.Add($"Audience: {_definition.TargetAudience}.");

            if (!string.IsNullOrWhiteSpace(_definition.PreferredResponseFormat))
                parts.Add($"Format: {_definition.PreferredResponseFormat}.");

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Estimates the token count of the rendered persona prompt.
        /// </summary>
        public int EstimateTokens(bool compact = false)
        {
            var text = compact ? RenderCompact() : Render();
            return PromptGuard.EstimateTokens(text);
        }

        /// <summary>
        /// Serializes the persona definition to JSON.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            return JsonSerializer.Serialize(_definition, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        /// <summary>
        /// Saves the persona definition to a JSON file.
        /// </summary>
        public async Task SaveToFileAsync(string path, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path cannot be null or empty.", nameof(path));
            path = Path.GetFullPath(path);
            await File.WriteAllTextAsync(path, ToJson(), ct);
        }

        /// <summary>
        /// Merges another persona's traits into this one, combining domains,
        /// constraints, and example phrases.
        /// </summary>
        public PersonaResult MergeWith(PersonaResult other)
        {
            var merged = new PersonaDefinition
            {
                Name = _definition.Name,
                Role = _definition.Role,
                Background = _definition.Background,
                Tone = _definition.Tone,
                TargetAudience = _definition.TargetAudience,
                PreferredResponseFormat = _definition.PreferredResponseFormat,
                Domains = new List<KnowledgeDomain>(_definition.Domains),
                Constraints = new List<PersonaConstraint>(_definition.Constraints),
                ExamplePhrases = new List<string>(_definition.ExamplePhrases),
                CustomSections = new Dictionary<string, string>(_definition.CustomSections)
            };

            // Add unique domains from other
            var existingDomains = new HashSet<string>(merged.Domains.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var domain in other.Definition.Domains)
            {
                if (!existingDomains.Contains(domain.Name))
                    merged.Domains.Add(domain);
            }

            // Add unique constraints from other
            var existingRules = new HashSet<string>(merged.Constraints.Select(c => c.Rule), StringComparer.OrdinalIgnoreCase);
            foreach (var constraint in other.Definition.Constraints)
            {
                if (!existingRules.Contains(constraint.Rule))
                    merged.Constraints.Add(constraint);
            }

            // Add unique phrases from other
            foreach (var phrase in other.Definition.ExamplePhrases)
            {
                if (!merged.ExamplePhrases.Contains(phrase))
                    merged.ExamplePhrases.Add(phrase);
            }

            // Merge custom sections
            foreach (var (key, value) in other.Definition.CustomSections)
            {
                if (!merged.CustomSections.ContainsKey(key))
                    merged.CustomSections[key] = value;
            }

            return new PersonaResult(merged);
        }

        private static string ToneDescription(PersonaTone tone) => tone switch
        {
            PersonaTone.Friendly => "Warm, approachable, and conversational",
            PersonaTone.Professional => "Formal, precise, and authoritative",
            PersonaTone.Casual => "Relaxed, informal, and easy-going",
            PersonaTone.Concise => "Direct, brief, and to-the-point",
            PersonaTone.Verbose => "Detailed, thorough, and comprehensive",
            PersonaTone.Supportive => "Encouraging, patient, and affirming",
            PersonaTone.Socratic => "Question-driven, guiding discovery through inquiry",
            PersonaTone.Witty => "Clever, humorous, and engaging",
            _ => tone.ToString()
        };
    }
}
