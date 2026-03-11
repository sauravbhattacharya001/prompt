namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>Supported data types for slot values.</summary>
    public enum SlotType { Text, Integer, Number, Boolean, Date, Enum, Email, Phone }

    /// <summary>Defines a single named slot with type, constraints, and metadata.</summary>
    public sealed class SlotDefinition
    {
        public string Name { get; }
        public SlotType Type { get; }
        public bool Required { get; init; } = true;
        public string? DefaultValue { get; init; }
        public string? Description { get; init; }
        public IReadOnlyList<string>? AllowedValues { get; init; }
        public string? ValidationPattern { get; init; }
        public int? MinLength { get; init; }
        public int? MaxLength { get; init; }
        public double? MinValue { get; init; }
        public double? MaxValue { get; init; }
        public IReadOnlyList<string>? Aliases { get; init; }

        public SlotDefinition(string name, SlotType type = SlotType.Text)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Slot name cannot be empty.", nameof(name));
            Name = name;
            Type = type;
        }
    }

    /// <summary>Result of a slot-filling attempt.</summary>
    public sealed class SlotFillResult
    {
        public IReadOnlyDictionary<string, string> FilledSlots { get; internal set; }
            = new Dictionary<string, string>();
        public IReadOnlyList<string> MissingSlots { get; internal set; }
            = Array.Empty<string>();
        public IReadOnlyDictionary<string, string> Errors { get; internal set; }
            = new Dictionary<string, string>();
        public bool IsComplete => MissingSlots.Count == 0 && Errors.Count == 0;
        public string? RenderedPrompt { get; internal set; }

        /// <summary>Human-readable summary.</summary>
        public string Summary
        {
            get
            {
                var parts = new List<string> { $"Filled: {FilledSlots.Count}" };
                if (MissingSlots.Count > 0)
                    parts.Add($"Missing: {string.Join(", ", MissingSlots)}");
                if (Errors.Count > 0)
                    parts.Add($"Errors: {Errors.Count}");
                parts.Add(IsComplete ? "Status: Complete" : "Status: Incomplete");
                return string.Join(" | ", parts);
            }
        }
    }

    /// <summary>
    /// Structured slot-filling engine for prompt templates.
    /// Extracts values from user input, validates and coerces them,
    /// and progressively fills slots across multiple turns.
    /// </summary>
    public sealed class PromptSlotFiller
    {
        private readonly string _template;
        private readonly Dictionary<string, SlotDefinition> _slots = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Func<string, string?>> _customValidators = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);
        private static readonly HashSet<string> TrueValues = new(StringComparer.OrdinalIgnoreCase)
            { "true", "yes", "y", "1", "on", "yep", "yeah", "sure" };
        private static readonly HashSet<string> FalseValues = new(StringComparer.OrdinalIgnoreCase)
            { "false", "no", "n", "0", "off", "nope", "nah" };
        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(
            @"^[\+]?[\d\s\-\(\)]{7,20}$", RegexOptions.Compiled);

        /// <summary>Create a slot filler for a prompt template with {name} placeholders.</summary>
        public PromptSlotFiller(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Template cannot be empty.", nameof(template));
            _template = template;
        }

        /// <summary>The prompt template string.</summary>
        public string Template => _template;

        /// <summary>All defined slot schemas.</summary>
        public IReadOnlyDictionary<string, SlotDefinition> Slots =>
            new Dictionary<string, SlotDefinition>(_slots, StringComparer.OrdinalIgnoreCase);

        /// <summary>Define a slot schema. Fluent.</summary>
        public PromptSlotFiller DefineSlot(SlotDefinition slot)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            _slots[slot.Name] = slot;
            return this;
        }

        /// <summary>Register a custom validator. Return null for valid, or error message.</summary>
        public PromptSlotFiller WithValidator(string slotName, Func<string, string?> validator)
        {
            if (string.IsNullOrWhiteSpace(slotName))
                throw new ArgumentException("Slot name cannot be empty.", nameof(slotName));
            _customValidators[slotName] = validator ?? throw new ArgumentNullException(nameof(validator));
            return this;
        }

        /// <summary>Auto-discover slots from template placeholders.</summary>
        public PromptSlotFiller AutoDiscover()
        {
            foreach (Match m in PlaceholderRegex.Matches(_template))
            {
                var name = m.Groups[1].Value;
                if (!_slots.ContainsKey(name))
                    _slots[name] = new SlotDefinition(name);
            }
            return this;
        }

        /// <summary>Extract slot values from user input, merge with previous state.</summary>
        public SlotFillResult Fill(string input, SlotFillResult? previous = null)
        {
            var filled = previous != null
                ? new Dictionary<string, string>(previous.FilledSlots, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(input))
                return BuildResult(filled, errors);

            foreach (var kvp in _slots)
            {
                var slot = kvp.Value;
                if (filled.ContainsKey(slot.Name)) continue;

                var extracted = ExtractValue(input, slot);
                if (extracted == null) continue;

                var error = ValidateValue(extracted, slot);
                if (error != null) { errors[slot.Name] = error; continue; }

                var coerced = CoerceValue(extracted, slot);
                if (coerced != null) filled[slot.Name] = coerced;
            }
            return BuildResult(filled, errors);
        }

        /// <summary>Directly set a slot value (programmatic fill).</summary>
        public SlotFillResult SetSlot(string slotName, string value, SlotFillResult? previous = null)
        {
            var filled = previous != null
                ? new Dictionary<string, string>(previous.FilledSlots, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (_slots.TryGetValue(slotName, out var slot))
            {
                var error = ValidateValue(value, slot);
                if (error != null) errors[slotName] = error;
                else { var coerced = CoerceValue(value, slot); if (coerced != null) filled[slotName] = coerced; }
            }
            else filled[slotName] = value;

            return BuildResult(filled, errors);
        }

        /// <summary>Remove a slot value (user correction).</summary>
        public SlotFillResult ClearSlot(string slotName, SlotFillResult previous)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            var filled = new Dictionary<string, string>(previous.FilledSlots, StringComparer.OrdinalIgnoreCase);
            filled.Remove(slotName);
            return BuildResult(filled, new Dictionary<string, string>());
        }

        /// <summary>Reset all slots.</summary>
        public SlotFillResult Reset() =>
            BuildResult(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>());

        /// <summary>Generate prompt asking for missing values.</summary>
        public string GeneratePromptForMissing(SlotFillResult result)
        {
            if (result.IsComplete) return "All information has been provided.";
            var parts = new List<string>();
            foreach (var name in result.MissingSlots)
            {
                if (_slots.TryGetValue(name, out var slot))
                {
                    var desc = slot.Description ?? name;
                    var hint = slot.Type switch
                    {
                        SlotType.Integer => " (a whole number)",
                        SlotType.Number => " (a number)",
                        SlotType.Boolean => " (yes or no)",
                        SlotType.Date => " (a date, e.g. 2026-04-15)",
                        SlotType.Email => " (an email address)",
                        SlotType.Phone => " (a phone number)",
                        SlotType.Enum when slot.AllowedValues?.Count > 0 =>
                            $" (one of: {string.Join(", ", slot.AllowedValues)})",
                        _ => ""
                    };
                    parts.Add($"- {desc}{hint}");
                }
                else parts.Add($"- {name}");
            }
            return $"Please provide the following:\n{string.Join("\n", parts)}";
        }

        /// <summary>List all placeholders in the template.</summary>
        public IReadOnlyList<string> GetPlaceholders() =>
            PlaceholderRegex.Matches(_template).Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>Placeholders without defined slots.</summary>
        public IReadOnlyList<string> GetUndeclaredPlaceholders() =>
            GetPlaceholders().Where(p => !_slots.ContainsKey(p)).ToList();

        // ── Extraction ────────────────────────────────────

        private string? ExtractValue(string input, SlotDefinition slot)
        {
            var names = new List<string> { slot.Name };
            if (slot.Aliases != null) names.AddRange(slot.Aliases);

            foreach (var name in names)
            {
                var pattern = $@"(?i)\b{Regex.Escape(name)}\s*(?::|=|is)\s*(.+?)(?:\s*[,;]|$)";
                var m = Regex.Match(input, pattern);
                if (m.Success) return m.Groups[1].Value.Trim();
            }

            return slot.Type switch
            {
                SlotType.Email => ExtractByRegex(input, EmailRegex),
                SlotType.Phone => ExtractByRegex(input, PhoneRegex),
                SlotType.Date => ExtractDate(input),
                SlotType.Boolean => ExtractBoolean(input),
                SlotType.Integer => ExtractInteger(input, slot),
                SlotType.Number => ExtractNumber(input, slot),
                SlotType.Enum => ExtractEnum(input, slot),
                _ => null
            };
        }

        private static string? ExtractByRegex(string input, Regex regex)
        {
            foreach (var word in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (regex.IsMatch(word)) return word;
            return null;
        }

        private static string? ExtractDate(string input)
        {
            var iso = Regex.Match(input, @"\b(\d{4}-\d{2}-\d{2})\b");
            if (iso.Success && DateTime.TryParse(iso.Groups[1].Value, out _))
                return iso.Groups[1].Value;
            var us = Regex.Match(input, @"\b(\d{1,2}/\d{1,2}/\d{4})\b");
            if (us.Success && DateTime.TryParse(us.Groups[1].Value, out var dt))
                return dt.ToString("yyyy-MM-dd");
            return null;
        }

        private static string? ExtractBoolean(string input)
        {
            foreach (var w in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (TrueValues.Contains(w)) return "true";
                if (FalseValues.Contains(w)) return "false";
            }
            return null;
        }

        private static string? ExtractInteger(string input, SlotDefinition slot)
        {
            var matches = Regex.Matches(input, @"\b(\d+)\b");
            foreach (Match m in matches)
                if (int.TryParse(m.Groups[1].Value, out var val))
                    if ((!slot.MinValue.HasValue || val >= slot.MinValue) &&
                        (!slot.MaxValue.HasValue || val <= slot.MaxValue))
                        return val.ToString();
            foreach (Match m in matches)
                if (int.TryParse(m.Groups[1].Value, out _))
                    return m.Groups[1].Value;
            return null;
        }

        private static string? ExtractNumber(string input, SlotDefinition slot)
        {
            var matches = Regex.Matches(input, @"\b(\d+\.?\d*)\b");
            foreach (Match m in matches)
                if (double.TryParse(m.Groups[1].Value, out var val))
                    if ((!slot.MinValue.HasValue || val >= slot.MinValue) &&
                        (!slot.MaxValue.HasValue || val <= slot.MaxValue))
                        return val.ToString();
            foreach (Match m in matches)
                if (double.TryParse(m.Groups[1].Value, out _))
                    return m.Groups[1].Value;
            return null;
        }

        private static string? ExtractEnum(string input, SlotDefinition slot)
        {
            if (slot.AllowedValues == null || slot.AllowedValues.Count == 0) return null;
            var lower = input.ToLowerInvariant();
            foreach (var val in slot.AllowedValues)
                if (Regex.IsMatch(lower, $@"\b{Regex.Escape(val.ToLowerInvariant())}\b"))
                    return val;
            return null;
        }

        // ── Validation ────────────────────────────────────

        private string? ValidateValue(string value, SlotDefinition slot)
        {
            if (string.IsNullOrWhiteSpace(value))
                return slot.Required ? $"'{slot.Name}' is required." : null;

            switch (slot.Type)
            {
                case SlotType.Integer:
                    if (!int.TryParse(value, out var iv)) return $"'{slot.Name}' must be a whole number.";
                    if (slot.MinValue.HasValue && iv < slot.MinValue) return $"'{slot.Name}' must be at least {slot.MinValue}.";
                    if (slot.MaxValue.HasValue && iv > slot.MaxValue) return $"'{slot.Name}' must be at most {slot.MaxValue}.";
                    break;
                case SlotType.Number:
                    if (!double.TryParse(value, out var dv)) return $"'{slot.Name}' must be a number.";
                    if (slot.MinValue.HasValue && dv < slot.MinValue) return $"'{slot.Name}' must be at least {slot.MinValue}.";
                    if (slot.MaxValue.HasValue && dv > slot.MaxValue) return $"'{slot.Name}' must be at most {slot.MaxValue}.";
                    break;
                case SlotType.Boolean:
                    if (!TrueValues.Contains(value) && !FalseValues.Contains(value))
                        return $"'{slot.Name}' must be yes/no or true/false.";
                    break;
                case SlotType.Date:
                    if (!DateTime.TryParse(value, out _)) return $"'{slot.Name}' must be a valid date.";
                    break;
                case SlotType.Email:
                    if (!EmailRegex.IsMatch(value)) return $"'{slot.Name}' must be a valid email address.";
                    break;
                case SlotType.Phone:
                    if (!PhoneRegex.IsMatch(value)) return $"'{slot.Name}' must be a valid phone number.";
                    break;
                case SlotType.Enum:
                    if (slot.AllowedValues?.Count > 0 &&
                        !slot.AllowedValues.Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase)))
                        return $"'{slot.Name}' must be one of: {string.Join(", ", slot.AllowedValues)}.";
                    break;
            }

            if (slot.MinLength.HasValue && value.Length < slot.MinLength)
                return $"'{slot.Name}' must be at least {slot.MinLength} characters.";
            if (slot.MaxLength.HasValue && value.Length > slot.MaxLength)
                return $"'{slot.Name}' must be at most {slot.MaxLength} characters.";
            if (slot.ValidationPattern != null && !Regex.IsMatch(value, slot.ValidationPattern))
                return $"'{slot.Name}' does not match the required pattern.";
            if (_customValidators.TryGetValue(slot.Name, out var validator))
            {
                var err = validator(value);
                if (err != null) return err;
            }
            return null;
        }

        // ── Coercion ──────────────────────────────────────

        private static string? CoerceValue(string value, SlotDefinition slot) =>
            slot.Type switch
            {
                SlotType.Boolean => TrueValues.Contains(value) ? "true" : "false",
                SlotType.Integer => int.TryParse(value, out var i) ? i.ToString() : value,
                SlotType.Number => double.TryParse(value, out var d) ? d.ToString() : value,
                SlotType.Date => DateTime.TryParse(value, out var dt) ? dt.ToString("yyyy-MM-dd") : value,
                _ => value.Trim()
            };

        // ── Result Building ───────────────────────────────

        private SlotFillResult BuildResult(Dictionary<string, string> filled, Dictionary<string, string> errors)
        {
            foreach (var kvp in _slots)
                if (!filled.ContainsKey(kvp.Value.Name) && kvp.Value.DefaultValue != null)
                    filled[kvp.Value.Name] = kvp.Value.DefaultValue;

            var missing = _slots.Values
                .Where(s => s.Required && !filled.ContainsKey(s.Name))
                .Select(s => s.Name).ToList();

            string? rendered = null;
            if (missing.Count == 0 && errors.Count == 0)
                rendered = PlaceholderRegex.Replace(_template, m =>
                    filled.TryGetValue(m.Groups[1].Value, out var val) ? val : m.Value);

            return new SlotFillResult
            {
                FilledSlots = new Dictionary<string, string>(filled, StringComparer.OrdinalIgnoreCase),
                MissingSlots = missing,
                Errors = new Dictionary<string, string>(errors, StringComparer.OrdinalIgnoreCase),
                RenderedPrompt = rendered
            };
        }
    }
}
