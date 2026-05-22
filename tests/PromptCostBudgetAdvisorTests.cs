namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using Prompt;
    using Xunit;

    public class PromptCostBudgetAdvisorTests
    {
        private static readonly DateTime FixedNow = new(2026, 5, 21, 23, 0, 0, DateTimeKind.Utc);

        private static PromptCostBudgetAdvisor MakeAdvisor() => new(() => FixedNow);

        private static CostOptions Opts(
            CostRiskAppetite r = CostRiskAppetite.Balanced,
            int soft = 6000,
            int hard = 12000)
            => new() { RiskAppetite = r, SoftCharLimit = soft, HardCharLimit = hard };

        private static string Repeat(string s, int n)
        {
            var sb = new StringBuilder(s.Length * n);
            for (int i = 0; i < n; i++) sb.Append(s);
            return sb.ToString();
        }

        [Fact]
        public void EmptyPrompt_NoFindings_GradeA_Ready_BudgetOk()
        {
            var rpt = MakeAdvisor().Analyze("", Opts());
            Assert.Empty(rpt.Findings);
            Assert.Equal('A', rpt.Grade);
            Assert.Equal(CostVerdict.Ready, rpt.Verdict);
            Assert.Contains(rpt.Playbook, a => a.Id == "PROMPT_BUDGET_OK");
            Assert.Contains("VERY_SHORT_PROMPT", rpt.Insights);
            Assert.Equal(0, rpt.EstimatedTokens);
            Assert.Equal(0, rpt.EstimatedSavingsTokens);
            Assert.Equal(FixedNow, rpt.GeneratedAt);
            Assert.Equal("", rpt.CostTrimmedDraft);
        }

        [Fact]
        public void NullPrompt_DoesNotThrow_AndIsEmpty()
        {
            var rpt = MakeAdvisor().Analyze(null, Opts());
            Assert.Empty(rpt.Findings);
            Assert.Equal('A', rpt.Grade);
            Assert.Equal(0, rpt.EstimatedTokens);
        }

        [Fact]
        public void HardBudgetExceeded_FiresP0_BlockSend_GradeF()
        {
            string big = Repeat("a", 13000);
            var rpt = MakeAdvisor().Analyze(big, Opts(soft: 6000, hard: 12000));
            Assert.Contains(rpt.Findings, f => f.Code == "HARD_BUDGET_EXCEEDED");
            Assert.Equal(CostVerdict.BlockSend, rpt.Verdict);
            Assert.Equal('F', rpt.Grade);
            Assert.Contains(rpt.Playbook, a => a.Id == "SHRINK_TO_HARD_BUDGET");
            Assert.Contains(rpt.Playbook, a => a.Id == "BLOCK_AND_REWRITE");
            Assert.Contains("PROMPT_OVER_HARD_BUDGET", rpt.Insights);
            Assert.True(rpt.EstimatedSavingsTokens > 0);
        }

        [Fact]
        public void SoftBudgetExceededOnly_FiresP1_NotP0()
        {
            string mid = Repeat("a", 7000);
            var rpt = MakeAdvisor().Analyze(mid, Opts(soft: 6000, hard: 12000));
            Assert.Contains(rpt.Findings, f => f.Code == "EXCESSIVE_PROMPT_LENGTH");
            Assert.DoesNotContain(rpt.Findings, f => f.Code == "HARD_BUDGET_EXCEEDED");
            Assert.NotEqual(CostVerdict.BlockSend, rpt.Verdict);
            Assert.Contains("PROMPT_OVER_SOFT_BUDGET", rpt.Insights);
        }

        [Fact]
        public void RedundantInstructions_Fires()
        {
            string p = "You are a helpful assistant. Be concise. Keep it short. Answer the user.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "REDUNDANT_INSTRUCTIONS");
            Assert.Contains(rpt.Playbook, a => a.Id == "TIGHTEN_REDUNDANT_INSTRUCTIONS");
        }

        [Fact]
        public void OverlongFewShotExamples_Fires()
        {
            string ex = "```\n" + Repeat("x", 800) + "\n```";
            string p = "You are a translator. Examples:\n" + ex + "\nNow translate the user's input.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "OVERLONG_FEW_SHOT_EXAMPLES");
        }

        [Fact]
        public void ExcessiveHedgingFiller_Fires()
        {
            string p = "Please respond. Please be helpful. Please just answer. Please simply reply. Make sure to be polite. Be sure to thank the user.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "EXCESSIVE_HEDGING_FILLER");
            Assert.Contains(rpt.Playbook, a => a.Id == "REMOVE_FILLER");
        }

        [Fact]
        public void BloatedRolePreamble_Fires()
        {
            string preamble = "You are a brilliant, world-class, deeply knowledgeable, exceptionally patient, immensely thoughtful, profoundly empathetic and outrageously witty multi-domain expert who has spent decades mastering every conceivable corner of human endeavor and stands ready to deploy your encyclopedic mind in service of the user. ";
            string p = preamble + "Respond to the user's question.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "BLOATED_ROLE_PREAMBLE");
            Assert.Contains(rpt.Playbook, a => a.Id == "COMPRESS_ROLE_PREAMBLE");
        }

        [Fact]
        public void RestatedOutputFormat_Fires()
        {
            string p = "Respond in JSON. Be careful. Return as JSON. Output JSON only.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "RESTATED_OUTPUT_FORMAT");
            Assert.Contains(rpt.Playbook, a => a.Id == "CONSOLIDATE_FORMAT_INSTRUCTION");
        }

        [Fact]
        public void UnboundedOutputLength_Fires()
        {
            string p = "Provide a comprehensive analysis of the topic in full detail.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "UNBOUNDED_OUTPUT_LENGTH");
            Assert.Contains(rpt.Playbook, a => a.Id == "CAP_OUTPUT_LENGTH");
        }

        [Fact]
        public void UnboundedOutput_SuppressedByLengthCap()
        {
            string p = "Provide a comprehensive analysis in at most 200 words.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.DoesNotContain(rpt.Findings, f => f.Code == "UNBOUNDED_OUTPUT_LENGTH");
        }

        [Fact]
        public void LargeInlineDataBlock_Fires_AndSavingsTokensPositive()
        {
            string big = Repeat("a,b,c,d,e,f,g,h,i,j", 250); // single long line ~5000 chars
            string p = "Here is the dataset:\n" + big + "\nSummarize it.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "LARGE_INLINE_DATA_BLOCK");
            Assert.Contains(rpt.Playbook, a => a.Id == "EXTRACT_INLINE_DATA_TO_RETRIEVAL");
            Assert.True(rpt.EstimatedSavingsTokens > 0);
        }

        [Fact]
        public void DuplicatedSystemBlock_Fires()
        {
            string para = Repeat("The agent should think carefully about each step and then act. ", 5); // ~310 chars
            string p = "Intro.\n\n" + para + "\n\nMiddle content.\n\n" + para + "\n\nEnd.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "DUPLICATED_SYSTEM_BLOCK");
            Assert.Contains(rpt.Playbook, a => a.Id == "DEDUPE_SYSTEM_BLOCKS");
            Assert.Contains("DUPLICATION_DETECTED", rpt.Insights);
        }

        [Fact]
        public void PolitenessOverhead_Fires()
        {
            string p = "Please answer. Please be brief. Thanks. Thank you for your time.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "POLITENESS_OVERHEAD");
        }

        [Fact]
        public void NoLengthGuidanceForList_Fires_AndSuppressedByCap()
        {
            string p1 = "List the major considerations for choosing a database.";
            var r1 = MakeAdvisor().Analyze(p1, Opts());
            Assert.Contains(r1.Findings, f => f.Code == "NO_LENGTH_GUIDANCE_FOR_LIST");

            string p2 = "List the top 5 considerations for choosing a database.";
            var r2 = MakeAdvisor().Analyze(p2, Opts());
            Assert.DoesNotContain(r2.Findings, f => f.Code == "NO_LENGTH_GUIDANCE_FOR_LIST");
        }

        [Fact]
        public void EstimatedTokens_MatchesFormula()
        {
            string p = Repeat("a", 400);
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Equal(100, rpt.EstimatedTokens); // 400 * 0.25
        }

        [Fact]
        public void CostTrimmedDraft_HasSuggestionsBlock_WhenP0OrP1Present()
        {
            string p = Repeat("a", 13000);
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains("# Prompt-Budget Trim Suggestions", rpt.CostTrimmedDraft);
        }

        [Fact]
        public void CostTrimmedDraft_Unchanged_WhenNoP0OrP1()
        {
            string p = "You are a helpful assistant. Answer succinctly.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Equal(p, rpt.CostTrimmedDraft);
        }

        [Fact]
        public void DeterministicOrdering_AcrossCalls()
        {
            string p = "Please answer. Please be brief. Thanks. Provide a comprehensive analysis. Respond in JSON. Return as JSON. Output JSON. List the considerations.";
            var r1 = MakeAdvisor().Analyze(p, Opts());
            var r2 = MakeAdvisor().Analyze(p, Opts());
            Assert.Equal(
                string.Join(",", r1.Findings.Select(f => f.Code)),
                string.Join(",", r2.Findings.Select(f => f.Code)));
            Assert.Equal(r1.ToJson(), r2.ToJson());
        }

        [Fact]
        public void Cautious_AppendsReviewAction_AtLowGrade()
        {
            string p = Repeat("a", 13000);
            var rpt = MakeAdvisor().Analyze(p, Opts(r: CostRiskAppetite.Cautious));
            Assert.Equal('F', rpt.Grade);
            Assert.Contains(rpt.Playbook, a => a.Id == "SCHEDULE_PROMPT_BUDGET_REVIEW");
        }

        [Fact]
        public void Aggressive_TrimsP3Fallback_WhenP0OrP1Present()
        {
            string p = Repeat("a", 13000) + " Please. Please. Please. Thanks.";
            var rpt = MakeAdvisor().Analyze(p, Opts(r: CostRiskAppetite.Aggressive));
            Assert.Contains(rpt.Findings, f => f.Code == "HARD_BUDGET_EXCEEDED");
            Assert.DoesNotContain(rpt.Playbook, a => a.Priority == CostPriority.P3);
        }

        [Fact]
        public void ToMarkdown_ContainsAllSections()
        {
            string p = "Please answer. Please be brief. Provide a comprehensive analysis.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            string md = rpt.ToMarkdown();
            Assert.Contains("# Prompt Cost / Budget Audit", md);
            Assert.Contains("## Findings", md);
            Assert.Contains("## Playbook", md);
            Assert.Contains("## Insights", md);
        }

        [Fact]
        public void ToJson_RoundtripsDeterministically()
        {
            string p = "List things. Please be comprehensive.";
            var r1 = MakeAdvisor().Analyze(p, Opts());
            var r2 = MakeAdvisor().Analyze(p, Opts());
            Assert.Equal(r1.ToJson(), r2.ToJson());
            Assert.Contains("\"CostScore\"", r1.ToJson());
        }
    }
}
