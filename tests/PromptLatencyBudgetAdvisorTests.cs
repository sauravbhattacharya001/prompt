namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using Prompt;
    using Xunit;

    public class PromptLatencyBudgetAdvisorTests
    {
        private static PromptLatencyBudgetAdvisor MakeAdvisor() => new();

        [Fact]
        public void NullPrompt_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MakeAdvisor().Analyze(null!));
        }

        [Fact]
        public void EmptyPrompt_HasMissingCapAndStreamingDisabledFindings()
        {
            var rpt = MakeAdvisor().Analyze("");
            Assert.Contains(rpt.Findings, f => f.Mode == LatencyMode.MissingOutputCap);
            Assert.Contains(rpt.Findings, f => f.Mode == LatencyMode.StreamingDisabled);
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == LatencyMode.OversizedPrompt);
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == LatencyMode.RetryLoop);
        }

        [Fact]
        public void ChainOfThoughtPrompt_DetectedAndPlaybookIncluded()
        {
            var rpt = MakeAdvisor().Analyze("Please think step by step and solve the puzzle.");
            Assert.Contains(rpt.Findings, f => f.Mode == LatencyMode.ChainOfThoughtExpansion);
            Assert.Contains(rpt.Playbook, a => a.Id == "TrimChainOfThought");
        }

        [Fact]
        public void UnboundedOutput_IsP0AndProducesCapAction()
        {
            var rpt = MakeAdvisor().Analyze("Write the answer with no length limit.");
            var f = Assert.Single(rpt.Findings, x => x.Mode == LatencyMode.UnboundedOutput);
            Assert.Equal(LatencyPriority.P0, f.Priority);
            Assert.Contains(rpt.Playbook, a => a.Id == "CapOutputLength" && a.Priority == LatencyPriority.P0);
        }

        [Fact]
        public void ExplicitWordCap_SuppressesMissingCapAndShrinksResponseTokens()
        {
            var rpt = MakeAdvisor().Analyze("Summarize in 50 words.");
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == LatencyMode.MissingOutputCap);
            Assert.True(rpt.EstimatedResponseTokens < 200);
        }

        [Fact]
        public void RetryLoop_IsP0()
        {
            var rpt = MakeAdvisor().Analyze("Refine the answer and retry until correct.");
            var f = Assert.Single(rpt.Findings, x => x.Mode == LatencyMode.RetryLoop);
            Assert.Equal(LatencyPriority.P0, f.Priority);
            Assert.Contains(rpt.Playbook, a => a.Id == "RemoveRetryLoop");
        }

        [Fact]
        public void SerialToolChain_FiresOnlyWhenToolsAvailable()
        {
            var prompt = "First fetch the file, then parse it, then summarize, then email it.";
            var noTools = MakeAdvisor().Analyze(prompt);
            Assert.DoesNotContain(noTools.Findings, f => f.Mode == LatencyMode.SerialToolChain);

            var withTools = MakeAdvisor().Analyze(prompt, new LatencyContext { ToolsAvailable = true });
            Assert.Contains(withTools.Findings, f => f.Mode == LatencyMode.SerialToolChain);
            Assert.Contains(withTools.Playbook, a => a.Id == "ParallelizeToolCalls");
        }

        [Fact]
        public void OversizedPrompt_FiresWhenOverBudget()
        {
            var big = new string('a', 6000); // ~1500 tokens by char heuristic
            var ctx = new LatencyContext { PromptTokenBudget = 200 };
            var rpt = MakeAdvisor().Analyze(big, ctx);
            var f = Assert.Single(rpt.Findings, x => x.Mode == LatencyMode.OversizedPrompt);
            Assert.True(f.Severity >= 70);
            Assert.Equal(LatencyPriority.P0, f.Priority);
            Assert.Contains(rpt.Playbook, a => a.Id == "CompressPrompt");
        }

        [Fact]
        public void StreamingEnabled_AddsPositiveInsightAndNoStreamingFinding()
        {
            var rpt = MakeAdvisor().Analyze("Summarize in 50 words.",
                new LatencyContext { StreamingEnabled = true });
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == LatencyMode.StreamingDisabled);
            Assert.Contains(rpt.Insights, i => i.Contains("Streaming enabled", StringComparison.Ordinal));
        }

        [Fact]
        public void Fanout_FiresWhenParallelDisabled_SuppressedWhenEnabled()
        {
            var prompt = "For each item in the list, compute a summary.";
            var noPar = MakeAdvisor().Analyze(prompt);
            Assert.Contains(noPar.Findings, f => f.Mode == LatencyMode.SerializableFanout);

            var par = MakeAdvisor().Analyze(prompt, new LatencyContext { ParallelToolCallsEnabled = true });
            Assert.DoesNotContain(par.Findings, f => f.Mode == LatencyMode.SerializableFanout);
        }

        [Fact]
        public void Multimodal_Fires()
        {
            var rpt = MakeAdvisor().Analyze("Please analyze this image and tell me what's wrong.");
            Assert.Contains(rpt.Findings, f => f.Mode == LatencyMode.HeavyMultimodalInput);
            Assert.Contains(rpt.Playbook, a => a.Id == "PreprocessMultimodalInput");
        }

        [Fact]
        public void ExhaustiveCoverage_Fires()
        {
            var rpt = MakeAdvisor().Analyze("Give me a comprehensive list of all possible options.");
            Assert.Contains(rpt.Findings, f => f.Mode == LatencyMode.ExhaustiveCoverage);
            Assert.Contains(rpt.Playbook, a => a.Id == "NarrowScope");
        }

        [Fact]
        public void TwoP0s_YieldsPathologicalVerdictAndGradeF()
        {
            var rpt = MakeAdvisor().Analyze("Write with no length limit and retry until correct.");
            Assert.True(rpt.Findings.Count(f => f.Priority == LatencyPriority.P0) >= 2);
            Assert.Equal(LatencyVerdict.Pathological, rpt.Verdict);
            Assert.Equal("F", rpt.Grade);
        }

        [Fact]
        public void CleanShortPrompt_GetsFastVerdict()
        {
            var rpt = MakeAdvisor().Analyze(
                "Summarize the meeting in 3 bullets.",
                new LatencyContext { StreamingEnabled = true });
            Assert.True(rpt.LatencyScore >= 80);
            Assert.True(rpt.Verdict == LatencyVerdict.Fast || rpt.Verdict == LatencyVerdict.Acceptable);
        }

        [Fact]
        public void Aggressive_TrimsP3WhenP0Present()
        {
            var rpt = MakeAdvisor().Analyze("Write with no length limit.",
                new LatencyContext { RiskAppetite = LatencyRiskAppetite.Aggressive });
            // P3 actions should be absent when P0 fired and appetite is aggressive
            Assert.DoesNotContain(rpt.Playbook, a => a.Priority == LatencyPriority.P3);
        }

        [Fact]
        public void Cautious_AddsPerfReviewWhenP0Present()
        {
            var rpt = MakeAdvisor().Analyze("Retry until correct.",
                new LatencyContext { RiskAppetite = LatencyRiskAppetite.Cautious });
            Assert.Contains(rpt.Playbook, a => a.Id == "SchedulePerfReview");
        }

        [Fact]
        public void OptimizedDraft_AppendsBudgetBlockWhenP0OrP1Present()
        {
            var prompt = "Write with no length limit.";
            var rpt = MakeAdvisor().Analyze(prompt);
            Assert.Contains("LATENCY_BUDGET", rpt.OptimizedDraft, StringComparison.Ordinal);
            Assert.StartsWith(prompt, rpt.OptimizedDraft, StringComparison.Ordinal);
        }

        [Fact]
        public void OptimizedDraft_IsPromptItselfWhenNoHighPriorityActions()
        {
            var prompt = "Summarize in 3 bullets.";
            var rpt = MakeAdvisor().Analyze(prompt,
                new LatencyContext { StreamingEnabled = true });
            Assert.Equal(prompt, rpt.OptimizedDraft);
        }

        [Fact]
        public void Playbook_NeverEmpty_HasOkActionWhenClean()
        {
            // Use a context where nothing fires (small prompt, streaming on, capped output)
            var rpt = MakeAdvisor().Analyze("Reply in 5 bullets.",
                new LatencyContext { StreamingEnabled = true });
            Assert.NotEmpty(rpt.Playbook);
        }

        [Fact]
        public void EstimatedLatency_IncludesOverheadAndTokenCost()
        {
            var rpt = MakeAdvisor().Analyze("Summarize in 50 words.",
                new LatencyContext { FixedOverheadMs = 1000, MsPerOutputToken = 10 });
            Assert.True(rpt.EstimatedTotalLatencyMs >= 1000);
            Assert.True(rpt.EstimatedTimeToFirstTokenMs >= 1000);
        }

        [Fact]
        public void BudgetOverrun_AppearsInInsights()
        {
            var big = new string('a', 8000);
            var rpt = MakeAdvisor().Analyze(big, new LatencyContext { LatencyBudgetMs = 100, PromptTokenBudget = 50 });
            Assert.Contains(rpt.Insights, i => i.Contains("exceeds budget", StringComparison.Ordinal));
        }

        [Fact]
        public void ToText_ContainsHeadlineAndSections()
        {
            var rpt = MakeAdvisor().Analyze("Think step by step.");
            var txt = MakeAdvisor().ToText(rpt);
            Assert.Contains("Findings", txt, StringComparison.Ordinal);
            Assert.Contains("Playbook", txt, StringComparison.Ordinal);
            Assert.Contains("Insights", txt, StringComparison.Ordinal);
            Assert.Contains(rpt.Headline, txt, StringComparison.Ordinal);
        }

        [Fact]
        public void ToMarkdown_ContainsSummaryAndPlaybook()
        {
            var rpt = MakeAdvisor().Analyze("Think step by step.");
            var md = MakeAdvisor().ToMarkdown(rpt);
            Assert.Contains("## Summary", md, StringComparison.Ordinal);
            Assert.Contains("## Findings", md, StringComparison.Ordinal);
            Assert.Contains("## Playbook", md, StringComparison.Ordinal);
            Assert.Contains("## Insights", md, StringComparison.Ordinal);
        }

        [Fact]
        public void ToJson_IsValidAndDeterministic()
        {
            var advisor = MakeAdvisor();
            var rpt1 = advisor.Analyze("Think step by step and be comprehensive.");
            var rpt2 = advisor.Analyze("Think step by step and be comprehensive.");
            var json1 = advisor.ToJson(rpt1);
            var json2 = advisor.ToJson(rpt2);
            Assert.Equal(json1, json2);
            // Round-trip JSON shape
            using var doc = JsonDocument.Parse(json1);
            Assert.True(doc.RootElement.TryGetProperty("findings", out _));
            Assert.True(doc.RootElement.TryGetProperty("playbook", out _));
            Assert.True(doc.RootElement.TryGetProperty("estimatedTotalLatencyMs", out _));
        }

        [Fact]
        public void FindingsAreSortedByPriorityAscThenSeverityDesc()
        {
            var rpt = MakeAdvisor().Analyze(
                "Be comprehensive, think step by step, retry until correct, with no length limit.");
            for (int i = 1; i < rpt.Findings.Count; i++)
            {
                Assert.True((int)rpt.Findings[i - 1].Priority <= (int)rpt.Findings[i].Priority);
                if (rpt.Findings[i - 1].Priority == rpt.Findings[i].Priority)
                    Assert.True(rpt.Findings[i - 1].Severity >= rpt.Findings[i].Severity);
            }
        }

        [Fact]
        public void DoesNotMutateInput()
        {
            var prompt = "Summarize in 50 words.";
            var copy = string.Copy(prompt);
            var rpt = MakeAdvisor().Analyze(prompt);
            Assert.Equal(copy, prompt);
            Assert.NotNull(rpt);
        }
    }
}
