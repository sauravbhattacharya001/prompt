namespace Prompt.Tests
{
    using Xunit;
    using System.Collections.Generic;

    public class PromptMaturityModelTests
    {
        [Fact]
        public void Assess_SimplePrompt_ReturnsLowMaturity()
        {
            var model = new PromptMaturityModel();
            var result = model.Assess("Tell me about dogs");

            Assert.Equal(MaturityLevel.Initial, result.OverallLevel);
            Assert.True(result.OverallScore < 2.5);
            Assert.Equal(8, result.Dimensions.Count);
            Assert.NotEmpty(result.TopRecommendations);
        }

        [Fact]
        public void Assess_WellStructuredPrompt_ReturnsHigherMaturity()
        {
            var prompt = @"You are a senior data analyst expert in Python.
Always provide code examples. Never use deprecated libraries.
Your audience is technical developers. Use a professional tone.

Context: Given the following CSV data, analyze trends.
Input: {{csv_data}}
Output: JSON format with fields: summary, trends, anomalies.

Step 1: Parse the input data
Step 2: Identify statistical outliers
Step 3: Generate trend analysis

Keep responses concise, maximum 300 words.
If the data is invalid or empty, return an error message explaining what's wrong.
If unsure about a trend, state your confidence level.
Verify your analysis before responding.

Example:
Input: 'date,value\n2024-01-01,100\n2024-01-02,105'
Output: {""summary"": ""Upward trend"", ""trends"": [""5% daily growth""]}

Bad example - do not output raw text without structure.

Provide safe, unbiased analysis. Do not generate personal information.
Avoid stereotypes. Cite data sources when applicable.";

            var model = new PromptMaturityModel();
            var result = model.Assess(prompt);

            Assert.True(result.OverallScore >= 3.0, $"Expected >= 3.0 but got {result.OverallScore}");
            Assert.NotEmpty(result.Strengths);
            Assert.NotEmpty(result.ProfileLabel);
        }

        [Fact]
        public void Assess_CustomWeights_AffectsOverallScore()
        {
            var model = new PromptMaturityModel();
            model.SetWeight(MaturityDimension.SafetyEthics, 5.0);
            model.SetWeight(MaturityDimension.Efficiency, 0.1);

            var prompt = "You are helpful. Be safe and ethical. Never generate harmful content. Avoid bias and stereotypes. Respect privacy and PII.";
            var result = model.Assess(prompt);

            // Safety-heavy prompt with safety-heavy weights should score higher
            var defaultModel = new PromptMaturityModel();
            var defaultResult = defaultModel.Assess(prompt);

            Assert.True(result.OverallScore >= defaultResult.OverallScore - 0.5,
                "Safety-weighted model should not score much lower for a safety-focused prompt");
        }

        [Fact]
        public void ToReport_ProducesReadableOutput()
        {
            var model = new PromptMaturityModel();
            var result = model.Assess("You are an expert code reviewer. Analyze the code for bugs.");
            var report = result.ToReport();

            Assert.Contains("PROMPT MATURITY ASSESSMENT", report);
            Assert.Contains("Overall Level:", report);
            Assert.Contains("Overall Score:", report);
        }

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var model = new PromptMaturityModel();
            var result = model.Assess("Summarize this text briefly.");
            var json = result.ToJson();

            Assert.Contains("overallLevel", json);
            Assert.Contains("dimensions", json);
        }

        [Fact]
        public void CompareProgress_ShowsDelta()
        {
            var model = new PromptMaturityModel();
            var before = model.Assess("Tell me about cats");
            var after = model.Assess("You are a veterinary expert. Analyze the following cat health data. Format as JSON. If data is missing, explain what's needed.");

            var comparison = MaturityAssessment.CompareProgress(before, after);
            Assert.Contains("Maturity Progress", comparison);
            Assert.Contains("→", comparison);
        }

        [Fact]
        public void AssessPortfolio_ReturnsAverageWeakestStrongest()
        {
            var model = new PromptMaturityModel();
            var prompts = new List<string>
            {
                "Tell me a joke",
                "You are an expert. Analyze this data. Format as JSON. Step 1: Parse. Step 2: Analyze.",
                "Summarize briefly"
            };

            var (avg, weakest, strongest) = model.AssessPortfolio(prompts);

            Assert.True(weakest.OverallScore <= avg.OverallScore);
            Assert.True(strongest.OverallScore >= avg.OverallScore);
            Assert.Contains("Portfolio Average", avg.ProfileLabel);
        }

        [Fact]
        public void Assess_EmptyPrompt_Throws()
        {
            var model = new PromptMaturityModel();
            Assert.Throws<System.ArgumentException>(() => model.Assess(""));
            Assert.Throws<System.ArgumentException>(() => model.Assess("   "));
        }
    }
}
