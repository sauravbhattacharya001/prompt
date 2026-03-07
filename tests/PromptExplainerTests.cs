namespace Prompt.Tests
{
    using Xunit;

    public class PromptExplainerTests
    {
        private readonly PromptExplainer _explainer = new();

        [Fact]
        public void Explain_EmptyPrompt_ReturnsZeroComplexity()
        {
            var result = _explainer.Explain("");
            Assert.Equal(0, result.ComplexityScore);
            Assert.Equal(0, result.EstimatedTokens);
            Assert.Equal("Empty prompt", result.Summary);
        }

        [Fact]
        public void Explain_NullPrompt_ReturnsEmptyResult()
        {
            var result = _explainer.Explain(null!);
            Assert.Equal(0, result.ComplexityScore);
        }

        [Fact]
        public void Explain_DetectsRoleAssignment()
        {
            var result = _explainer.Explain("You are a helpful coding assistant.\nPlease review this code.");
            Assert.Contains(result.Techniques, t => t.Name == "Role Assignment");
        }

        [Fact]
        public void Explain_DetectsChainOfThought()
        {
            var result = _explainer.Explain("Solve this math problem. Let's think step by step.");
            Assert.Contains(result.Techniques, t => t.Name == "Chain-of-Thought");
        }

        [Fact]
        public void Explain_DetectsFewShot()
        {
            var result = _explainer.Explain("Classify the sentiment.\nExample:\nInput: 'Great!' -> Positive\nInput: 'Bad' -> Negative");
            Assert.Contains(result.Techniques, t => t.Name == "Few-Shot");
        }

        [Fact]
        public void Explain_DetectsOutputFormatting()
        {
            var result = _explainer.Explain("List the top 5 features. Format as JSON.");
            Assert.Contains(result.Techniques, t => t.Name == "Output Formatting");
        }

        [Fact]
        public void Explain_DetectsNegativeConstraints()
        {
            var result = _explainer.Explain("Summarize the text. Do not include opinions. Never mention the author.");
            Assert.Contains(result.Techniques, t => t.Name == "Negative Constraint");
        }

        [Fact]
        public void Explain_DetectsSequentialInstructions()
        {
            var result = _explainer.Explain("First, read the text. Then, identify key points. Finally, summarize.");
            Assert.Contains(result.Techniques, t => t.Name == "Sequential Instructions");
        }

        [Fact]
        public void Explain_SuggestsRoleWhenMissing()
        {
            var result = _explainer.Explain("Summarize this article.");
            Assert.Contains(result.Suggestions, s => s.Category == "Structure" && s.Message.Contains("role"));
        }

        [Fact]
        public void Explain_SuggestsChainOfThoughtForComplexTask()
        {
            var result = _explainer.Explain("Analyze the following business scenario and explain the trade-offs.");
            Assert.Contains(result.Suggestions, s => s.Category == "Technique" && s.Message.Contains("step by step"));
        }

        [Fact]
        public void Explain_SuggestsFewShotForClassification()
        {
            var result = _explainer.Explain("Classify these emails as spam or not spam.");
            Assert.Contains(result.Suggestions, s => s.Message.Contains("few-shot"));
        }

        [Fact]
        public void Explain_WarnsShortPrompt()
        {
            var result = _explainer.Explain("Tell me a joke.");
            Assert.Contains(result.Suggestions, s => s.Severity == "Warning" && s.Category == "Completeness");
        }

        [Fact]
        public void Explain_ComplexityScalesWithLength()
        {
            var short_ = _explainer.Explain("Hi");
            var long_ = _explainer.Explain(string.Join("\n", Enumerable.Range(0, 100).Select(i =>
                $"Step {i}: You are a senior engineer. Think step by step. Example: input -> output. Format as JSON.")));
            Assert.True(long_.ComplexityScore > short_.ComplexityScore);
        }

        [Fact]
        public void Explain_DetectsMultipleTechniques()
        {
            var prompt = @"You are a senior data analyst.
Context: We have Q4 sales data.
Task: Analyze trends and anomalies.
Let's think step by step.
Example:
  Input: [10, 20, 15] -> Trend: Fluctuating
Do not make assumptions. Never guess.
Format as JSON.";
            var result = _explainer.Explain(prompt);
            Assert.True(result.Techniques.Count >= 3);
        }

        [Fact]
        public void Explain_DetectsSections()
        {
            var prompt = @"Role: You are a tutor.
Context: The student is learning Python.
Task: Explain list comprehensions.
Output format: Use bullet points.";
            var result = _explainer.Explain(prompt);
            Assert.True(result.Sections.Count >= 2);
        }

        [Fact]
        public void ToReport_ProducesReadableOutput()
        {
            var result = _explainer.Explain("You are a helpful assistant. Think step by step. Format as JSON.");
            var report = result.ToReport();
            Assert.Contains("Prompt Analysis Report", report);
            Assert.Contains("Techniques Detected", report);
        }

        [Fact]
        public void Explain_ConfidenceIncreasesWithMultipleMatches()
        {
            var prompt = "Do not use jargon. Don't make assumptions. Never guess. Must not include opinions.";
            var result = _explainer.Explain(prompt);
            var neg = result.Techniques.First(t => t.Name == "Negative Constraint");
            Assert.True(neg.Confidence > 0.7);
        }

        [Fact]
        public void Explain_EstimatesTokensReasonably()
        {
            var result = _explainer.Explain("Hello world, this is a test prompt with about ten words.");
            // ~57 chars / 4 ≈ 15 tokens
            Assert.InRange(result.EstimatedTokens, 10, 25);
        }

        [Fact]
        public void Explain_WarnsExcessiveNegativeConstraints()
        {
            var prompt = "Do not do X. Don't do Y. Never do Z. Must not do A. Do not do B. Don't do C.";
            var result = _explainer.Explain(prompt);
            Assert.Contains(result.Suggestions, s => s.Message.Contains("negative constraints"));
        }

        [Fact]
        public void Explain_DetectsConditionalLogic()
        {
            var result = _explainer.Explain("If the user asks about pricing then respond with the rate card.");
            Assert.Contains(result.Techniques, t => t.Name == "Conditional Logic");
        }

        [Fact]
        public void Explain_DetectsChatRoleMarkers()
        {
            var result = _explainer.Explain("system: You are a bot.\nuser: Hello\nassistant: Hi!");
            Assert.Contains(result.Techniques, t => t.Name == "Chat Role Markers");
        }

        [Fact]
        public void Explain_DetectsNamedTechniqueReference()
        {
            var result = _explainer.Explain("Use a chain-of-thought approach to solve this problem.");
            Assert.Contains(result.Techniques, t => t.Name == "Named Technique Reference");
        }

        [Fact]
        public void Explain_SummaryContainsCoreIntent()
        {
            var result = _explainer.Explain("Task: Translate this document from English to French.");
            Assert.Contains("Translate", result.Summary);
        }
    }
}
