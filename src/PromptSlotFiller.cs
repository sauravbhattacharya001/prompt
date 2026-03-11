namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Describes how an unfilled slot was resolved.
    /// </summary>
    public enum SlotResolution
    {
        /// <summary>Slot remains unfilled.</summary>
        Unfilled,
        /// <summary>Filled from explicit user-supplied values.</summary>
        Explicit,
        /// <summary>Filled from template defaults.</summary>
        Default,
        /// <summary>Filled by a registered slot provider.</summary>
        Provider,
        /// <summary>Filled by a fallback value.</summary>
        Fallback,
        /// <summary>Filled by a validation/transform rule.</summary>
        Transformed
    }

    /// <summary>
    /// A detected slot within a prompt template.
    /// </summary>
    public class PromptSlot
    {
        /// <summary>The variable name (e.g. "topic").</summary>
        public string Name { get; set; } = "";

        /// <summary>The full match text including delimiters (e.g. "{{topic}}").</summary>
        public string RawMatch { get; set; } = "";

        /// <summary>Zero-based position in the template string.</summary>
        public int Position { get; set; }

        /// <summary>Whether the slot has a default value in the template (e.g. "{{topic:default}}").</summary>
        public bool HasDefault { get; set; }

        /// <summary>The default value if present.</summary>
        public string? DefaultValue { get; set; }

        /// <summary>Whether this slot is required (no default, no fallback).</summary>
        public bool IsRequired => !HasDefault;

        /// <summary>The resolved value for this slot.</summary>
        public string? ResolvedValue { get; set; }

        /// <summary>How this slot was resolved.</summary>
        public SlotResolution Resolution { get; set; } = SlotResolution.Unfilled;
    }

    /// <summary>
    /// Result of a slot-filling operation.
    /// </summary>
    public class SlotFillResult
    {
        /// <summary>The original template text.</summary>
        public string OriginalTemplate { get; set; } = "";

        /// <summary>The filled output text.</summary>
        public string FilledText { get; set; } = "";

        /// <summary>All slots detected in the template.</summary>
        public List<PromptSlot> Slots { get; set; } = new();

        /// <summary>Slots that were successfully filled.</summary>
        public List<PromptSlot> FilledSlots => Slots.Where(s => s.Resolution != SlotResolution.Unfilled).ToList();

        /// <summary>Slots that remain unfilled.</summary>
        public List<PromptSlot> UnfilledSlots => Slots.Where(s => s.Resolution == SlotResolution.Unfilled).ToList();

        /// <summary>Whether all slots were filled.</summary>
        public bool IsComplete => Slots.All(s => s.Resolution != SlotResolution.Unfilled);

        /// <summary>Whether all required (no default) slots were filled.</summary>
        public bool RequiredSlotsFilled => Slots.Where(s => s.IsRequired).All(s => s.Resolution != SlotResolution.Unfilled);

        /// <summary>Percentage of slots that were filled (0-100).</summary>
        public double FillPercentage => Slots.Count == 0 ? 100.0 : Math.Round(FilledSlots.Count * 100.0 / Slots.Count, 1);

        /// <summary>Summary of how slots were resolved.</summary>
        public Dictionary<SlotResolution, int> ResolutionSummary =>
            Slots.GroupBy(s => s.Resolution).ToDictionary(g => g.Key, g => g.Count());

        /// <summary>Warnings generated during filling.</summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Provides values for named slots. Implement this interface to create
    /// custom slot providers (e.g. from config, environment, database, etc.)
    /// </summary>
    public interface ISlotProvider
    {
        /// <summary>Name of this provider (for debugging/logging).</summary>
        string Name { get; }

        /// <summary>Priority (lower = tried first).</summary>
        int Priority { get; }

        /// <summary>
        /// Try to resolve a slot value. Return null if this provider can't fill it.
        /// </summary>
        string? Resolve(string slotName, string? currentValue);

        /// <summary>
        /// Check if this provider can handle a given slot name.
        /// </summary>
        bool CanResolve(string slotName);
    }

    /// <summary>
    /// Validates and optionally transforms slot values before insertion.
    /// </summary>
    public class SlotValidator
    {
        /// <summary>The slot name pattern to match (supports wildcards: * matches any).</summary>
        public string SlotPattern { get; set; } = "*";

        /// <summary>Validation function. Returns null if valid, error message if invalid.</summary>
        public Func<string, string?>? Validate { get; set; }

        /// <summary>Optional transform applied after validation.</summary>
        public Func<string, string>? Transform { get; set; }

        /// <summary>Whether to reject the fill if validation fails (true) or just warn (false).</summary>
        public bool RejectOnFailure { get; set; }

        internal bool Matches(string slotName)
        {
            if (SlotPattern == "*") return true;
            if (SlotPattern.EndsWith("*"))
                return slotName.StartsWith(SlotPattern[..^1], StringComparison.OrdinalIgnoreCase);
            if (SlotPattern.StartsWith("*"))
                return slotName.EndsWith(SlotPattern[1..], StringComparison.OrdinalIgnoreCase);
            return slotName.Equals(SlotPattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Provides slot values from a dictionary.
    /// </summary>
    public class DictionarySlotProvider : ISlotProvider
    {
        private readonly Dictionary<string, string> _values;
        public string Name { get; }
        public int Priority { get; }

        public DictionarySlotProvider(Dictionary<string, string> values, string name = "Dictionary", int priority = 50)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
            Name = name;
            Priority = priority;
        }

        public bool CanResolve(string slotName) => _values.ContainsKey(slotName);
        public string? Resolve(string slotName, string? currentValue) =>
            _values.TryGetValue(slotName, out var value) ? value : null;
    }

    /// <summary>
    /// Provides slot values from environment variables.
    /// </summary>
    public class EnvironmentSlotProvider : ISlotProvider
    {
        private readonly string _prefix;
        public string Name => "Environment";
        public int Priority { get; }

        /// <param name="prefix">Optional prefix for env var names (e.g. "PROMPT_" → looks up "PROMPT_slotname").</param>
        /// <param name="priority">Resolution priority.</param>
        public EnvironmentSlotProvider(string prefix = "", int priority = 80)
        {
            _prefix = prefix;
            Priority = priority;
        }

        public bool CanResolve(string slotName) =>
            Environment.GetEnvironmentVariable(_prefix + slotName.ToUpperInvariant().Replace('-', '_')) != null;

        public string? Resolve(string slotName, string? currentValue) =>
            Environment.GetEnvironmentVariable(_prefix + slotName.ToUpperInvariant().Replace('-', '_'));
    }

    /// <summary>
    /// Provides slot values using a delegate function.
    /// </summary>
    public class FuncSlotProvider : ISlotProvider
    {
        private readonly Func<string, string?> _resolver;
        public string Name { get; }
        public int Priority { get; }

        public FuncSlotProvider(Func<string, string?> resolver, string name = "Function", int priority = 60)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Name = name;
            Priority = priority;
        }

        public bool CanResolve(string slotName) => _resolver(slotName) != null;
        public string? Resolve(string slotName, string? currentValue) => _resolver(slotName);
    }

    /// <summary>
    /// Intelligently detects unfilled template slots and fills them using
    /// a pipeline of providers, defaults, validators, and transforms.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="PromptTemplate"/> which does simple variable substitution,
    /// SlotFiller provides a rich pipeline: detection → provider resolution →
    /// validation → transformation → filling, with full diagnostics.
    /// </para>
    /// <para>
    /// Supports multiple slot syntaxes: <c>{{name}}</c>, <c>{{name:default}}</c>,
    /// <c>{name}</c>, and <c>$name$</c>.
    /// </para>
    /// <example>
    /// <code>
    /// var filler = new PromptSlotFiller()
    ///     .AddProvider(new DictionarySlotProvider(new Dictionary&lt;string, string&gt;
    ///     {
    ///         ["role"] = "data analyst",
    ///         ["format"] = "JSON"
    ///     }))
    ///     .AddValidator(new SlotValidator
    ///     {
    ///         SlotPattern = "format",
    ///         Validate = v => new[] { "JSON", "CSV", "XML" }.Contains(v) ? null : "Invalid format",
    ///         Transform = v => v.ToUpperInvariant()
    ///     })
    ///     .WithFallback("[MISSING]");
    ///
    /// var result = filler.Fill("You are a {{role}}. Output in {{format}}. Topic: {{topic}}.");
    /// // result.FilledText = "You are a data analyst. Output in JSON. Topic: [MISSING]."
    /// // result.UnfilledSlots = [ { Name = "topic", ... } ]
    /// </code>
    /// </example>
    /// </remarks>
    public class PromptSlotFiller
    {
        private readonly List<ISlotProvider> _providers = new();
        private readonly List<SlotValidator> _validators = new();
        private string? _fallback;
        private SlotSyntax _syntax = SlotSyntax.DoubleCurly;
        private bool _caseSensitive;
        private bool _strictMode;

        // Regex patterns for different syntaxes
        private static readonly Regex DoubleCurlyRegex = new(@"\{\{([^{}]+?)\}\}", RegexOptions.Compiled);
        private static readonly Regex SingleCurlyRegex = new(@"(?<!\{)\{([^{}]+?)\}(?!\})", RegexOptions.Compiled);
        private static readonly Regex DollarRegex = new(@"\$([A-Za-z_][A-Za-z0-9_]*)\$", RegexOptions.Compiled);
        private static readonly Regex AllSyntaxRegex = new(@"\{\{([^{}]+?)\}\}|(?<!\{)\{([^{}]+?)\}(?!\})|\$([A-Za-z_][A-Za-z0-9_]*)\$", RegexOptions.Compiled);

        /// <summary>
        /// Supported slot syntax styles.
        /// </summary>
        public enum SlotSyntax
        {
            /// <summary>{{name}} or {{name:default}}</summary>
            DoubleCurly,
            /// <summary>{name}</summary>
            SingleCurly,
            /// <summary>$name$</summary>
            Dollar,
            /// <summary>All syntaxes detected</summary>
            Auto
        }

        /// <summary>
        /// Set the slot syntax to detect.
        /// </summary>
        public PromptSlotFiller WithSyntax(SlotSyntax syntax)
        {
            _syntax = syntax;
            return this;
        }

        /// <summary>
        /// Add a slot value provider to the pipeline.
        /// </summary>
        public PromptSlotFiller AddProvider(ISlotProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            _providers.Add(provider);
            return this;
        }

        /// <summary>
        /// Add explicit key-value pairs as a provider.
        /// </summary>
        public PromptSlotFiller AddValues(Dictionary<string, string> values, int priority = 10)
        {
            _providers.Add(new DictionarySlotProvider(values, "Explicit", priority));
            return this;
        }

        /// <summary>
        /// Add a slot validator/transform.
        /// </summary>
        public PromptSlotFiller AddValidator(SlotValidator validator)
        {
            ArgumentNullException.ThrowIfNull(validator);
            _validators.Add(validator);
            return this;
        }

        /// <summary>
        /// Set a fallback value for unfilled slots (replaces the placeholder text).
        /// </summary>
        public PromptSlotFiller WithFallback(string fallback)
        {
            _fallback = fallback;
            return this;
        }

        /// <summary>
        /// Enable case-sensitive slot name matching.
        /// </summary>
        public PromptSlotFiller CaseSensitive(bool enabled = true)
        {
            _caseSensitive = enabled;
            return this;
        }

        /// <summary>
        /// Enable strict mode: throws if any required slot is unfilled.
        /// </summary>
        public PromptSlotFiller Strict(bool enabled = true)
        {
            _strictMode = enabled;
            return this;
        }

        /// <summary>
        /// Detect all slots in a template without filling them.
        /// </summary>
        public List<PromptSlot> DetectSlots(string template)
        {
            if (string.IsNullOrEmpty(template)) return new();

            var regex = GetRegex();
            var slots = new List<PromptSlot>();
            var seen = new HashSet<string>(_caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

            foreach (Match match in regex.Matches(template))
            {
                var rawName = ExtractSlotName(match);
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                var name = rawName;
                string? defaultValue = null;
                bool hasDefault = false;

                // Check for default value syntax: {{name:default}}
                var colonIdx = rawName.IndexOf(':');
                if (colonIdx > 0)
                {
                    name = rawName[..colonIdx].Trim();
                    defaultValue = rawName[(colonIdx + 1)..].Trim();
                    hasDefault = true;
                }

                var key = _caseSensitive ? name : name.ToLowerInvariant();
                if (seen.Contains(key)) continue;
                seen.Add(key);

                slots.Add(new PromptSlot
                {
                    Name = name,
                    RawMatch = match.Value,
                    Position = match.Index,
                    HasDefault = hasDefault,
                    DefaultValue = defaultValue
                });
            }

            return slots;
        }

        /// <summary>
        /// Fill all detected slots in the template using the configured pipeline.
        /// </summary>
        public SlotFillResult Fill(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return new SlotFillResult { OriginalTemplate = template ?? "", FilledText = template ?? "" };
            }

            var slots = DetectSlots(template);
            var result = new SlotFillResult
            {
                OriginalTemplate = template,
                Slots = slots
            };

            // Resolve each slot
            foreach (var slot in slots)
            {
                ResolveSlot(slot, result);
            }

            // Build filled text
            var filled = template;
            var regex = GetRegex();
            filled = regex.Replace(filled, match =>
            {
                var rawName = ExtractSlotName(match);
                var name = rawName;
                var colonIdx = rawName.IndexOf(':');
                if (colonIdx > 0) name = rawName[..colonIdx].Trim();

                var slot = slots.FirstOrDefault(s =>
                    string.Equals(s.Name, name, _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

                if (slot == null) return match.Value;

                if (slot.Resolution != SlotResolution.Unfilled && slot.ResolvedValue != null)
                    return slot.ResolvedValue;

                if (_fallback != null)
                {
                    slot.Resolution = SlotResolution.Fallback;
                    slot.ResolvedValue = _fallback;
                    return _fallback;
                }

                return match.Value;
            });

            result.FilledText = filled;

            // Strict mode check
            if (_strictMode)
            {
                var unfilled = result.UnfilledSlots.Where(s => s.IsRequired).ToList();
                if (unfilled.Count > 0)
                {
                    var names = string.Join(", ", unfilled.Select(s => s.Name));
                    throw new InvalidOperationException($"Required slots not filled: {names}");
                }
            }

            return result;
        }

        /// <summary>
        /// Fill a template with explicit values (convenience method).
        /// </summary>
        public SlotFillResult Fill(string template, Dictionary<string, string> values)
        {
            var filler = Clone();
            filler.AddValues(values);
            return filler.Fill(template);
        }

        /// <summary>
        /// Get a diagnostic report of slot detection and resolution.
        /// </summary>
        public string Diagnose(string template)
        {
            var result = Fill(template);
            var sb = new StringBuilder();

            sb.AppendLine("Slot Fill Diagnostic Report");
            sb.AppendLine(new string('─', 50));
            sb.AppendLine($"Template length: {template.Length} chars");
            sb.AppendLine($"Slots detected:  {result.Slots.Count}");
            sb.AppendLine($"Slots filled:    {result.FilledSlots.Count}");
            sb.AppendLine($"Slots unfilled:  {result.UnfilledSlots.Count}");
            sb.AppendLine($"Fill percentage:  {result.FillPercentage}%");
            sb.AppendLine($"Complete:        {result.IsComplete}");
            sb.AppendLine();

            sb.AppendLine("Slot Details:");
            foreach (var slot in result.Slots)
            {
                var status = slot.Resolution == SlotResolution.Unfilled ? "❌" : "✅";
                sb.AppendLine($"  {status} {slot.Name}");
                sb.AppendLine($"     Position:   {slot.Position}");
                sb.AppendLine($"     Resolution: {slot.Resolution}");
                if (slot.HasDefault)
                    sb.AppendLine($"     Default:    {slot.DefaultValue}");
                if (slot.ResolvedValue != null)
                    sb.AppendLine($"     Value:      {slot.ResolvedValue}");
            }

            if (result.Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings:");
                foreach (var w in result.Warnings)
                    sb.AppendLine($"  ⚠ {w}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serialize a SlotFillResult to JSON.
        /// </summary>
        public static string ToJson(SlotFillResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            var obj = new
            {
                originalTemplate = result.OriginalTemplate,
                filledText = result.FilledText,
                isComplete = result.IsComplete,
                fillPercentage = result.FillPercentage,
                slots = result.Slots.Select(s => new
                {
                    name = s.Name,
                    position = s.Position,
                    hasDefault = s.HasDefault,
                    defaultValue = s.DefaultValue,
                    resolvedValue = s.ResolvedValue,
                    resolution = s.Resolution.ToString(),
                    isRequired = s.IsRequired
                }),
                resolutionSummary = result.ResolutionSummary.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                warnings = result.Warnings
            };
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }

        // ── Internal ─────────────────────────────────────────

        private void ResolveSlot(PromptSlot slot, SlotFillResult result)
        {
            // 1. Try providers (sorted by priority)
            foreach (var provider in _providers.OrderBy(p => p.Priority))
            {
                try
                {
                    var value = provider.Resolve(slot.Name, slot.ResolvedValue);
                    if (value != null)
                    {
                        slot.ResolvedValue = value;
                        slot.Resolution = provider is DictionarySlotProvider dp && dp.Name == "Explicit"
                            ? SlotResolution.Explicit
                            : SlotResolution.Provider;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Provider '{provider.Name}' failed for slot '{slot.Name}': {ex.Message}");
                }
            }

            // 2. Fall back to template default
            if (slot.Resolution == SlotResolution.Unfilled && slot.HasDefault)
            {
                slot.ResolvedValue = slot.DefaultValue;
                slot.Resolution = SlotResolution.Default;
            }

            // 3. Apply validators/transforms
            if (slot.ResolvedValue != null)
            {
                foreach (var validator in _validators.Where(v => v.Matches(slot.Name)))
                {
                    // Validate
                    if (validator.Validate != null)
                    {
                        var error = validator.Validate(slot.ResolvedValue);
                        if (error != null)
                        {
                            if (validator.RejectOnFailure)
                            {
                                result.Warnings.Add($"Slot '{slot.Name}' value rejected: {error}");
                                slot.ResolvedValue = null;
                                slot.Resolution = SlotResolution.Unfilled;
                                return;
                            }
                            result.Warnings.Add($"Slot '{slot.Name}' validation warning: {error}");
                        }
                    }

                    // Transform
                    if (validator.Transform != null && slot.ResolvedValue != null)
                    {
                        slot.ResolvedValue = validator.Transform(slot.ResolvedValue);
                        if (slot.Resolution == SlotResolution.Provider || slot.Resolution == SlotResolution.Explicit)
                            slot.Resolution = SlotResolution.Transformed;
                    }
                }
            }
        }

        private Regex GetRegex() => _syntax switch
        {
            SlotSyntax.DoubleCurly => DoubleCurlyRegex,
            SlotSyntax.SingleCurly => SingleCurlyRegex,
            SlotSyntax.Dollar => DollarRegex,
            SlotSyntax.Auto => AllSyntaxRegex,
            _ => DoubleCurlyRegex
        };

        private static string ExtractSlotName(Match match)
        {
            // For Auto syntax, multiple groups may be captured
            for (int i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success)
                    return match.Groups[i].Value.Trim();
            }
            return match.Groups[1].Value.Trim();
        }

        private PromptSlotFiller Clone()
        {
            var clone = new PromptSlotFiller
            {
                _fallback = _fallback,
                _syntax = _syntax,
                _caseSensitive = _caseSensitive,
                _strictMode = _strictMode
            };
            clone._providers.AddRange(_providers);
            clone._validators.AddRange(_validators);
            return clone;
        }
    }
}
