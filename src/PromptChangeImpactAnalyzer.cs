namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ── Enums ────────────────────────────────────────────────

    /// <summary>
    /// Risk level for an impact finding.
    /// </summary>
    public enum ImpactRisk
    {
        /// <summary>Informational — no functional impact expected.</summary>
        Low,
        /// <summary>Moderate — behavior may change for some consumers.</summary>
        Medium,
        /// <summary>High — likely breaks dependent prompts/chains.</summary>
        High,
        /// <summary>Critical — cascading failure across many dependents.</summary>
        Critical
    }

    /// <summary>
    /// The kind of change detected in a prompt template.
    /// </summary>
    public enum ChangeKind
    {
        /// <summary>A variable was added.</summary>
        VariableAdded,
        /// <summary>A variable was removed.</summary>
        VariableRemoved,
        /// <summary>A variable was renamed (removed + added heuristic).</summary>
        VariableRenamed,
        /// <summary>Instruction text changed significantly.</summary>
        InstructionChanged,
        /// <summary>The template grew or shrank substantially.</summary>
        LengthChanged,
        /// <summary>Output format indicators changed.</summary>
        OutputFormatChanged,
        /// <summary>A default value changed.</summary>
        DefaultChanged
    }

    // ── Models ───────────────────────────────────────────────

    /// <summary>
    /// A single detected change between two versions of a prompt.
    /// </summary>
    public sealed class PromptChange
    {
        public ChangeKind Kind { get; }
        public string Description { get; }
        public ImpactRisk Risk { get; }

        public PromptChange(ChangeKind kind, string description, ImpactRisk risk)
        {
            Kind = kind;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Risk = risk;
        }

        public override string ToString() => $"[{Risk}] {Kind}: {Description}";
    }

    /// <summary>
    /// A dependent that would be affected by a prompt change.
    /// </summary>
    public sealed class AffectedDependent
    {
        /// <summary>Name/ID of the dependent (entry name, chain name, etc.).</summary>
        public string Name { get; }

        /// <summary>Type of dependent.</summary>
        public string DependentType { get; }

        /// <summary>Why this dependent is affected.</summary>
        public string Reason { get; }

        /// <summary>Estimated risk to this dependent.</summary>
        public ImpactRisk Risk { get; }

        public AffectedDependent(string name, string dependentType, string reason, ImpactRisk risk)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DependentType = dependentType ?? throw new ArgumentNullException(nameof(dependentType));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Risk = risk;
        }
    }

    /// <summary>
    /// Complete impact analysis report for a prompt change.
    /// </summary>
    public sealed class ImpactReport
    {
        /// <summary>Name of the changed prompt.</summary>
        public string PromptName { get; }

        /// <summary>Detected changes.</summary>
        public IReadOnlyList<PromptChange> Changes { get; }

        /// <summary>Affected dependents.</summary>
        public IReadOnlyList<AffectedDependent> AffectedDependents { get; }

        /// <summary>Overall risk (max of all changes and dependents).</summary>
        public ImpactRisk OverallRisk { get; }

        /// <summary>Blast radius: number of unique dependents affected.</summary>
        public int BlastRadius => AffectedDependents.Count;

        /// <summary>Cascade depth: longest chain of transitive dependents.</summary>
        public int CascadeDepth { get; }

        /// <summary>Summary recommendations.</summary>
        public IReadOnlyList<string> Recommendations { get; }

        /// <summary>Timestamp of analysis.</summary>
        public DateTimeOffset AnalyzedAt { get; }

        internal ImpactReport(
            string promptName,
            IReadOnlyList<PromptChange> changes,
            IReadOnlyList<AffectedDependent> affectedDependents,
            ImpactRisk overallRisk,
            int cascadeDepth,
            IReadOnlyList<string> recommendations)
        {
            PromptName = promptName;
            Changes = changes;
            AffectedDependents = affectedDependents;
            OverallRisk = overallRisk;
            CascadeDepth = cascadeDepth;
            Recommendations = recommendations;
            AnalyzedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>Render a human-readable text summary.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"═══ Impact Report: {PromptName} ═══");
            sb.AppendLine($"Overall Risk: {OverallRisk}");
            sb.AppendLine($"Blast Radius: {BlastRadius} dependent(s)");
            sb.AppendLine($"Cascade Depth: {CascadeDepth}");
            sb.AppendLine();

            sb.AppendLine("── Changes ──");
            foreach (var c in Changes)
                sb.AppendLine($"  [{c.Risk}] {c.Kind}: {c.Description}");
            sb.AppendLine();

            if (AffectedDependents.Count > 0)
            {
                sb.AppendLine("── Affected Dependents ──");
                foreach (var d in AffectedDependents)
                    sb.AppendLine($"  [{d.Risk}] {d.DependentType} '{d.Name}': {d.Reason}");
                sb.AppendLine();
            }

            if (Recommendations.Count > 0)
            {
                sb.AppendLine("── Recommendations ──");
                foreach (var r in Recommendations)
                    sb.AppendLine($"  • {r}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Serialize to JSON.</summary>
        public string ToJson()
        {
            var obj = new
            {
                promptName = PromptName,
                overallRisk = OverallRisk.ToString(),
                blastRadius = BlastRadius,
                cascadeDepth = CascadeDepth,
                changes = Changes.Select(c => new { kind = c.Kind.ToString(), description = c.Description, risk = c.Risk.ToString() }),
                affectedDependents = AffectedDependents.Select(d => new { name = d.Name, type = d.DependentType, reason = d.Reason, risk = d.Risk.ToString() }),
                recommendations = Recommendations,
                analyzedAt = AnalyzedAt
            };
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    // ── Analyzer ─────────────────────────────────────────────

    /// <summary>
    /// Analyzes the impact of changing a prompt template by examining
    /// variable changes, text diffs, and tracing dependents through
    /// a library, chains, and dependency graphs.
    /// </summary>
    public sealed class PromptChangeImpactAnalyzer
    {
        private static readonly Regex VarPattern =
            new(@"\{\{(\w+)\}\}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex FormatPattern =
            new(@"\b(JSON|XML|CSV|YAML|markdown|bullet|numbered|table)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));

        private readonly PromptLibrary? _library;
        private readonly IReadOnlyList<PromptChain>? _chains;
        private readonly PromptDependencyGraph? _graph;

        /// <summary>
        /// Create an analyzer with optional context for dependent tracing.
        /// </summary>
        /// <param name="library">Library to scan for entries referencing the changed prompt's variables.</param>
        /// <param name="chains">Chains to scan for steps using the changed template.</param>
        /// <param name="graph">Dependency graph for transitive impact analysis.</param>
        public PromptChangeImpactAnalyzer(
            PromptLibrary? library = null,
            IReadOnlyList<PromptChain>? chains = null,
            PromptDependencyGraph? graph = null)
        {
            _library = library;
            _chains = chains;
            _graph = graph;
        }

        /// <summary>
        /// Analyze the impact of changing a prompt from <paramref name="before"/>
        /// to <paramref name="after"/>.
        /// </summary>
        /// <param name="promptName">Identifier for the prompt being changed.</param>
        /// <param name="before">Original template.</param>
        /// <param name="after">Modified template.</param>
        /// <returns>An <see cref="ImpactReport"/> with changes, dependents, and recommendations.</returns>
        public ImpactReport Analyze(string promptName, PromptTemplate before, PromptTemplate after)
        {
            if (string.IsNullOrWhiteSpace(promptName))
                throw new ArgumentException("Prompt name is required.", nameof(promptName));
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var changes = DetectChanges(before, after);
            var dependents = TraceDependents(promptName, changes);
            int cascadeDepth = ComputeCascadeDepth(promptName);
            var overallRisk = ComputeOverallRisk(changes, dependents, cascadeDepth);
            var recommendations = GenerateRecommendations(changes, dependents, cascadeDepth, overallRisk);

            return new ImpactReport(promptName, changes, dependents, overallRisk, cascadeDepth, recommendations);
        }

        /// <summary>
        /// Quick check — does the change have any high/critical risk?
        /// </summary>
        public bool IsBreakingChange(PromptTemplate before, PromptTemplate after)
        {
            var changes = DetectChanges(before, after);
            return changes.Any(c => c.Risk >= ImpactRisk.High);
        }

        /// <summary>
        /// Detect all changes between two template versions.
        /// </summary>
        public IReadOnlyList<PromptChange> DetectChanges(PromptTemplate before, PromptTemplate after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var changes = new List<PromptChange>();

            var oldVars = ExtractVariables(before.Template);
            var newVars = ExtractVariables(after.Template);

            var removed = oldVars.Except(newVars).ToList();
            var added = newVars.Except(oldVars).ToList();

            // Heuristic: if exactly one removed + one added, it might be a rename
            if (removed.Count == 1 && added.Count == 1)
            {
                changes.Add(new PromptChange(
                    ChangeKind.VariableRenamed,
                    $"'{removed[0]}' → '{added[0]}'",
                    ImpactRisk.High));
            }
            else
            {
                foreach (var v in removed)
                    changes.Add(new PromptChange(
                        ChangeKind.VariableRemoved,
                        $"Variable '{v}' was removed",
                        ImpactRisk.High));

                foreach (var v in added)
                    changes.Add(new PromptChange(
                        ChangeKind.VariableAdded,
                        $"Variable '{v}' was added",
                        ImpactRisk.Medium));
            }

            // Default value changes
            var commonVars = oldVars.Intersect(newVars);
            foreach (var v in commonVars)
            {
                var oldDefault = GetDefault(before, v);
                var newDefault = GetDefault(after, v);
                if (oldDefault != newDefault)
                {
                    changes.Add(new PromptChange(
                        ChangeKind.DefaultChanged,
                        $"Default for '{v}' changed from '{oldDefault ?? "(none)"}' to '{newDefault ?? "(none)"}'",
                        ImpactRisk.Low));
                }
            }

            // Length change
            double lenRatio = before.Template.Length > 0
                ? (double)after.Template.Length / before.Template.Length
                : after.Template.Length > 0 ? 2.0 : 1.0;
            if (lenRatio < 0.5 || lenRatio > 2.0)
            {
                changes.Add(new PromptChange(
                    ChangeKind.LengthChanged,
                    $"Template length changed from {before.Template.Length} to {after.Template.Length} chars ({lenRatio:P0})",
                    lenRatio < 0.3 || lenRatio > 3.0 ? ImpactRisk.High : ImpactRisk.Medium));
            }

            // Instruction text similarity (strip variables, compare)
            string oldText = VarPattern.Replace(before.Template, "").Trim();
            string newText = VarPattern.Replace(after.Template, "").Trim();
            if (oldText.Length > 0 && newText.Length > 0)
            {
                double sim = ComputeSimilarity(oldText, newText);
                if (sim < 0.5)
                    changes.Add(new PromptChange(
                        ChangeKind.InstructionChanged,
                        $"Instruction text significantly changed (similarity: {sim:P0})",
                        ImpactRisk.High));
                else if (sim < 0.8)
                    changes.Add(new PromptChange(
                        ChangeKind.InstructionChanged,
                        $"Instruction text moderately changed (similarity: {sim:P0})",
                        ImpactRisk.Medium));
            }

            // Output format change
            var oldFormats = FormatPattern.Matches(oldText).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToHashSet();
            var newFormats = FormatPattern.Matches(newText).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToHashSet();
            if (!oldFormats.SetEquals(newFormats) && (oldFormats.Count > 0 || newFormats.Count > 0))
            {
                changes.Add(new PromptChange(
                    ChangeKind.OutputFormatChanged,
                    $"Output format keywords changed: [{string.Join(", ", oldFormats)}] → [{string.Join(", ", newFormats)}]",
                    ImpactRisk.Medium));
            }

            return changes.AsReadOnly();
        }

        // ── Private helpers ──────────────────────────────────

        private List<AffectedDependent> TraceDependents(string promptName, IReadOnlyList<PromptChange> changes)
        {
            var dependents = new List<AffectedDependent>();
            var removedVars = changes
                .Where(c => c.Kind == ChangeKind.VariableRemoved || c.Kind == ChangeKind.VariableRenamed)
                .ToList();

            // Scan library entries
            if (_library != null)
            {
                foreach (var entry in _library.Entries)
                {
                    if (string.Equals(entry.Name, promptName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var entryVars = ExtractVariables(entry.Template.Template);
                    foreach (var rc in removedVars)
                    {
                        string varName = rc.Kind == ChangeKind.VariableRenamed
                            ? rc.Description.Split('→')[0].Trim().Trim('\'')
                            : rc.Description.Split('\'')[1];

                        if (entryVars.Contains(varName))
                        {
                            dependents.Add(new AffectedDependent(
                                entry.Name,
                                "LibraryEntry",
                                $"Uses variable '{varName}' which was {(rc.Kind == ChangeKind.VariableRenamed ? "renamed" : "removed")}",
                                ImpactRisk.High));
                        }
                    }
                }
            }

            // Scan chains
            if (_chains != null)
            {
                for (int ci = 0; ci < _chains.Count; ci++)
                {
                    var chain = _chains[ci];
                    bool affected = false;
                    string chainLabel = chain.Steps.Count > 0
                        ? $"Chain[{chain.Steps[0].Name}..{chain.Steps[chain.Steps.Count - 1].Name}]"
                        : $"Chain#{ci}";

                    foreach (var step in chain.Steps)
                    {
                        var stepVars = ExtractVariables(step.Template.Template);
                        foreach (var rc in removedVars)
                        {
                            string varName = rc.Kind == ChangeKind.VariableRenamed
                                ? rc.Description.Split('→')[0].Trim().Trim('\'')
                                : rc.Description.Split('\'')[1];

                            if (stepVars.Contains(varName))
                                affected = true;
                        }

                        // Check if step output feeds into the changed prompt
                        if (step.OutputVariable.Equals(promptName, StringComparison.OrdinalIgnoreCase))
                            affected = true;
                    }

                    if (affected)
                    {
                        dependents.Add(new AffectedDependent(
                            chainLabel,
                            "Chain",
                            "Contains steps affected by variable changes",
                            ImpactRisk.High));
                    }
                }
            }

            // Scan dependency graph
            if (_graph != null)
            {
                var downstream = FindDownstream(promptName);
                foreach (var nodeId in downstream)
                {
                    dependents.Add(new AffectedDependent(
                        nodeId,
                        "GraphNode",
                        $"Downstream of '{promptName}' in dependency graph",
                        changes.Any(c => c.Risk >= ImpactRisk.High) ? ImpactRisk.High : ImpactRisk.Medium));
                }
            }

            return dependents;
        }

        private HashSet<string> FindDownstream(string nodeId)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_graph == null) return visited;

            var queue = new Queue<string>();
            // Find nodes that depend on nodeId
            foreach (var node in _graph.Nodes.Values)
            {
                if (node.Dependencies.Any(d => string.Equals(d, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    if (visited.Add(node.Id))
                        queue.Enqueue(node.Id);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var node in _graph.Nodes.Values)
                {
                    if (node.Dependencies.Any(d => string.Equals(d, current, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (visited.Add(node.Id))
                            queue.Enqueue(node.Id);
                    }
                }
            }

            return visited;
        }

        private int ComputeCascadeDepth(string promptName)
        {
            if (_graph == null) return 0;

            int maxDepth = 0;
            var visited = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<(string id, int depth)>();

            foreach (var node in _graph.Nodes.Values)
            {
                if (node.Dependencies.Any(d => string.Equals(d, promptName, StringComparison.OrdinalIgnoreCase)))
                    queue.Enqueue((node.Id, 1));
            }

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (visited.TryGetValue(current, out int existing) && existing >= depth)
                    continue;
                visited[current] = depth;
                maxDepth = Math.Max(maxDepth, depth);

                foreach (var node in _graph.Nodes.Values)
                {
                    if (node.Dependencies.Any(d => string.Equals(d, current, StringComparison.OrdinalIgnoreCase)))
                        queue.Enqueue((node.Id, depth + 1));
                }
            }

            return maxDepth;
        }

        private static ImpactRisk ComputeOverallRisk(
            IReadOnlyList<PromptChange> changes,
            IReadOnlyList<AffectedDependent> dependents,
            int cascadeDepth)
        {
            var maxChangeRisk = changes.Count > 0 ? changes.Max(c => c.Risk) : ImpactRisk.Low;
            var maxDepRisk = dependents.Count > 0 ? dependents.Max(d => d.Risk) : ImpactRisk.Low;
            var baseRisk = (ImpactRisk)Math.Max((int)maxChangeRisk, (int)maxDepRisk);

            // Escalate if wide blast radius or deep cascade
            if (dependents.Count >= 5 && baseRisk < ImpactRisk.Critical)
                return ImpactRisk.Critical;
            if (cascadeDepth >= 3 && baseRisk < ImpactRisk.Critical)
                return ImpactRisk.Critical;
            if (dependents.Count >= 3 && baseRisk < ImpactRisk.High)
                return ImpactRisk.High;

            return baseRisk;
        }

        private static List<string> GenerateRecommendations(
            IReadOnlyList<PromptChange> changes,
            IReadOnlyList<AffectedDependent> dependents,
            int cascadeDepth,
            ImpactRisk overallRisk)
        {
            var recs = new List<string>();

            if (changes.Any(c => c.Kind == ChangeKind.VariableRemoved))
                recs.Add("Removed variables will break callers. Add deprecation period or provide migration path.");

            if (changes.Any(c => c.Kind == ChangeKind.VariableRenamed))
                recs.Add("Renamed variable detected. Update all consumers to use the new name.");

            if (changes.Any(c => c.Kind == ChangeKind.VariableAdded))
                recs.Add("New variables added. Ensure defaults are set or callers supply them.");

            if (changes.Any(c => c.Kind == ChangeKind.OutputFormatChanged))
                recs.Add("Output format changed. Downstream parsers may need updates.");

            if (changes.Any(c => c.Kind == ChangeKind.InstructionChanged && c.Risk >= ImpactRisk.High))
                recs.Add("Major instruction rewrite. Run A/B tests to verify output quality.");

            if (dependents.Count > 0)
                recs.Add($"Update or test {dependents.Count} affected dependent(s) before deploying.");

            if (cascadeDepth >= 2)
                recs.Add($"Cascade depth is {cascadeDepth}. Consider versioning this prompt to avoid ripple effects.");

            if (overallRisk >= ImpactRisk.Critical)
                recs.Add("CRITICAL risk level. Stage this change behind a feature flag and roll out gradually.");

            if (overallRisk <= ImpactRisk.Low && dependents.Count == 0)
                recs.Add("Low risk, no dependents affected. Safe to deploy directly.");

            return recs;
        }

        private static HashSet<string> ExtractVariables(string template)
        {
            var vars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in VarPattern.Matches(template))
                vars.Add(m.Groups[1].Value);
            return vars;
        }

        private static string? GetDefault(PromptTemplate template, string variable)
        {
            try
            {
                var defaults = template.GetVariables()
                    .Where(v => v == variable)
                    .Select(v =>
                    {
                        try
                        {
                            // Try rendering with just this variable missing to see if there's a default
                            var vars = new Dictionary<string, string>();
                            foreach (var other in ExtractVariables(template.Template))
                            {
                                if (!string.Equals(other, variable, StringComparison.OrdinalIgnoreCase))
                                    vars[other] = $"__{other}__";
                            }
                            var rendered = template.Render(vars);
                            // If it rendered without error, there's a default
                            return "(has default)";
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .FirstOrDefault();
                return defaults;
            }
            catch
            {
                return null;
            }
        }

        private static double ComputeSimilarity(string a, string b)
        {
            // Simple bigram similarity (Dice coefficient)
            if (a.Length < 2 && b.Length < 2)
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;

            var bigramsA = TextAnalysisHelpers.GetNgrams(a.ToLowerInvariant(), 2);
            var bigramsB = TextAnalysisHelpers.GetNgrams(b.ToLowerInvariant(), 2);

            int intersection = 0;
            var bCopy = new Dictionary<string, int>(bigramsB);
            foreach (var bg in bigramsA)
            {
                if (bCopy.TryGetValue(bg.Key, out int count) && count > 0)
                {
                    intersection += Math.Min(bg.Value, count);
                    bCopy[bg.Key] = count - Math.Min(bg.Value, count);
                }
            }

            int total = bigramsA.Values.Sum() + bigramsB.Values.Sum();
            return total == 0 ? 1.0 : (2.0 * intersection) / total;
        }

        // GetBigrams consolidated into TextAnalysisHelpers.GetNgrams(text, 2)
    }
}
