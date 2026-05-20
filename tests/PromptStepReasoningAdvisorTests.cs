namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using Prompt;
    using Xunit;

    public class PromptStepReasoningAdvisorTests
    {
        private static readonly DateTime FixedNow = new(2026, 5, 20, 15, 44, 0, DateTimeKind.Utc);

        private static PromptStepReasoningAdvisor MakeAdvisor()
            => new(() => FixedNow);

        private static StepReasoningOptions Opts(
            StepReasoningRiskAppetite r = StepReasoningRiskAppetite.Balanced,
            bool skipSelfCheck = false)
            => new() { RiskAppetite = r, SkipSelfCheckCheck = skipSelfCheck };

        [Fact]
        public void EmptyPrompt_NoFindings_GradeA()
        {
            var rpt = MakeAdvisor().Analyze("", Opts());
            Assert.Empty(rpt.Findings);
            Assert.Equal('A', rpt.Grade);
            Assert.Equal(100, rpt.ReasoningScore);
            Assert.Equal(StepReasoningVerdict.Ready, rpt.Verdict);
            Assert.Contains(rpt.Playbook, a => a.Id == "STEP_REASONING_OK");
            Assert.Equal(FixedNow, rpt.GeneratedAt);
        }

        [Fact]
        public void ComplexTaskWithoutCoT_FiresMissingReasoningGuidance()
        {
            var rpt = MakeAdvisor().Analyze(
                "Calculate the optimal allocation across the four portfolios; compare the two strategies and pick a winner.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("MISSING_REASONING_GUIDANCE", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "DECLARE_REASONING_PROTOCOL");
        }

        [Fact]
        public void TrivialTaskWithCoT_FiresOverprescribedReasoning()
        {
            var rpt = MakeAdvisor().Analyze(
                "Classify this sentence sentiment as positive or negative. Think step by step. ## Answer",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("OVERPRESCRIBED_REASONING", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "REMOVE_OVERPRESCRIBED_COT");
        }

        [Fact]
        public void CoTWithoutFinalAnswerDelimiter_FiresFinding()
        {
            var rpt = MakeAdvisor().Analyze(
                "Think step by step and reason through the problem. Stop when you have a numeric result.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("NO_FINAL_ANSWER_DELIMITER", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "ADD_FINAL_ANSWER_DELIMITER");
        }

        [Fact]
        public void ShowYourWorkWithoutFormat_FiresUnspecifiedFormat()
        {
            var rpt = MakeAdvisor().Analyze(
                "Solve this equation. Show your work. Final answer: <value>. Stop when solved.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("UNSPECIFIED_REASONING_FORMAT", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "SPECIFY_REASONING_FORMAT");
        }

        [Fact]
        public void ConciseAndCoT_FiresConflict()
        {
            var rpt = MakeAdvisor().Analyze(
                "Be concise. Think step by step. ## Answer. Stop when finished.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("CONFLICTING_REASONING_DIRECTIVES", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "RESOLVE_REASONING_CONFLICT");
        }

        [Fact]
        public void VagueStepScaffold_FiresWhenNoConcreteSteps()
        {
            var rpt = MakeAdvisor().Analyze(
                "First, look at the data, then process it, and finally summarize. Respond in English.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("VAGUE_STEP_SCAFFOLD", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "LIST_CONCRETE_STEPS");
        }

        [Fact]
        public void VagueStepScaffold_NotFiredWhenConcreteStepsListed()
        {
            var rpt = MakeAdvisor().Analyze(
                "Step 1: parse the input.\nStep 2: validate the schema.\nStep 3: emit JSON.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.DoesNotContain("VAGUE_STEP_SCAFFOLD", codes);
        }

        [Fact]
        public void LatencyAndCoT_TriggersBlockSendAndForcedF()
        {
            var rpt = MakeAdvisor().Analyze(
                "This is a real-time response system. Think step by step about the user's request and walk through your reasoning. ## Answer",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("LATENCY_VS_REASONING_CONFLICT", codes);
            Assert.Equal(StepReasoningVerdict.BlockSend, rpt.Verdict);
            Assert.Equal('F', rpt.Grade);
            Assert.Contains(rpt.Playbook, a => a.Id == "RESOLVE_LATENCY_VS_REASONING" && a.Priority == StepReasoningPriority.P0);
        }

        [Fact]
        public void SelfCheckWithoutCriteria_FiresUngroundedSelfCheck()
        {
            var rpt = MakeAdvisor().Analyze(
                "Provide an answer. Double-check your answer before responding. Respond in English.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("UNGROUNDED_SELF_CHECK", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "ADD_SELF_CHECK_CRITERIA");
        }

        [Fact]
        public void SelfCheckWithCriteria_NotFired()
        {
            var rpt = MakeAdvisor().Analyze(
                "Provide an answer. Double-check your answer against the rules listed above.",
                Opts());
            Assert.DoesNotContain("UNGROUNDED_SELF_CHECK", rpt.Findings.Select(f => f.Code));
        }

        [Fact]
        public void SkipSelfCheckOption_SuppressesFinding()
        {
            var rpt = MakeAdvisor().Analyze(
                "Provide an answer. Double-check your answer before responding.",
                Opts(skipSelfCheck: true));
            Assert.DoesNotContain("UNGROUNDED_SELF_CHECK", rpt.Findings.Select(f => f.Code));
        }

        [Fact]
        public void AnswerOnlyForComplexTask_TriggersBlockSendAndForcedF()
        {
            var rpt = MakeAdvisor().Analyze(
                "Calculate the integral and compare it to the analytic solution. Just the answer, do not explain.",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("REASONING_SUPPRESSED_FOR_COMPLEX_TASK", codes);
            Assert.Equal(StepReasoningVerdict.BlockSend, rpt.Verdict);
            Assert.Equal('F', rpt.Grade);
            Assert.Contains(rpt.Playbook, a => a.Id == "REENABLE_REASONING_FOR_COMPLEX" && a.Priority == StepReasoningPriority.P0);
        }

        [Fact]
        public void NoStopCondition_FiresWhenCoTPresent()
        {
            var rpt = MakeAdvisor().Analyze(
                "Think step by step. Show your work in numbered steps. ## Answer",
                Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("NO_STOP_CONDITION_FOR_THOUGHT", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "ADD_REASONING_STOP_CONDITION");
        }

        [Fact]
        public void CautiousAppetite_AddsSecondReviewerOnNonPassingGrade()
        {
            var rpt = MakeAdvisor().Analyze(
                "Calculate the answer.",
                Opts(StepReasoningRiskAppetite.Cautious));
            // Cautious multiplier pushes a missing-reasoning prompt down to C/D/F.
            Assert.Contains(rpt.Playbook, a => a.Id == "SECOND_REVIEWER");
            Assert.True(rpt.Grade == 'C' || rpt.Grade == 'D' || rpt.Grade == 'F');
        }

        [Fact]
        public void AggressiveAppetite_TrimsP3FallbackWhenHigherPriorityPresent()
        {
            var rpt = MakeAdvisor().Analyze(
                "Calculate the answer.",
                Opts(StepReasoningRiskAppetite.Aggressive));
            Assert.DoesNotContain(rpt.Playbook, a => a.Priority == StepReasoningPriority.P3);
            Assert.Contains(rpt.Playbook, a => a.Priority < StepReasoningPriority.P3);
        }

        [Fact]
        public void TightenedDraft_AppendsReasoningContractBlock()
        {
            var rpt = MakeAdvisor().Analyze(
                "Calculate the optimal allocation.",
                Opts());
            Assert.Contains("# Reasoning Contract", rpt.StepReasoningTightenedDraft);
            Assert.Contains("MISSING_REASONING_GUIDANCE", rpt.StepReasoningTightenedDraft);
        }

        [Fact]
        public void Json_RoundTripsCleanly()
        {
            var rpt = MakeAdvisor().Analyze("Calculate the answer.", Opts());
            var json = rpt.ToJson();
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("Verdict", out var v));
            Assert.True(doc.RootElement.TryGetProperty("Findings", out _));
            // Enum-as-string verification
            Assert.Equal(JsonValueKind.String, v.ValueKind);
        }

        [Fact]
        public void Markdown_HasExpectedHeaders()
        {
            var rpt = MakeAdvisor().Analyze("Calculate the answer.", Opts());
            var md = rpt.ToMarkdown();
            Assert.Contains("# Reasoning Contract Audit", md);
            Assert.Contains("## Findings", md);
            Assert.Contains("## Playbook", md);
            Assert.Contains("## Insights", md);
        }

        [Fact]
        public void Deterministic_TwoRunsSameOutput()
        {
            var a = MakeAdvisor().Analyze("Calculate the answer.", Opts());
            var b = MakeAdvisor().Analyze("Calculate the answer.", Opts());
            Assert.Equal(a.ReasoningScore, b.ReasoningScore);
            Assert.Equal(a.Grade, b.Grade);
            Assert.Equal(a.ToJson(), b.ToJson());
            Assert.Equal(FixedNow, a.GeneratedAt);
        }
    }
}
