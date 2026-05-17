namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Prompt;
    using Xunit;

    public class PromptExampleQualityAdvisorTests
    {
        private static readonly DateTime FixedNow = new(2026, 5, 17, 17, 0, 0, DateTimeKind.Utc);

        private static PromptExampleQualityAdvisor MakeAdvisor(ExampleRiskAppetite appetite = ExampleRiskAppetite.Balanced)
            => new(() => FixedNow) { RiskAppetite = appetite };

        private static QualityExample Ex(string id, string input, string output, string? label = null)
            => new() { Id = id, Input = input, Output = output, Label = label };

        [Fact]
        public void EmptyList_IsHealthy()
        {
            var rpt = MakeAdvisor().Analyze(Array.Empty<QualityExample>());
            Assert.Equal('A', rpt.Grade);
            Assert.Equal(0, rpt.OverallScore);
            Assert.Equal(ExampleQualityRisk.Healthy, rpt.Verdict);
            Assert.Empty(rpt.Findings);
            Assert.Contains("reordered=false", rpt.Curated.Note);
        }

        [Fact]
        public void SingleCleanExample_HealthyAndSetTooSmall()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "What is 2+2?", "4"),
            });
            Assert.Equal('A', rpt.Grade);
            Assert.Equal(ExampleQualityRisk.Healthy, rpt.Verdict);
            Assert.Contains(rpt.Insights, i => i.StartsWith("set_too_small"));
        }

        [Fact]
        public void NearDuplicate_TriggersRemoveAndDropsLater()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "The quick brown fox jumps over the lazy dog at noon", "Animal motion sentence."),
                Ex("b", "The quick brown fox jumps over the lazy dog at noon", "Animal motion sentence."),
                Ex("c", "Photosynthesis converts sunlight into chemical energy in plants.", "Bio process."),
            });
            Assert.Contains(rpt.Findings, f => f.Code == "NEAR_DUPLICATE" && f.ExampleId == "b");
            Assert.Contains(rpt.Assessments, a => a.ExampleId == "b" && a.Verdict == ExampleVerdict.Remove && a.Priority == ExamplePriority.P0);
            Assert.Contains(rpt.Playbook, p => p.Code == "REMOVE_DUPLICATES" && p.Priority == ExamplePriority.P0);
            Assert.Contains("b", rpt.Curated.RemovedExampleIds);
            Assert.DoesNotContain(rpt.Curated.Examples, e => e.Id == "b");
        }

        [Fact]
        public void ContradictoryExamples_ForceGradeF()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "Classify: I love it", "positive"),
                Ex("b", "Classify: I love it", "negative"),
            });
            Assert.Contains(rpt.Findings, f => f.Code == "CONTRADICTORY_EXAMPLE");
            Assert.Equal('F', rpt.Grade);
            Assert.Contains(rpt.Playbook, p => p.Code == "RESOLVE_CONTRADICTIONS" && p.Priority == ExamplePriority.P0);
        }

        [Fact]
        public void EmptyOutput_IsDroppedFromCurated()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "Question?", ""),
                Ex("b", "Other question?", "Answer."),
            });
            Assert.Contains(rpt.Findings, f => f.Code == "EMPTY_FIELD" && f.ExampleId == "a");
            Assert.Contains("a", rpt.Curated.RemovedExampleIds);
            Assert.Contains(rpt.Playbook, p => p.Code == "BACKFILL_REQUIRED_FIELDS");
        }

        [Fact]
        public void LabelLeakage_TriggersRewriteAction()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "The sentiment is positive in this review.", "positive"),
                Ex("b", "Another review entirely.", "neutral"),
            });
            Assert.Contains(rpt.Findings, f => f.Code == "LABEL_LEAKAGE" && f.ExampleId == "a");
            Assert.Contains(rpt.Playbook, p => p.Code == "REWRITE_LEAKED_OUTPUTS" && p.Priority == ExamplePriority.P1);
        }

        [Fact]
        public void FormatInconsistency_TriggersNormalizeAction()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "x", "{\"k\":1}"),
                Ex("b", "y", "{\"k\":2}"),
                Ex("c", "z", "A long natural language sentence describing the answer."),
                Ex("d", "w", "Another long natural language sentence with different words."),
            });
            Assert.Contains(rpt.Findings, f => f.Code == "FORMAT_INCONSISTENCY");
            Assert.Contains(rpt.Playbook, p => p.Code == "NORMALIZE_OUTPUT_FORMAT");
        }

        [Fact]
        public void LabelImbalance_TriggersAddMinority()
        {
            var examples = Enumerable.Range(0, 8).Select(i => Ex($"a{i}", $"text {i} alpha beta gamma delta epsilon zeta", "positive sentiment classification", "pos")).ToList();
            examples.Add(Ex("b0", "text 8 different vocabulary here completely", "negative case", "neg"));
            examples.Add(Ex("b1", "text 9 yet another batch of words", "negative case here", "neg"));
            var rpt = MakeAdvisor().Analyze(examples);
            Assert.Contains(rpt.Findings, f => f.Code == "LABEL_IMBALANCE");
            Assert.Contains(rpt.Playbook, p => p.Code == "ADD_MINORITY_EXAMPLES" && p.Priority == ExamplePriority.P1);
        }

        [Fact]
        public void LengthVariance_TriggersTightenAction_AndAggressiveTrimsIt()
        {
            var giant = string.Join(" ", Enumerable.Range(0, 200).Select(i => $"word{i}"));
            var examples = new[]
            {
                Ex("a", "short input alpha", "short out"),
                Ex("b", "short input beta", "short out two"),
                Ex("c", "short input gamma", "short out three"),
                Ex("d", giant, giant),
            };
            var rpt = MakeAdvisor().Analyze(examples);
            Assert.Contains(rpt.Findings, f => f.Code == "LENGTH_VARIANCE");
            Assert.Contains(rpt.Playbook, p => p.Code == "TIGHTEN_LENGTH_RANGE" && p.Priority == ExamplePriority.P2);

            var aggressive = MakeAdvisor(ExampleRiskAppetite.Aggressive).Analyze(examples);
            Assert.DoesNotContain(aggressive.Playbook, p => p.Code == "TIGHTEN_LENGTH_RANGE");
        }

        [Fact]
        public void OrderRecencyBias_TriggersReorder()
        {
            var examples = new[]
            {
                Ex("a", "alpha cat",  "noun", "n"),
                Ex("b", "beta dog",   "noun", "n"),
                Ex("c", "gamma run",  "verb", "v"),
                Ex("d", "delta jump", "verb", "v"),
                Ex("e", "epsilon swim","verb", "v"),
                Ex("f", "zeta climb", "verb", "v"),
            };
            var rpt = MakeAdvisor().Analyze(examples);
            Assert.Contains(rpt.Findings, f => f.Code == "ORDER_RECENCY_BIAS");
            Assert.Contains(rpt.Playbook, p => p.Code == "REORDER_EXAMPLES");
            Assert.EndsWith("reordered=true", rpt.Curated.Note);
        }

        [Fact]
        public void LowDiversity_TriggersAddDiverse()
        {
            var examples = new[]
            {
                Ex("a", "alpha beta gamma delta epsilon", "alpha beta gamma delta zeta"),
                Ex("b", "alpha beta gamma delta epsilon", "alpha beta gamma delta eta"),
                Ex("c", "alpha beta gamma delta epsilon", "alpha beta gamma delta theta"),
            };
            var rpt = MakeAdvisor().Analyze(examples);
            Assert.Contains(rpt.Findings, f => f.Code == "LOW_DIVERSITY");
            Assert.Contains(rpt.Playbook, p => p.Code == "ADD_DIVERSE_EXAMPLES");
        }

        [Fact]
        public void AmbiguousDemonstration_TriggersClarify()
        {
            var examples = new[]
            {
                Ex("a", "alpha beta gamma delta", "completely unrelated banana orange grape kiwi"),
                Ex("b", "moo cow horse pig", "barn farm tractor field"),
                Ex("c", "Question here please", "Answer reuses question and please vocabulary"),
                Ex("d", "Different content used", "Different content reused once again"),
            };
            var rpt = MakeAdvisor().Analyze(examples);
            Assert.Contains(rpt.Findings, f => f.Code == "AMBIGUOUS_DEMONSTRATION" && f.ExampleId == "a");
            Assert.Contains(rpt.Playbook, p => p.Code == "CLARIFY_DEMONSTRATIONS" && p.Priority == ExamplePriority.P2);
        }

        [Fact]
        public void RiskAppetite_ShiftsOverallScore()
        {
            var examples = new[]
            {
                Ex("a", "alpha beta gamma delta", "completely unrelated banana orange grape kiwi"),
                Ex("b", "moo cow horse pig", "barn farm tractor field"),
                Ex("c", "Question here please", "Answer reuses question and please vocabulary"),
            };
            var cautious = MakeAdvisor(ExampleRiskAppetite.Cautious).Analyze(examples);
            var balanced = MakeAdvisor(ExampleRiskAppetite.Balanced).Analyze(examples);
            var aggressive = MakeAdvisor(ExampleRiskAppetite.Aggressive).Analyze(examples);
            Assert.True(cautious.OverallScore >= balanced.OverallScore);
            Assert.True(balanced.OverallScore >= aggressive.OverallScore);
        }

        [Fact]
        public void ToJson_IsDeterministic()
        {
            var examples = new[]
            {
                Ex("a", "Hello world", "{\"greeting\": true}"),
                Ex("b", "Goodbye world", "Long answer text describing the farewell."),
            };
            var advisor = MakeAdvisor();
            var x = advisor.Analyze(examples).ToJson();
            var y = advisor.Analyze(examples).ToJson();
            Assert.Equal(x, y);
        }

        [Fact]
        public void ForcedF_FromManyDuplicatesAndLeakage()
        {
            var dupBody = "the quick brown fox jumps over the lazy dog at noon";
            var examples = new[]
            {
                Ex("a", dupBody, "X"),
                Ex("b", dupBody, "X"),
                Ex("c", "The sentiment is happy in this review.", "happy"),
                Ex("d", "Distinct sentence about photosynthesis and plants.", "biology"),
            };
            var rpt = MakeAdvisor().Analyze(examples);
            Assert.True(rpt.Findings.Any(f => f.Code == "NEAR_DUPLICATE"));
            Assert.True(rpt.Findings.Any(f => f.Code == "LABEL_LEAKAGE"));
            Assert.Equal('F', rpt.Grade);
        }

        [Fact]
        public void Markdown_ContainsHeaderAndPlaybookSections()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "Q1", ""),
                Ex("b", "Q2", "A2"),
            });
            var md = rpt.ToMarkdown();
            Assert.Contains("## PromptExampleQualityAdvisor", md);
            Assert.Contains("### Playbook", md);
            Assert.Contains("BACKFILL_REQUIRED_FIELDS", md);
        }

        [Fact]
        public void Playbook_OrderedP0First()
        {
            var rpt = MakeAdvisor().Analyze(new[]
            {
                Ex("a", "Q", ""),
                Ex("b", "The sentiment is positive in this text.", "positive"),
                Ex("c", "Another sentence entirely about nothing related.", "negative"),
            });
            for (int i = 1; i < rpt.Playbook.Count; i++)
            {
                Assert.True((int)rpt.Playbook[i].Priority >= (int)rpt.Playbook[i - 1].Priority);
            }
        }
    }
}
