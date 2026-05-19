namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using Prompt;
    using Xunit;

    public class PromptOutputContractAdvisorTests
    {
        private static readonly DateTime FixedNow = new(2026, 5, 18, 22, 0, 0, DateTimeKind.Utc);

        private static PromptOutputContractAdvisor MakeAdvisor()
            => new(() => FixedNow);

        private static OutputContractOptions Opts(OutputContractRiskAppetite r = OutputContractRiskAppetite.Balanced, bool skipRefusal = false)
            => new() { RiskAppetite = r, SkipRefusalPolicyCheck = skipRefusal };

        // A well-defined prompt that exercises every "good" path.
        private const string WellFormed = @"
You are a categorization assistant.
Respond in English.
Respond as JSON only with the following keys:
- ""category"": string
- ""confidence"": number
- ""rationale"": string (under 40 words)

If you don't know the category, return {""category"": ""UNKNOWN"", ""confidence"": 0, ""rationale"": """"}.
If a request is unsafe or out-of-scope, refuse and say UNKNOWN.
Respond in this order: category, confidence, rationale.
";

        [Fact]
        public void EmptyPrompt_NoFindings_GradeA()
        {
            var rpt = MakeAdvisor().Analyze("", Opts());
            Assert.Empty(rpt.Findings);
            Assert.Equal('A', rpt.Grade);
            Assert.Equal(100, rpt.ContractScore);
            Assert.Equal(OutputContractVerdict.Ready, rpt.Verdict);
            Assert.Contains(rpt.Playbook, a => a.Id == "OUTPUT_CONTRACT_OK");
        }

        [Fact]
        public void NakedSummarize_FiresLengthBudget_NoFormat_NoErrorMode_NoRefusal_NoLanguage()
        {
            var rpt = MakeAdvisor().Analyze("Summarize this article.", Opts());
            var codes = rpt.Findings.Select(f => f.Code).ToList();
            Assert.Contains("MISSING_LENGTH_BUDGET", codes);
            Assert.Contains("NO_FORMAT_DECLARED", codes);
            Assert.Contains("NO_ERROR_MODE", codes);
            Assert.Contains("NO_REFUSAL_POLICY", codes);
            Assert.Contains("UNSPECIFIED_LANGUAGE_OF_OUTPUT", codes);
            Assert.Contains(rpt.Playbook, a => a.Id == "ADD_LENGTH_BUDGET");
            Assert.Contains(rpt.Playbook, a => a.Id == "DECLARE_OUTPUT_FORMAT");
        }

        [Fact]
        public void FreeformButParsed_TriggersBlockSendAndForcedF()
        {
            const string p = "Summarize this article in under 100 words. The output will be parsed by automation downstream.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Equal(OutputContractVerdict.BlockSend, rpt.Verdict);
            Assert.Equal('F', rpt.Grade);
            Assert.Contains(rpt.Findings, f => f.Code == "FREEFORM_BUT_PARSED");
        }

        [Fact]
        public void SchemalessJsonForMachine_AlsoTriggersBlock()
        {
            const string p = "Return JSON. This will be parsed by our pipeline.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Equal(OutputContractVerdict.BlockSend, rpt.Verdict);
            Assert.Contains(rpt.Findings, f => f.Code == "MISSING_SCHEMA" || f.Code == "FREEFORM_BUT_PARSED");
            Assert.Contains(rpt.Playbook, a => a.Id == "DEFINE_JSON_SCHEMA");
        }

        [Fact]
        public void WellDefinedContract_ScoresHigh_GradeA()
        {
            var rpt = MakeAdvisor().Analyze(WellFormed, Opts());
            Assert.True(rpt.ContractScore >= 85, $"score={rpt.ContractScore}, findings={string.Join(",", rpt.Findings.Select(f => f.Code))}");
            Assert.Equal('A', rpt.Grade);
            Assert.Equal(OutputContractVerdict.Ready, rpt.Verdict);
            Assert.Contains("WELL_DEFINED_CONTRACT", rpt.Insights);
        }

        [Fact]
        public void RiskAppetite_Monotonicity_CautiousLEBalancedLEAggressive()
        {
            const string p = "Summarize this article.";
            var c = MakeAdvisor().Analyze(p, Opts(OutputContractRiskAppetite.Cautious));
            var b = MakeAdvisor().Analyze(p, Opts(OutputContractRiskAppetite.Balanced));
            var a = MakeAdvisor().Analyze(p, Opts(OutputContractRiskAppetite.Aggressive));
            Assert.True(c.ContractScore <= b.ContractScore, $"cautious={c.ContractScore} balanced={b.ContractScore}");
            Assert.True(b.ContractScore <= a.ContractScore, $"balanced={b.ContractScore} aggressive={a.ContractScore}");
        }

        [Fact]
        public void Markdown_ContainsRequiredSections()
        {
            var rpt = MakeAdvisor().Analyze("Summarize this article.", Opts());
            var md = rpt.ToMarkdown();
            Assert.Contains("# Output Contract Audit", md);
            Assert.Contains("## Findings", md);
            Assert.Contains("## Playbook", md);
            Assert.Contains("## Insights", md);
        }

        [Fact]
        public void Json_IsValid_AndContainsRequiredKeys_AndDeterministicForFixedClock()
        {
            var rpt1 = MakeAdvisor().Analyze("Summarize this article.", Opts());
            var rpt2 = MakeAdvisor().Analyze("Summarize this article.", Opts());
            var j1 = rpt1.ToJson();
            var j2 = rpt2.ToJson();
            Assert.Equal(j1, j2);
            using var doc = JsonDocument.Parse(j1);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("ContractScore", out _));
            Assert.True(root.TryGetProperty("Grade", out _));
            Assert.True(root.TryGetProperty("Verdict", out _));
            Assert.True(root.TryGetProperty("Findings", out _));
            Assert.True(root.TryGetProperty("Playbook", out _));
            Assert.True(root.TryGetProperty("Insights", out _));
            Assert.True(root.TryGetProperty("ContractTightenedDraft", out _));
        }

        [Fact]
        public void ToText_HeadlineStartsWithVerdict()
        {
            var rpt = MakeAdvisor().Analyze("Summarize this article.", Opts());
            var text = rpt.ToText();
            Assert.StartsWith($"[{rpt.Verdict}]", text);
        }

        [Fact]
        public void Playbook_IsP0First()
        {
            var rpt = MakeAdvisor().Analyze("Summarize this article.", Opts());
            int last = -1;
            foreach (var a in rpt.Playbook)
            {
                int p = (int)a.Priority;
                Assert.True(p >= last, $"Out-of-order priority: {a.Id} {a.Priority}");
                last = p;
            }
        }

        [Fact]
        public void ContractTightenedDraft_PreservesOriginalPromptPrefix()
        {
            const string p = "Summarize this article.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.StartsWith(p, rpt.ContractTightenedDraft);
            Assert.Contains("# Output Contract", rpt.ContractTightenedDraft);
        }

        [Fact]
        public void SkipRefusalPolicyCheck_SuppressesDetector()
        {
            var on = MakeAdvisor().Analyze("Summarize this article.", Opts(skipRefusal: false));
            var off = MakeAdvisor().Analyze("Summarize this article.", Opts(skipRefusal: true));
            Assert.Contains(on.Findings, f => f.Code == "NO_REFUSAL_POLICY");
            Assert.DoesNotContain(off.Findings, f => f.Code == "NO_REFUSAL_POLICY");
        }

        [Fact]
        public void Playbook_DedupedById()
        {
            var rpt = MakeAdvisor().Analyze("Summarize this article.", Opts());
            var ids = rpt.Playbook.Select(a => a.Id).ToList();
            Assert.Equal(ids.Distinct().Count(), ids.Count);
        }

        [Fact]
        public void OutputContractOk_OnlyWhenNoFindings()
        {
            var clean = MakeAdvisor().Analyze("", Opts());
            Assert.Contains(clean.Playbook, a => a.Id == "OUTPUT_CONTRACT_OK");

            var dirty = MakeAdvisor().Analyze("Summarize this article.", Opts());
            Assert.DoesNotContain(dirty.Playbook, a => a.Id == "OUTPUT_CONTRACT_OK");
        }

        [Fact]
        public void AmbiguousFormat_StructuredVsProse_FiresFinding()
        {
            const string p = "Respond as JSON in English with at most 5 keys. Also respond in markdown for clarity.";
            var rpt = MakeAdvisor().Analyze(p, Opts());
            Assert.Contains(rpt.Findings, f => f.Code == "AMBIGUOUS_FORMAT");
            Assert.Contains(rpt.Playbook, a => a.Id == "RESOLVE_FORMAT_AMBIGUITY");
        }

        [Fact]
        public void CautiousMode_AddsSecondReviewerWhenGradeIsCDOrF()
        {
            const string p = "Summarize this article.";
            var rpt = MakeAdvisor().Analyze(p, Opts(OutputContractRiskAppetite.Cautious));
            if (rpt.Grade == 'C' || rpt.Grade == 'D' || rpt.Grade == 'F')
                Assert.Contains(rpt.Playbook, a => a.Id == "SECOND_REVIEWER");
        }
    }
}
