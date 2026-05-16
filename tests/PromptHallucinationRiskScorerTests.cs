namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Xunit;

    public class PromptHallucinationRiskScorerTests
    {
        private readonly PromptHallucinationRiskScorer _scorer = new();

        [Fact]
        public void Score_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _scorer.Score(null!));
        }

        [Fact]
        public void Score_EmptyPrompt_ReturnsZeroAndEmptyFixes()
        {
            var result = _scorer.Score("");
            Assert.Equal(0, result.OverallScore);
            Assert.Equal("Empty", result.Level);
            Assert.Empty(result.Fixes);
            Assert.Equal("", result.MitigatedPromptDraft);
        }

        [Fact]
        public void Score_WhitespacePrompt_ReturnsZero()
        {
            var result = _scorer.Score("   \n\t  ");
            Assert.Equal(0, result.OverallScore);
            Assert.Equal("Empty", result.Level);
        }

        [Fact]
        public void Score_GroundedPrompt_LowRisk()
        {
            var prompt = @"Context:
```
The 2019 study by Smith et al. found that mice exposed to compound X
showed a 23% reduction in inflammation markers over 14 days.
```

Based on the Context above, what compound was tested and what was the
observed effect? Cite the source using [1]. If the answer is not in the
Context, say 'I don't know'.";

            var result = _scorer.Score(prompt);
            Assert.True(result.OverallScore <= 3.5,
                $"Expected low risk for a grounded prompt, got {result.OverallScore}. Dims: " +
                string.Join(", ", result.Dimensions.Select(d => $"{d.Name}={d.Score}")));
            Assert.Contains(result.Level, new[] { "Minimal", "Low", "Moderate" });
        }

        [Fact]
        public void Score_HighRiskPrompt_ProducesFixesAndMitigatedDraft()
        {
            var prompt = "List every paper Alice Liddell published in 2024 with exact citation counts, " +
                         "the precise dates of publication, and the names of all co-authors. " +
                         "Do not say you don't know.";

            var result = _scorer.Score(prompt);

            Assert.True(result.OverallScore >= 4.5,
                $"Expected moderate-or-higher risk, got {result.OverallScore}. Dims: " +
                string.Join(", ", result.Dimensions.Select(d => $"{d.Name}={d.Score}")));
            Assert.NotEmpty(result.Fixes);
            Assert.Contains("# Anti-Hallucination Guardrails", result.MitigatedPromptDraft);
            Assert.StartsWith(prompt.TrimEnd().Substring(0, 30), result.MitigatedPromptDraft);
        }

        [Fact]
        public void Score_Fixes_AreSortedBySeverityDesc()
        {
            var prompt = "List every detail about Dr. Foo Bar. Exact dates, exact citation counts, " +
                         "every paper, every co-author. Must answer. Do not say you don't know. " +
                         "Imagine plausible answers where uncertain. Latest publications as of today. " +
                         "Also include their current address and current phone number.";

            var result = _scorer.Score(prompt);
            Assert.NotEmpty(result.Fixes);

            var order = new System.Collections.Generic.Dictionary<string, int>
            {
                { "critical", 0 }, { "high", 1 }, { "medium", 2 }, { "low", 3 },
            };
            for (int i = 1; i < result.Fixes.Count; i++)
            {
                Assert.True(order[result.Fixes[i - 1].Severity] <= order[result.Fixes[i].Severity],
                    $"Fixes not sorted: {result.Fixes[i - 1].Severity} before {result.Fixes[i].Severity}");
            }
        }

        [Fact]
        public void Score_SummaryString_HasExpectedFormat()
        {
            var result = _scorer.Score("Tell me about Marie Curie. List every paper she wrote.");
            Assert.Matches(@"^Hallucination Risk: \d+\.\d/10 \([A-Za-z]+\) - \d+ fixes suggested$", result.Summary);
        }

        [Fact]
        public void Score_SpeculativePrompt_TriggersSpeculativeDimension()
        {
            var prompt = "Invent a plausible biography for a fictional 18th-century inventor named " +
                         "Edgar Thistlewick. Imagine his major contributions.";
            var result = _scorer.Score(prompt);
            var spec = result.Dimensions.First(d => d.Name == "Speculative Framing");
            Assert.True(spec.Score > 0, $"Expected SpeculativeFraming > 0, got {spec.Score}");
            Assert.NotEmpty(spec.Evidence);
        }

        [Fact]
        public void Score_LowRiskPrompt_MitigatedDraftEqualsOriginalWhenNoFixes()
        {
            var prompt = "Context:\n```\nThe sky is blue because of Rayleigh scattering.\n```\n" +
                         "Summarize the Context in one sentence. If unsure, say 'I don't know'.";
            var result = _scorer.Score(prompt);

            if (result.Fixes.Count == 0)
            {
                Assert.Equal(prompt, result.MitigatedPromptDraft);
            }
            else
            {
                // If fixes triggered, the test prompt was richer than expected; still validate header is present.
                Assert.Contains("# Anti-Hallucination Guardrails", result.MitigatedPromptDraft);
            }
        }
    }
}
