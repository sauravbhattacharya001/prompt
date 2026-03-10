namespace Prompt.Tests
{
    using Xunit;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PromptQualityGateTests
    {
        private readonly PromptQualityGate _gate = new();

        [Fact] public void Null_Throws() => Assert.Throws<ArgumentNullException>(() => _gate.Evaluate(null!));
        [Fact] public void Empty_LowScore() { var r = _gate.Evaluate(""); Assert.True(r.OverallScore < 0.4); Assert.Equal(GateVerdict.Fail, r.Verdict); }
        [Fact] public void SingleWord_Fails() { var r = _gate.Evaluate("Help"); Assert.Equal(GateVerdict.Fail, r.Verdict); Assert.True(r.TopSuggestions.Count > 0); }
        [Fact] public void Clarity_ImperativeVerb() { var r = _gate.Evaluate("Write a comprehensive analysis of climate change impacts on coastal cities."); var c = r.Dimensions.First(d => d.Dimension == QualityDimension.Clarity); Assert.True(c.Score >= 0.5); Assert.Contains(c.Evidence, e => e.Contains("imperative")); }
        [Fact] public void Clarity_VagueWords() { var c = _gate.Evaluate("Tell me something about stuff and things and whatever else is interesting.").Dimensions.First(d => d.Dimension == QualityDimension.Clarity); Assert.True(c.Score < 0.8); }
        [Fact] public void Specificity_Numbers() { var s = _gate.Evaluate("List exactly 5 reasons why Python is popular, with 2 examples each.").Dimensions.First(d => d.Dimension == QualityDimension.Specificity); Assert.True(s.Score >= 0.4); }
        [Fact] public void Specificity_QuotedTerms() { var s = _gate.Evaluate("Explain \"dependency injection\" in the context of \"inversion of control\".").Dimensions.First(d => d.Dimension == QualityDimension.Specificity); Assert.True(s.Score >= 0.4); }
        [Fact] public void OutputFormat_Json() { var f = _gate.Evaluate("Return the results as a JSON object with keys 'name' and 'age'.").Dimensions.First(d => d.Dimension == QualityDimension.OutputFormat); Assert.True(f.Score >= 0.5); }
        [Fact] public void OutputFormat_None() { var f = _gate.Evaluate("Tell me about dogs.").Dimensions.First(d => d.Dimension == QualityDimension.OutputFormat); Assert.True(f.Score <= 0.2); Assert.Contains(f.Suggestions, s => s.Contains("format")); }
        [Fact] public void Constraints_Multiple() { var c = _gate.Evaluate("You must use only peer-reviewed sources. Do not include personal opinions. Limit to 500 words. Avoid jargon.").Dimensions.First(d => d.Dimension == QualityDimension.Constraints); Assert.True(c.Score >= 0.7); }
        [Fact] public void Constraints_None() { Assert.True(_gate.Evaluate("Tell me about dogs.").Dimensions.First(d => d.Dimension == QualityDimension.Constraints).Score < 0.3); }
        [Fact] public void Persona_YouAre() { Assert.True(_gate.Evaluate("You are an expert data scientist. Analyze this dataset and provide insights.").Dimensions.First(d => d.Dimension == QualityDimension.Persona).Score >= 0.5); }
        [Fact] public void Persona_None() { Assert.True(_gate.Evaluate("What is 2+2?").Dimensions.First(d => d.Dimension == QualityDimension.Persona).Score <= 0.2); }
        [Fact] public void Examples_ForExample() { Assert.True(_gate.Evaluate("Classify the sentiment. For example, 'I love this!' is positive. Input: 'Great product'").Dimensions.First(d => d.Dimension == QualityDimension.Examples).Score >= 0.4); }
        [Fact] public void Examples_CodeFences() { Assert.True(_gate.Evaluate("Convert this:\n```python\nprint('hello')\n```\nto JavaScript.").Dimensions.First(d => d.Dimension == QualityDimension.Examples).Score >= 0.3); }
        [Fact] public void Context_Background() { Assert.True(_gate.Evaluate("Background: We are building a fintech app for millennials. Given that our audience prefers mobile-first design, suggest UI improvements.").Dimensions.First(d => d.Dimension == QualityDimension.Context).Score >= 0.5); }
        [Fact] public void Structure_Headers() { Assert.True(_gate.Evaluate("## Instructions\nDo this thing.\n\n## Format\nReturn JSON.\n\n## Constraints\nKeep it short.").Dimensions.First(d => d.Dimension == QualityDimension.Structure).Score >= 0.6); }
        [Fact] public void Structure_Lists() { Assert.True(_gate.Evaluate("Follow these steps:\n- First, read the input\n- Then, parse the data\n- Finally, output the result\n- Validate everything").Dimensions.First(d => d.Dimension == QualityDimension.Structure).Score >= 0.5); }
        [Fact] public void HighQuality_Passes()
        {
            var prompt = "## Role\nYou are an expert data scientist with 10 years of experience.\n\n## Context\nWe have a dataset of 10,000 customer records from an e-commerce platform. The goal is to predict churn.\n\n## Instructions\n1. Analyze the feature importance\n2. Suggest 3 specific preprocessing steps\n3. Recommend exactly 2 model architectures\n\n## Output Format\nReturn your analysis as a JSON object with keys: 'features', 'preprocessing', 'models'.\n\n## Constraints\n- Do not include deep learning approaches\n- Limit explanation to 200 words per section\n- Must cite at least one research paper\n\n## Example\nInput: customer_age, purchase_frequency, last_login\nOutput: {\"features\": [{\"name\": \"purchase_frequency\", \"importance\": 0.85}]}";
            var r = _gate.Evaluate(prompt);
            Assert.Equal(GateVerdict.Pass, r.Verdict);
            Assert.True(r.OverallScore >= 0.6);
            Assert.True(r.Strengths.Count >= 3);
        }
        [Fact] public void LowQuality_FailsOrWarns() { var r = _gate.Evaluate("tell me something cool"); Assert.True(r.Verdict == GateVerdict.Fail || r.Verdict == GateVerdict.Warning); Assert.True(r.Weaknesses.Count >= 3); }
        [Fact] public void CustomThresholds() { Assert.NotEqual(GateVerdict.Pass, new PromptQualityGate(new QualityGateConfig { PassThreshold = 0.9 }).Evaluate("Write a function to sort an array. Return as JSON format with the code.").Verdict); }
        [Fact] public void StrictMode() { Assert.Equal(GateVerdict.Fail, new PromptQualityGate(new QualityGateConfig { StrictMode = true, StrictMinimum = 0.3 }).Evaluate("Tell me about dogs").Verdict); }
        [Fact] public void CustomWeights() { var r = new PromptQualityGate(new QualityGateConfig { Weights = new() { { QualityDimension.Clarity, 3.0 } } }).Evaluate("Write a clear analysis."); Assert.Equal(3.0, r.Dimensions.First(d => d.Dimension == QualityDimension.Clarity).Weight); }
        [Fact] public void MaxSuggestions() { Assert.True(new PromptQualityGate(new QualityGateConfig { MaxSuggestions = 2 }).Evaluate("stuff").TopSuggestions.Count <= 2); }
        [Fact] public void Passes_Bool() => Assert.False(_gate.Passes("x"));
        [Fact] public void EvaluateAll_Ordered() { var r = _gate.EvaluateAll(new[] { "hi", "Write a detailed analysis of Python vs Java.", "stuff" }); Assert.Equal(3, r.Count); Assert.True(r[0].Result.OverallScore >= r[1].Result.OverallScore); }
        [Fact] public void EvaluateAll_Null() => Assert.Throws<ArgumentNullException>(() => _gate.EvaluateAll(null!));
        [Fact] public void Compare_BetterWins() { var c = _gate.Compare("tell me about dogs", "You are a veterinary expert. Provide a detailed comparison of 3 dog breeds. Format as a markdown table. Do not include large breeds."); Assert.Equal("B", c.Winner); Assert.True(c.ScoreDelta > 0); }
        [Fact] public void Compare_Tie() { var c = _gate.Compare("Write about cats.", "Write about cats."); Assert.Equal("Tie", c.Winner); Assert.Equal(0, c.ScoreDelta); }
        [Fact] public void Compare_NullA() => Assert.Throws<ArgumentNullException>(() => _gate.Compare(null!, "b"));
        [Fact] public void Compare_NullB() => Assert.Throws<ArgumentNullException>(() => _gate.Compare("a", null!));
        [Fact] public void Compare_Summary() { Assert.False(string.IsNullOrEmpty(_gate.Compare("hi", "You are an expert. Return JSON format.").Summary)); }
        [Fact] public void Report_NonEmpty() { Assert.Contains("Quality Gate", _gate.Evaluate("Write a Python function for binary search.").Report); }
        [Fact] public void Summary_NonEmpty() { Assert.False(string.IsNullOrEmpty(_gate.Evaluate("Hello world").Summary)); }
        [Fact] public void Labels() { foreach (var d in _gate.Evaluate("You are an expert. Write a detailed analysis. Return JSON. Do not include opinions. For example, analyze this data.").Dimensions) Assert.Contains(d.Label, new[] { "Excellent", "Good", "Fair", "Poor" }); }
        [Fact] public void Always8Dims() { var r = _gate.Evaluate("anything"); Assert.Equal(8, r.Dimensions.Count); foreach (QualityDimension qd in Enum.GetValues(typeof(QualityDimension))) Assert.Contains(qd, r.Dimensions.Select(d => d.Dimension).ToHashSet()); }
        [Fact] public void Clamped() { var r = _gate.Evaluate(string.Join("\n", Enumerable.Repeat("## Section\nYou are an expert. Given that the context involves research, specifically analyze step-by-step. For example, Input: data Output: result. Must limit to 100 words. Do not skip. Format as JSON table with headers.", 10))); foreach (var d in r.Dimensions) { Assert.True(d.Score >= 0.0); Assert.True(d.Score <= 1.0); } Assert.True(r.OverallScore >= 0.0 && r.OverallScore <= 1.0); }
        [Fact] public void Whitespace() => Assert.Equal(GateVerdict.Fail, _gate.Evaluate("   \n\t  ").Verdict);
        [Fact] public void LongSingleLine() => Assert.Equal(8, _gate.Evaluate(new string('a', 5000)).Dimensions.Count);
        [Fact] public void Unicode() => Assert.Equal(8, _gate.Evaluate("Write a detailed report.").Dimensions.Count);
        [Fact] public void ResultLabel() => Assert.Contains(_gate.Evaluate("test").Label, new[] { "Excellent", "Good", "Fair", "Poor" });
    }
}

