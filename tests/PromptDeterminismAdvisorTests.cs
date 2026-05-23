namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using Prompt;
    using Xunit;

    public class PromptDeterminismAdvisorTests
    {
        private static PromptDeterminismAdvisor MakeAdvisor() => new();

        [Fact]
        public void NullPrompt_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MakeAdvisor().Analyze(null!));
        }

        [Fact]
        public void EmptyPrompt_StillReportsBaselineRisks()
        {
            // Empty prompt has no creativity/clock/random/etc., but version+sampling are unpinned by default.
            var rpt = MakeAdvisor().Analyze("");
            Assert.Contains(rpt.Findings, f => f.Mode == DeterminismMode.UnpinnedVersions);
            Assert.Contains(rpt.Findings, f => f.Mode == DeterminismMode.SamplingDrift);
            // No P0
            Assert.DoesNotContain(rpt.Findings, f => f.Priority == DeterminismPriority.P0);
        }

        [Fact]
        public void PinningContext_BoostsScoreAndSuppressesActions()
        {
            var ctx = new DeterminismContext
            {
                ModelVersionPinned = true,
                SamplingPinned = true,
                SeedPinned = true,
            };
            var rpt = MakeAdvisor().Analyze("Summarize the input.", ctx);
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == DeterminismMode.SamplingDrift);
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == DeterminismMode.UnpinnedVersions);
            Assert.DoesNotContain(rpt.Playbook, a => a.Id == "PinModelVersion");
            Assert.DoesNotContain(rpt.Playbook, a => a.Id == "PinSamplingParameters");
            Assert.Equal(100, rpt.ReproducibilityScore);
            Assert.Equal("A", rpt.Grade);
            Assert.Equal(DeterminismVerdict.Reproducible, rpt.Verdict);
        }

        [Fact]
        public void CreativityAndRandomness_HighlyVolatile()
        {
            var rpt = MakeAdvisor().Analyze("Be creative and pick a random color.");
            Assert.Contains(rpt.Findings, f => f.Mode == DeterminismMode.CreativityDirective);
            Assert.Contains(rpt.Findings, f => f.Mode == DeterminismMode.RandomnessDirective);
            Assert.Contains(rpt.Findings, f => f.Priority == DeterminismPriority.P0); // randomness w/o seed
            Assert.Equal(DeterminismVerdict.HighlyVolatile, rpt.Verdict);
            Assert.Equal("F", rpt.Grade);
        }

        [Fact]
        public void RandomnessWithoutSeed_IsP0_AndDrivesPinSeedAction()
        {
            var rpt = MakeAdvisor().Analyze("Pick a random sample of products.");
            var f = rpt.Findings.Single(x => x.Mode == DeterminismMode.RandomnessDirective);
            Assert.Equal(DeterminismPriority.P0, f.Priority);
            Assert.Contains(rpt.Playbook, a => a.Id == "PinSeed" && a.Priority == DeterminismPriority.P0);
        }

        [Fact]
        public void LiveDataWithToolsEnabled_IsP0_AndDrivesSnapshotAction()
        {
            var ctx = new DeterminismContext { ToolsAvailable = true };
            var rpt = MakeAdvisor().Analyze("Browse the web and tell me the current price of AAPL.", ctx);
            var f = rpt.Findings.Single(x => x.Mode == DeterminismMode.LiveDataLookup);
            Assert.Equal(DeterminismPriority.P0, f.Priority);
            Assert.Contains(rpt.Playbook, a => a.Id == "SnapshotLiveData" && a.Priority == DeterminismPriority.P0);
        }

        [Fact]
        public void ImplicitClock_FiresWithoutPinnedDate()
        {
            var rpt = MakeAdvisor().Analyze("List the top headlines today.");
            Assert.Contains(rpt.Findings, f => f.Mode == DeterminismMode.ImplicitClock);
            Assert.Contains(rpt.Playbook, a => a.Id == "PinTimestamp");
        }

        [Fact]
        public void ImplicitClock_SuppressedWhenAnchorPresent()
        {
            var rpt = MakeAdvisor().Analyze("As of 2026-05-23, list the top headlines today.");
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == DeterminismMode.ImplicitClock);
        }

        [Fact]
        public void UnstableOrdering_FiresWithoutTieBreaker_SuppressedWithSortKey()
        {
            var bad = MakeAdvisor().Analyze("List the cities of France.");
            Assert.Contains(bad.Findings, f => f.Mode == DeterminismMode.UnstableOrdering);

            var good = MakeAdvisor().Analyze("List the cities of France ordered by name alphabetically.");
            Assert.DoesNotContain(good.Findings, f => f.Mode == DeterminismMode.UnstableOrdering);
        }

        [Fact]
        public void UnboundedEnumeration_SuppressedByExactCount()
        {
            var bad = MakeAdvisor().Analyze("Provide a few examples of metaphors.");
            Assert.Contains(bad.Findings, f => f.Mode == DeterminismMode.UnboundedEnumeration);

            var good = MakeAdvisor().Analyze("Provide a few examples of metaphors. Provide exactly 3.");
            Assert.DoesNotContain(good.Findings, f => f.Mode == DeterminismMode.UnboundedEnumeration);
        }

        [Fact]
        public void SamplingMentionedButNotPinned_IsHigherSeverity()
        {
            var rpt = MakeAdvisor().Analyze("Use a high temperature when responding.");
            var f = rpt.Findings.Single(x => x.Mode == DeterminismMode.SamplingDrift);
            Assert.True(f.Severity >= 55);
            Assert.Equal(DeterminismPriority.P1, f.Priority);
        }

        [Fact]
        public void SamplingPinnedInline_SuppressesDrift()
        {
            var rpt = MakeAdvisor().Analyze("Use temperature 0 and be deterministic.");
            Assert.DoesNotContain(rpt.Findings, f => f.Mode == DeterminismMode.SamplingDrift);
        }

        [Fact]
        public void UnorderedSerialization_FiresAndSuppressedBySortedKeys()
        {
            var bad = MakeAdvisor().Analyze("Return a JSON object with the values.");
            Assert.Contains(bad.Findings, f => f.Mode == DeterminismMode.UnorderedSerialization);

            var good = MakeAdvisor().Analyze("Return a JSON object with sorted keys for deterministic output.");
            Assert.DoesNotContain(good.Findings, f => f.Mode == DeterminismMode.UnorderedSerialization);
        }

        [Fact]
        public void SessionMemory_FiresAndIsAddressed()
        {
            var rpt = MakeAdvisor().Analyze("As we discussed earlier, finish the plan.");
            Assert.Contains(rpt.Findings, f => f.Mode == DeterminismMode.SessionMemoryAssumed);
        }

        [Fact]
        public void ByteStability_EscalatesCreativityAndClockToP0()
        {
            var ctx = new DeterminismContext { RequireByteStability = true };
            var rpt = MakeAdvisor().Analyze("Be creative; consider today.", ctx);
            var creativity = rpt.Findings.Single(f => f.Mode == DeterminismMode.CreativityDirective);
            var clock = rpt.Findings.Single(f => f.Mode == DeterminismMode.ImplicitClock);
            Assert.Equal(DeterminismPriority.P0, creativity.Priority);
            Assert.Equal(DeterminismPriority.P0, clock.Priority);
            Assert.True(creativity.Severity >= 80);
            Assert.Equal(DeterminismVerdict.HighlyVolatile, rpt.Verdict);
        }

        [Fact]
        public void CautiousAppendsReproReviewWhenP0OrP1Present()
        {
            var ctx = new DeterminismContext { RiskAppetite = DeterminismRiskAppetite.Cautious };
            var rpt = MakeAdvisor().Analyze("Be creative.", ctx);
            Assert.Contains(rpt.Playbook, a => a.Id == "ScheduleReproReview");
        }

        [Fact]
        public void AggressiveTrimsP3WhenHigherPriorityExists()
        {
            // Aggressive + a P0 (randomness) + something that would be P3 (session memory)
            var ctx = new DeterminismContext { RiskAppetite = DeterminismRiskAppetite.Aggressive };
            var rpt = MakeAdvisor().Analyze("Pick a random sample. As we discussed earlier.", ctx);
            Assert.DoesNotContain(rpt.Playbook, a => a.Priority == DeterminismPriority.P3);
            Assert.Contains(rpt.Playbook, a => a.Id == "PinSeed");
        }

        [Fact]
        public void FindingsAndPlaybook_AreDeterministicallyOrdered()
        {
            var rpt = MakeAdvisor().Analyze("Pick a random sample today. List items. Be creative.");
            // Findings ordered: priority asc, severity desc, mode-name asc
            for (int i = 1; i < rpt.Findings.Count; i++)
            {
                var a = rpt.Findings[i - 1];
                var b = rpt.Findings[i];
                Assert.True(
                    (int)a.Priority < (int)b.Priority
                    || ((int)a.Priority == (int)b.Priority && a.Severity >= b.Severity),
                    $"Findings not ordered at index {i}");
            }
            // Playbook ordered: priority asc, id asc
            for (int i = 1; i < rpt.Playbook.Count; i++)
            {
                var a = rpt.Playbook[i - 1];
                var b = rpt.Playbook[i];
                Assert.True(
                    (int)a.Priority < (int)b.Priority
                    || ((int)a.Priority == (int)b.Priority && string.CompareOrdinal(a.Id, b.Id) <= 0),
                    $"Playbook not ordered at index {i}");
            }
        }

        [Fact]
        public void AnalyzeIsPureAndStable()
        {
            const string prompt = "Be creative. Pick a random sample today.";
            var a = MakeAdvisor().Analyze(prompt);
            var b = MakeAdvisor().Analyze(prompt);
            Assert.Equal(a.Headline, b.Headline);
            Assert.Equal(a.ReproducibilityScore, b.ReproducibilityScore);
            Assert.Equal(a.Findings.Count, b.Findings.Count);
            Assert.Equal(a.Playbook.Count, b.Playbook.Count);
        }

        [Fact]
        public void PinnedDraft_AppendsSafeguardBlockOnlyForP0P1()
        {
            var advisor = MakeAdvisor();
            var rpt = advisor.Analyze("Pick a random sample.");
            Assert.Contains("DETERMINISM_SAFEGUARDS", rpt.PinnedDraft);
            Assert.Contains("PinSeed", rpt.PinnedDraft);

            // A prompt with everything pinned (no P0/P1) -> draft unchanged
            var ctx = new DeterminismContext
            {
                ModelVersionPinned = true,
                SamplingPinned = true,
                SeedPinned = true,
            };
            var clean = advisor.Analyze("Summarize the input.", ctx);
            Assert.Equal("Summarize the input.", clean.PinnedDraft);
        }

        [Fact]
        public void ToTextMarkdownJson_RenderHeadlineAndFindings()
        {
            var advisor = MakeAdvisor();
            var rpt = advisor.Analyze("Pick a random sample. List items.");
            var text = advisor.ToText(rpt);
            var md = advisor.ToMarkdown(rpt);
            var json = advisor.ToJson(rpt);
            Assert.Contains(rpt.Headline, text);
            Assert.Contains("Findings", text);
            Assert.Contains("PromptDeterminismAdvisor", md);
            Assert.Contains("RandomnessDirective", md);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(rpt.Headline, doc.RootElement.GetProperty("headline").GetString());
            Assert.Equal(rpt.ReproducibilityScore, doc.RootElement.GetProperty("reproducibilityScore").GetInt32());
            // Sorted top-level keys
            var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
            var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            Assert.Equal(sorted, keys);
        }

        [Fact]
        public void NoRisks_YieldsOkAction()
        {
            var ctx = new DeterminismContext
            {
                ModelVersionPinned = true,
                SamplingPinned = true,
                SeedPinned = true,
            };
            var rpt = MakeAdvisor().Analyze("Translate the following sentence to French: 'Hello.'", ctx);
            Assert.Empty(rpt.Findings);
            Assert.Contains(rpt.Playbook, a => a.Id == "DETERMINISM_OK");
        }
    }
}
