namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Prompt;
    using Xunit;

    public class PromptKnowledgeFreshnessAdvisorTests
    {
        private static readonly DateTime FixedNow = new(2026, 5, 17, 17, 0, 0, DateTimeKind.Utc);

        private static PromptKnowledgeFreshnessAdvisor MakeAdvisor() =>
            new(() => FixedNow);

        [Fact]
        public void EmptyPrompt_IsFresh()
        {
            var rpt = MakeAdvisor().Analyze("   ");
            Assert.Equal(FreshnessRiskLevel.Fresh, rpt.Verdict);
            Assert.Equal(0, rpt.OverallScore);
            Assert.Equal('A', rpt.Grade);
            Assert.Empty(rpt.Findings);
        }

        [Fact]
        public void AnchoredPrompt_NoStaleLanguageFinding()
        {
            var prompt = "As of 2026-05-15, the policy described below applies to all new customers.";
            var rpt = MakeAdvisor().Analyze(prompt);
            Assert.DoesNotContain(rpt.Findings, f => f.Code == "STALE_PHRASE");
            Assert.True(rpt.Grade == 'A' || rpt.Grade == 'B', $"expected A or B, got {rpt.Grade}");
        }

        [Fact]
        public void HardcodedOldYear_TriggersReplaceAction()
        {
            var prompt = "Use the 2018 tax bracket when computing the deduction.";
            var rpt = MakeAdvisor().Analyze(prompt);
            var f = rpt.Findings.FirstOrDefault(x => x.Code == "HARDCODED_YEAR");
            Assert.NotNull(f);
            Assert.True(f!.Severity >= 40, $"severity was {f.Severity}");
            Assert.Contains(rpt.Playbook, a => a.Code == "REPLACE_HARDCODED_DATE" && a.Priority == FreshnessPriority.P0);
        }

        [Fact]
        public void FutureYear_NotFlaggedAsStale()
        {
            var prompt = "The upcoming 2028 elections will determine policy direction.";
            var rpt = MakeAdvisor().Analyze(prompt);
            Assert.DoesNotContain(rpt.Findings, f => f.Code == "HARDCODED_YEAR");
        }

        [Fact]
        public void StalePhraseWithoutAnchor_EmitsAddAsOfAction()
        {
            var prompt = "Summarize the latest customer feedback we received today.";
            var rpt = MakeAdvisor().Analyze(prompt);
            Assert.Contains(rpt.Findings, f => f.Code == "STALE_PHRASE");
            Assert.Contains(rpt.Playbook, a => a.Code == "ADD_AS_OF_ANCHOR");
        }

        [Fact]
        public void StalePhraseWithRecentAnchor_IsAnchored()
        {
            var prompt = "Summarize the latest customer feedback we received today.";
            var ctx = new FreshnessContext(AsOf: FixedNow.AddDays(-10));
            var rpt = MakeAdvisor().Analyze(prompt, ctx);
            Assert.DoesNotContain(rpt.Findings, f => f.Code == "STALE_PHRASE");
            Assert.Contains(rpt.Findings, f => f.Code == "STALE_PHRASE_ANCHORED");
            Assert.DoesNotContain(rpt.Playbook, a => a.Code == "ADD_AS_OF_ANCHOR");
        }

        [Fact]
        public void StaleAsOf_TriggersCriticallyStale()
        {
            var prompt = "Reference our quarterly KPIs.";
            var ctx = new FreshnessContext(AsOf: FixedNow.AddDays(-400));
            var rpt = MakeAdvisor().Analyze(prompt, ctx);
            Assert.Contains(rpt.Findings, f => f.Code == "STALE_AS_OF" && f.Verdict == FreshnessRiskLevel.CriticallyStale);
            Assert.Contains(rpt.Playbook, a => a.Code == "ADD_KB_REFRESH_CADENCE" && a.Priority == FreshnessPriority.P0);
            Assert.Contains(rpt.Playbook, a => a.Code == "WARN_USER_DATA_MAY_BE_STALE");
        }

        [Fact]
        public void DateSensitiveDomainNoGrounding_RequiresRetrievalP0()
        {
            var prompt = "Summarize this morning's headlines for our internal brief.";
            var ctx = new FreshnessContext(Domain: FreshnessDomain.News, HasRetrievalGrounding: false);
            var rpt = MakeAdvisor().Analyze(prompt, ctx);
            Assert.Contains(rpt.Findings, f => f.Code == "DATE_SENSITIVE_DOMAIN_NO_GROUNDING");
            Assert.Contains(rpt.Playbook, a => a.Code == "ADD_RETRIEVAL_GROUNDING" && a.Priority == FreshnessPriority.P0);
        }

        [Fact]
        public void DateSensitiveDomainWithGrounding_NoFinding()
        {
            var prompt = "Summarize this morning's headlines for our internal brief.";
            var ctx = new FreshnessContext(Domain: FreshnessDomain.News, HasRetrievalGrounding: true);
            var rpt = MakeAdvisor().Analyze(prompt, ctx);
            Assert.DoesNotContain(rpt.Findings, f => f.Code == "DATE_SENSITIVE_DOMAIN_NO_GROUNDING");
        }

        [Fact]
        public void TrainingCutoffExposure_TriggersGuardAction()
        {
            var prompt = "As an AI language model, my training data goes up to early 2024, so caveat accordingly.";
            var rpt = MakeAdvisor().Analyze(prompt);
            Assert.Contains(rpt.Findings, f => f.Code == "TRAINING_CUTOFF_EXPOSURE");
            Assert.Contains(rpt.Playbook, a => a.Code == "GUARD_TRAINING_CUTOFF" && a.Priority == FreshnessPriority.P1);
        }

        [Fact]
        public void AggressiveAppetite_TrimsLowPriorityActions()
        {
            var prompt = "Give me the latest sales numbers as of now.";
            var cautious = MakeAdvisor().Analyze(prompt, new FreshnessContext(RiskAppetite: "cautious"));
            var aggressive = MakeAdvisor().Analyze(prompt, new FreshnessContext(RiskAppetite: "aggressive"));
            Assert.True(cautious.Playbook.Count >= aggressive.Playbook.Count,
                $"cautious={cautious.Playbook.Count} aggressive={aggressive.Playbook.Count}");
            Assert.DoesNotContain(aggressive.Playbook,
                a => a.Priority == FreshnessPriority.P2 || a.Priority == FreshnessPriority.P3);
        }

        [Fact]
        public void HighSeverityFinding_ForcesGradeF()
        {
            var prompt = "Reference the 1999 policy memo.";
            var ctx = new FreshnessContext(AsOf: FixedNow.AddDays(-1000));
            var rpt = MakeAdvisor().Analyze(prompt, ctx);
            Assert.True(rpt.Findings.Any(f => f.Severity >= 75));
            Assert.Equal('F', rpt.Grade);
        }

        [Fact]
        public void MultipleStalePhrases_DedupeToOneAddAsOfAction()
        {
            var prompt = "Provide the latest, current, and most recent updates right now today.";
            var rpt = MakeAdvisor().Analyze(prompt);
            int addAnchorCount = rpt.Playbook.Count(a => a.Code == "ADD_AS_OF_ANCHOR");
            Assert.Equal(1, addAnchorCount);
        }

        [Fact]
        public void ToJson_IsDeterministic()
        {
            var prompt = "Summarize the latest 2022 earnings report.";
            var advisor = MakeAdvisor();
            var a = advisor.Analyze(prompt).ToJson();
            var b = advisor.Analyze(prompt).ToJson();
            Assert.Equal(a, b);
        }

        [Fact]
        public void ToMarkdown_ContainsHeaderAndFindingsSection()
        {
            var prompt = "Use the 2018 tax bracket when computing.";
            var md = MakeAdvisor().Analyze(prompt).ToMarkdown();
            Assert.Contains("## PromptKnowledgeFreshnessAdvisor", md);
            Assert.Contains("### Findings", md);
            Assert.Contains("HARDCODED_YEAR", md);
        }

        [Fact]
        public void Render_TextIncludesScoreAndVerdict()
        {
            var prompt = "Use the 2018 tax bracket.";
            var text = MakeAdvisor().Analyze(prompt).Render();
            Assert.Contains("score", text);
            Assert.Contains("HardcodedYear", text);
        }

        [Fact]
        public void NullPrompt_ReturnsFreshReport()
        {
            var rpt = MakeAdvisor().Analyze(null);
            Assert.Equal(FreshnessRiskLevel.Fresh, rpt.Verdict);
            Assert.Equal(0, rpt.OverallScore);
        }

        [Fact]
        public void Playbook_OrderedP0First()
        {
            var prompt = "Use the 2018 tax bracket on the latest filings.";
            var rpt = MakeAdvisor().Analyze(prompt);
            for (int i = 1; i < rpt.Playbook.Count; i++)
            {
                Assert.True((int)rpt.Playbook[i].Priority >= (int)rpt.Playbook[i - 1].Priority);
            }
        }
    }
}
