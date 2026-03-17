namespace Prompt.Tests
{
    using Xunit;
    using System.Collections.Generic;
    using System.Linq;

    public class PromptChangeImpactAnalyzerTests
    {
        // ── Basic change detection ──────────────────────────

        [Fact]
        public void Analyze_NullName_Throws()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var t = new PromptTemplate("hello");
            Assert.Throws<ArgumentException>(() => analyzer.Analyze("", t, t));
        }

        [Fact]
        public void Analyze_NullBefore_Throws()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            Assert.Throws<ArgumentNullException>(() => analyzer.Analyze("test", null!, new PromptTemplate("x")));
        }

        [Fact]
        public void Analyze_NullAfter_Throws()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            Assert.Throws<ArgumentNullException>(() => analyzer.Analyze("test", new PromptTemplate("x"), null!));
        }

        [Fact]
        public void Analyze_IdenticalTemplates_NoChanges()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var t = new PromptTemplate("You are a {{role}} assistant.");
            var report = analyzer.Analyze("test", t, t);

            Assert.Empty(report.Changes);
            Assert.Equal(0, report.BlastRadius);
            Assert.Equal(ImpactRisk.Low, report.OverallRisk);
        }

        [Fact]
        public void DetectChanges_VariableAdded()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Help with {{topic}}.");
            var after = new PromptTemplate("Help with {{topic}} in {{language}}.");

            var changes = analyzer.DetectChanges(before, after);
            Assert.Contains(changes, c => c.Kind == ChangeKind.VariableAdded && c.Description.Contains("language"));
            Assert.True(changes.First(c => c.Kind == ChangeKind.VariableAdded).Risk == ImpactRisk.Medium);
        }

        [Fact]
        public void DetectChanges_VariableRemoved()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Help with {{topic}} in {{language}}.");
            var after = new PromptTemplate("Help with {{topic}}.");

            var changes = analyzer.DetectChanges(before, after);
            Assert.Contains(changes, c => c.Kind == ChangeKind.VariableRemoved && c.Description.Contains("language"));
            Assert.True(changes.First(c => c.Kind == ChangeKind.VariableRemoved).Risk == ImpactRisk.High);
        }

        [Fact]
        public void DetectChanges_VariableRenamed()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Summarize {{document}}.");
            var after = new PromptTemplate("Summarize {{content}}.");

            var changes = analyzer.DetectChanges(before, after);
            Assert.Contains(changes, c => c.Kind == ChangeKind.VariableRenamed);
        }

        [Fact]
        public void DetectChanges_OutputFormatChanged()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Respond in JSON format.");
            var after = new PromptTemplate("Respond in CSV format.");

            var changes = analyzer.DetectChanges(before, after);
            Assert.Contains(changes, c => c.Kind == ChangeKind.OutputFormatChanged);
        }

        [Fact]
        public void DetectChanges_MajorInstructionChange()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("You are a helpful coding assistant that writes clean C# code.");
            var after = new PromptTemplate("Generate creative fiction stories with vivid imagery and dialogue.");

            var changes = analyzer.DetectChanges(before, after);
            Assert.Contains(changes, c => c.Kind == ChangeKind.InstructionChanged && c.Risk >= ImpactRisk.High);
        }

        [Fact]
        public void DetectChanges_LengthChange_Significant()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Short prompt.");
            var after = new PromptTemplate("This is a much longer prompt that has been expanded significantly with many additional instructions, guidelines, constraints, examples, and other content that makes it quite verbose compared to the original version.");

            var changes = analyzer.DetectChanges(before, after);
            Assert.Contains(changes, c => c.Kind == ChangeKind.LengthChanged);
        }

        [Fact]
        public void IsBreakingChange_RemovedVariable_True()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("{{role}}: Help with {{topic}}.");
            var after = new PromptTemplate("Help with {{topic}}.");

            Assert.True(analyzer.IsBreakingChange(before, after));
        }

        [Fact]
        public void IsBreakingChange_AddedVariable_False()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Help with {{topic}}.");
            var after = new PromptTemplate("Help with {{topic}} please, {{user}}.");

            Assert.False(analyzer.IsBreakingChange(before, after));
        }

        // ── Library dependent tracing ───────────────────────

        [Fact]
        public void Analyze_LibraryDependent_DetectsAffectedEntry()
        {
            var library = new PromptLibrary();
            library.Add("main-prompt", new PromptTemplate("{{role}}: do {{task}}."));
            library.Add("helper", new PromptTemplate("As a {{role}}, assist."));

            var analyzer = new PromptChangeImpactAnalyzer(library: library);
            var before = new PromptTemplate("{{role}}: do {{task}}.");
            var after = new PromptTemplate("Do {{task}}."); // removed role

            var report = analyzer.Analyze("main-prompt", before, after);

            Assert.True(report.BlastRadius >= 1);
            Assert.Contains(report.AffectedDependents, d => d.Name == "helper" && d.DependentType == "LibraryEntry");
        }

        // ── Chain dependent tracing ─────────────────────────

        [Fact]
        public void Analyze_ChainDependent_DetectsAffectedChain()
        {
            var chain = new PromptChain()
                .AddStep("step1", new PromptTemplate("Review {{code}} for {{language}}."), "review")
                .AddStep("step2", new PromptTemplate("Summarize: {{review}}"), "summary");

            var analyzer = new PromptChangeImpactAnalyzer(chains: new[] { chain });
            var before = new PromptTemplate("Review {{code}} for {{language}}.");
            var after = new PromptTemplate("Review {{code}}."); // removed language

            var report = analyzer.Analyze("code-review", before, after);

            Assert.Contains(report.AffectedDependents, d => d.DependentType == "Chain");
        }

        // ── Dependency graph tracing ────────────────────────

        [Fact]
        public void Analyze_GraphDependent_TracesDownstream()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode(new PromptNode("root"));
            var child = new PromptNode("child");
            child.DependsOn("root");
            graph.AddNode(child);
            var grandchild = new PromptNode("grandchild");
            grandchild.DependsOn("child");
            graph.AddNode(grandchild);

            var analyzer = new PromptChangeImpactAnalyzer(graph: graph);
            var before = new PromptTemplate("Original {{input}}.");
            var after = new PromptTemplate("Changed completely to something else.");

            var report = analyzer.Analyze("root", before, after);

            Assert.True(report.BlastRadius >= 2); // child + grandchild
            Assert.True(report.CascadeDepth >= 2);
            Assert.Contains(report.AffectedDependents, d => d.Name == "child");
            Assert.Contains(report.AffectedDependents, d => d.Name == "grandchild");
        }

        [Fact]
        public void Analyze_DeepCascade_EscalatesToCritical()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode(new PromptNode("a"));
            var b = new PromptNode("b"); b.DependsOn("a"); graph.AddNode(b);
            var c = new PromptNode("c"); c.DependsOn("b"); graph.AddNode(c);
            var d = new PromptNode("d"); d.DependsOn("c"); graph.AddNode(d);

            var analyzer = new PromptChangeImpactAnalyzer(graph: graph);
            var before = new PromptTemplate("{{x}} prompt.");
            var after = new PromptTemplate("Different prompt.");

            var report = analyzer.Analyze("a", before, after);

            Assert.Equal(ImpactRisk.Critical, report.OverallRisk);
            Assert.True(report.CascadeDepth >= 3);
        }

        // ── Recommendations ─────────────────────────────────

        [Fact]
        public void Analyze_RemovedVariable_RecommendsDeprecation()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("{{role}}: {{task}}");
            var after = new PromptTemplate("{{task}}");

            var report = analyzer.Analyze("test", before, after);

            Assert.Contains(report.Recommendations, r => r.Contains("deprecation") || r.Contains("migration"));
        }

        [Fact]
        public void Analyze_AddedVariable_RecommendsDefaults()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Do {{task}}.");
            var after = new PromptTemplate("Do {{task}} in {{style}}.");

            var report = analyzer.Analyze("test", before, after);

            Assert.Contains(report.Recommendations, r => r.Contains("default") || r.Contains("Default"));
        }

        [Fact]
        public void Analyze_CriticalRisk_RecommendsFeatureFlag()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode(new PromptNode("root"));
            for (int i = 0; i < 6; i++)
            {
                var n = new PromptNode($"dep-{i}");
                n.DependsOn("root");
                graph.AddNode(n);
            }

            var analyzer = new PromptChangeImpactAnalyzer(graph: graph);
            var before = new PromptTemplate("{{x}} old.");
            var after = new PromptTemplate("Totally new.");

            var report = analyzer.Analyze("root", before, after);

            Assert.Equal(ImpactRisk.Critical, report.OverallRisk);
            Assert.Contains(report.Recommendations, r => r.Contains("feature flag") || r.Contains("gradually"));
        }

        // ── Output formats ──────────────────────────────────

        [Fact]
        public void ToText_ContainsSections()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("{{role}}: {{task}}");
            var after = new PromptTemplate("{{task}} in {{format}}");

            var report = analyzer.Analyze("my-prompt", before, after);
            var text = report.ToText();

            Assert.Contains("Impact Report: my-prompt", text);
            Assert.Contains("Changes", text);
            Assert.Contains("Recommendations", text);
        }

        [Fact]
        public void ToJson_ValidJson()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Do {{task}}.");
            var after = new PromptTemplate("Do {{task}} in {{style}}.");

            var report = analyzer.Analyze("test", before, after);
            var json = report.ToJson();

            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal("test", doc.RootElement.GetProperty("promptName").GetString());
            Assert.True(doc.RootElement.GetProperty("blastRadius").GetInt32() >= 0);
        }

        [Fact]
        public void Analyze_NoContextProvided_StillWorks()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Simple {{x}}.");
            var after = new PromptTemplate("Simple {{y}}.");

            var report = analyzer.Analyze("standalone", before, after);

            Assert.NotEmpty(report.Changes);
            Assert.Equal(0, report.BlastRadius);
            Assert.Equal(0, report.CascadeDepth);
            Assert.Equal("standalone", report.PromptName);
        }

        [Fact]
        public void Analyze_LowRiskNoDependent_SafeToDeploy()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Hello world.");
            var after = new PromptTemplate("Hello world!");

            var report = analyzer.Analyze("trivial", before, after);

            Assert.Equal(ImpactRisk.Low, report.OverallRisk);
            Assert.Contains(report.Recommendations, r => r.Contains("Safe to deploy"));
        }

        // ── Multiple changes combined ───────────────────────

        [Fact]
        public void Analyze_MultipleChanges_AllDetected()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("You are a {{role}}. Respond in JSON format with {{detail}}.");
            var after = new PromptTemplate("Generate CSV output about {{topic}} with extra info.");

            var report = analyzer.Analyze("multi", before, after);

            // Should detect: variable removals, variable additions, format change, instruction change
            Assert.True(report.Changes.Count >= 3, $"Expected ≥3 changes, got {report.Changes.Count}");
        }

        [Fact]
        public void DetectChanges_NullBefore_Throws()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            Assert.Throws<ArgumentNullException>(() => analyzer.DetectChanges(null!, new PromptTemplate("x")));
        }

        [Fact]
        public void DetectChanges_NullAfter_Throws()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            Assert.Throws<ArgumentNullException>(() => analyzer.DetectChanges(new PromptTemplate("x"), null!));
        }

        [Fact]
        public void Analyze_WidBlastRadius_EscalatesRisk()
        {
            var library = new PromptLibrary();
            library.Add("a", new PromptTemplate("Use {{shared}}."));
            library.Add("b", new PromptTemplate("Also {{shared}}."));
            library.Add("c", new PromptTemplate("And {{shared}} too."));
            library.Add("target", new PromptTemplate("{{shared}} main."));

            var analyzer = new PromptChangeImpactAnalyzer(library: library);
            var before = new PromptTemplate("{{shared}} original.");
            var after = new PromptTemplate("No variables now.");

            var report = analyzer.Analyze("target", before, after);

            Assert.True(report.BlastRadius >= 3);
            Assert.True(report.OverallRisk >= ImpactRisk.High);
        }

        [Fact]
        public void Report_AnalyzedAt_IsRecent()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var t = new PromptTemplate("{{x}}");
            var report = analyzer.Analyze("timing", t, t);

            Assert.True((DateTimeOffset.UtcNow - report.AnalyzedAt).TotalSeconds < 5);
        }

        [Fact]
        public void Analyze_RenamedVariable_RecommendsUpdate()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Summarize {{document}}.");
            var after = new PromptTemplate("Summarize {{content}}.");

            var report = analyzer.Analyze("test", before, after);

            Assert.Contains(report.Recommendations, r => r.Contains("Renamed") || r.Contains("renamed") || r.Contains("new name") || r.Contains("Update"));
        }

        [Fact]
        public void Analyze_OutputFormatChanged_RecommendsParsers()
        {
            var analyzer = new PromptChangeImpactAnalyzer();
            var before = new PromptTemplate("Give me JSON output.");
            var after = new PromptTemplate("Give me XML output.");

            var report = analyzer.Analyze("test", before, after);

            Assert.Contains(report.Recommendations, r => r.Contains("parser") || r.Contains("format") || r.Contains("Format"));
        }
    }
}
