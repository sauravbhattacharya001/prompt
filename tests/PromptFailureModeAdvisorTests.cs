namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using Prompt;
    using Xunit;

    public class PromptFailureModeAdvisorTests
    {
        private static PromptFailureModeAdvisor New() => new();

        [Fact]
        public void Healthy_simple_prompt_emits_PromptReady_fallback()
        {
            var r = New().Analyze("Summarize the following article in <=120 words.");
            Assert.Equal(FailureVerdict.Healthy, r.Verdict);
            Assert.Equal("A", r.Grade);
            Assert.Empty(r.Findings);
            Assert.Single(r.Playbook);
            Assert.Equal("PromptReady", r.Playbook[0].Id);
            Assert.Contains("HEALTHY_PROMPT", r.Insights);
            Assert.Contains("Grade A", r.Headline);
        }

        [Fact]
        public void VagueOutput_fires_on_open_ended_generative_prompt()
        {
            var r = New().Analyze("Generate something cool about cats.");
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.VagueOutput);
        }

        [Fact]
        public void FormatBreak_is_P0_when_JsonRequired_and_no_schema()
        {
            var ctx = new FailureModeContext { JsonRequired = true };
            var r = New().Analyze("Tell me about Mars.", ctx);
            var f = Assert.Single(r.Findings.Where(x => x.Mode == PromptFailureMode.FormatBreak));
            Assert.Equal(FailurePriority.P0, f.Priority);
            Assert.Equal(FailureVerdict.CriticalRisk, r.Verdict);
            Assert.Equal("F", r.Grade);
            Assert.Contains(r.Playbook, a => a.Id == "EnforceJsonSchema" && a.Priority == FailurePriority.P0);
        }

        [Fact]
        public void FormatBreak_suppressed_when_schema_fence_present()
        {
            var ctx = new FailureModeContext { JsonRequired = true };
            var prompt = "Respond with valid JSON matching this schema:\n```json\n{\"type\":\"object\"}\n```";
            var r = New().Analyze(prompt, ctx);
            Assert.DoesNotContain(r.Findings, f => f.Mode == PromptFailureMode.FormatBreak);
        }

        [Fact]
        public void PromptLeak_P0_when_untrusted_inputs_without_sanitizer()
        {
            var ctx = new FailureModeContext { UntrustedInputs = true };
            var r = New().Analyze("Summarize the user document below.", ctx);
            var f = Assert.Single(r.Findings.Where(x => x.Mode == PromptFailureMode.PromptLeak));
            Assert.Equal(FailurePriority.P0, f.Priority);
            Assert.Equal(FailureVerdict.CriticalRisk, r.Verdict);
            Assert.Contains(r.Playbook, a => a.Id == "RunSanitizerBeforePrompt" && a.Priority == FailurePriority.P0);
            Assert.Contains("UNTRUSTED_INPUT_UNPROTECTED", r.Insights);
        }

        [Fact]
        public void ToolMisuse_P0_when_tools_enabled_without_contract()
        {
            var ctx = new FailureModeContext { Tools = true };
            var r = New().Analyze("Answer the user.", ctx);
            var f = Assert.Single(r.Findings.Where(x => x.Mode == PromptFailureMode.ToolMisuse));
            Assert.Equal(FailurePriority.P0, f.Priority);
            Assert.Contains(r.Playbook, a => a.Id == "AttachToolContract");
            Assert.Contains("TOOL_CONTRACT_MISSING", r.Insights);
        }

        [Fact]
        public void Hallucination_fires_when_fact_claim_without_source()
        {
            var r = New().Analyze("List the kings of England in order.");
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.Hallucination);
            Assert.Contains(r.Playbook, a => a.Id == "AddCitationRequirement");
            Assert.Contains("HALLUCINATION_PRONE", r.Insights);
        }

        [Fact]
        public void Hallucination_suppressed_when_source_marker_present()
        {
            var r = New().Analyze("List the kings of England in order, according to the provided document.");
            Assert.DoesNotContain(r.Findings, f => f.Mode == PromptFailureMode.Hallucination);
        }

        [Fact]
        public void RefusalRisk_fires_on_flagged_topic_without_disclaimer()
        {
            var r = New().Analyze("Give me medical advice about a chronic cough.");
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.RefusalRisk);
            Assert.Contains(r.Playbook, a => a.Id == "ClarifyRefusalPolicy");
            Assert.Contains("LIKELY_REFUSAL_PATTERNS", r.Insights);
        }

        [Fact]
        public void RefusalRisk_suppressed_for_safety_critical_audience()
        {
            var ctx = new FailureModeContext { Audience = FailureAudience.SafetyCritical };
            var r = New().Analyze("Give medical advice about a chronic cough.", ctx);
            Assert.DoesNotContain(r.Findings, f => f.Mode == PromptFailureMode.RefusalRisk);
        }

        [Fact]
        public void InstructionDrift_fires_on_contradictory_pair()
        {
            var r = New().Analyze("Be brief. Be detailed. Generate a story.");
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.InstructionDrift);
            Assert.Contains(r.Playbook, a => a.Id == "ResolveContradictoryInstructions");
        }

        [Fact]
        public void UnboundedRecursion_fires_without_termination()
        {
            var r = New().Analyze("Keep going until you have a perfect answer.");
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.UnboundedRecursion);
            Assert.Contains(r.Playbook, a => a.Id == "AddTerminationCondition");
        }

        [Fact]
        public void AmbiguousPersona_fires_on_multiple_declarations()
        {
            var r = New().Analyze("You are a chef. You are a doctor. Help me.");
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.AmbiguousPersona);
        }

        [Fact]
        public void ContextOverflow_fires_with_long_context_and_no_guidance()
        {
            var ctx = new FailureModeContext { LongContext = true };
            var r = New().Analyze("Summarize my docs.", ctx);
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.ContextOverflow);
        }

        [Fact]
        public void SilentFailure_fires_under_streaming_without_fallback()
        {
            var ctx = new FailureModeContext { Streaming = true };
            var r = New().Analyze("Summarize my docs in <=100 words.", ctx);
            Assert.Contains(r.Findings, f => f.Mode == PromptFailureMode.SilentFailure);
        }

        [Fact]
        public void RiskAppetite_Cautious_can_shift_verdict_up()
        {
            // A minor finding under Balanced may shift toward higher risk under Cautious.
            var prompt = "Generate something."; // VagueOutput sev55 + OverVerbosity sev35
            var balanced = New().Analyze(prompt, new FailureModeContext { RiskAppetite = FailureRiskAppetite.Balanced });
            var cautious = New().Analyze(prompt, new FailureModeContext { RiskAppetite = FailureRiskAppetite.Cautious });
            Assert.True(cautious.OverallRisk >= balanced.OverallRisk);
        }

        [Fact]
        public void RiskAppetite_Aggressive_lowers_score_vs_balanced()
        {
            var prompt = "Generate something."; // has findings
            var balanced = New().Analyze(prompt, new FailureModeContext { RiskAppetite = FailureRiskAppetite.Balanced });
            var aggressive = New().Analyze(prompt, new FailureModeContext { RiskAppetite = FailureRiskAppetite.Aggressive });
            Assert.True(aggressive.OverallRisk <= balanced.OverallRisk);
        }

        [Fact]
        public void Cautious_appends_SchedulePromptReview_at_CDF_grade()
        {
            var ctx = new FailureModeContext
            {
                RiskAppetite = FailureRiskAppetite.Cautious,
                JsonRequired = true,
            };
            var r = New().Analyze("Tell me about Mars.", ctx);
            Assert.Contains(r.Playbook, a => a.Id == "SchedulePromptReview");
        }

        [Fact]
        public void Aggressive_trims_P3_when_higher_priority_exists()
        {
            var ctx = new FailureModeContext { Streaming = true, JsonRequired = true, RiskAppetite = FailureRiskAppetite.Aggressive };
            var r = New().Analyze("Tell me about Mars in a friendly tone.", ctx);
            Assert.Contains(r.Playbook, a => a.Priority == FailurePriority.P0);
            Assert.DoesNotContain(r.Playbook, a => a.Priority == FailurePriority.P3);
        }

        [Fact]
        public void HardenedDraft_appends_TODOs_for_P0_or_P1_actions()
        {
            var original = "Tell me about Mars.";
            var ctx = new FailureModeContext { JsonRequired = true };
            var r = New().Analyze(original, ctx);
            Assert.NotEqual(original, r.HardenedDraft);
            Assert.Contains("# Failure-mode safeguards", r.HardenedDraft);
            Assert.Contains("- [ ]", r.HardenedDraft);
            Assert.StartsWith(original, r.HardenedDraft);
        }

        [Fact]
        public void HardenedDraft_unchanged_when_no_critical_findings()
        {
            var original = "Summarize the following article in <=120 words.";
            var r = New().Analyze(original);
            Assert.Equal(original, r.HardenedDraft);
        }

        [Fact]
        public void ToMarkdown_contains_all_four_sections()
        {
            var r = New().Analyze("Generate something cool about cats.");
            var md = New().ToMarkdown(r);
            Assert.Contains("## Summary", md);
            Assert.Contains("## Findings", md);
            Assert.Contains("## Playbook", md);
            Assert.Contains("## Insights", md);
        }

        [Fact]
        public void ToJson_is_valid_and_deterministic()
        {
            var advisor = New();
            var r = advisor.Analyze("Generate something cool about cats.");
            var a = advisor.ToJson(r);
            var b = advisor.ToJson(r);
            Assert.Equal(a, b);
            using var doc = JsonDocument.Parse(a);
            Assert.True(doc.RootElement.TryGetProperty("verdict", out _));
            Assert.True(doc.RootElement.TryGetProperty("playbook", out _));
        }

        [Fact]
        public void Analyze_does_not_mutate_input_prompt()
        {
            var original = "Generate something cool about cats.";
            var copy = string.Copy(original);
            _ = New().Analyze(original);
            Assert.Equal(copy, original);
        }

        [Fact]
        public void Headline_contains_verdict_grade_and_risk()
        {
            var r = New().Analyze("Summarize the following article in <=120 words.");
            Assert.Contains(r.Verdict.ToString(), r.Headline);
            Assert.Contains("Grade", r.Headline);
            Assert.Contains("Risk", r.Headline);
        }

        [Fact]
        public void ToText_is_non_empty_and_lists_findings_and_playbook()
        {
            var r = New().Analyze("Generate something cool about cats.");
            var txt = New().ToText(r);
            Assert.Contains("Findings", txt);
            Assert.Contains("Playbook", txt);
            Assert.Contains("Insights", txt);
        }
    }
}
