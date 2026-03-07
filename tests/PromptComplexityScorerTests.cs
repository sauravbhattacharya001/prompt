namespace Prompt.Tests
{
    using Xunit;
    using System.Linq;

    public class PromptComplexityScorerTests
    {
        private readonly PromptComplexityScorer _scorer = new();

        [Fact]
        public void Score_NullPrompt_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => _scorer.Score(null!));
        }

        [Fact]
        public void Score_EmptyPrompt_ReturnsZero()
        {
            var result = _scorer.Score("");
            Assert.Equal(0, result.OverallScore);
            Assert.Equal("Empty", result.Level);
            Assert.Equal("none", result.Recommendation.Tier);
        }

        [Fact]
        public void Score_WhitespacePrompt_ReturnsZero()
        {
            var result = _scorer.Score("   \n\t  ");
            Assert.Equal(0, result.OverallScore);
        }

        [Fact]
        public void Score_SimplePrompt_LowComplexity()
        {
            var result = _scorer.Score("You are a helpful assistant. Answer the user's question.");
            Assert.True(result.OverallScore <= 3.0, $"Expected low complexity, got {result.OverallScore}");
            Assert.Contains(result.Level, new[] { "Trivial", "Simple" });
            Assert.Equal("small", result.Recommendation.Tier);
        }

        [Fact]
        public void Score_ComplexPrompt_HighComplexity()
        {
            var prompt = @"You are a senior tax advisor. Given the user's W-2, 1099-DIV, and
                Schedule K-1, compute their estimated tax liability under both standard
                and itemized deductions. If married filing jointly and AGI exceeds
                {{threshold}}, apply AMT calculations. Compare the results step by step
                and analyze trade-offs. Output as JSON with fields: gross_income,
                deductions, taxable_income, tax_owed, effective_rate. Never include PII.
                Always validate inputs before computing. Ensure compliance with HIPAA
                and GDPR where applicable.";

            var result = _scorer.Score(prompt);
            Assert.True(result.OverallScore >= 4.0, $"Expected high complexity, got {result.OverallScore}");
            Assert.NotEmpty(result.Dimensions);
        }

        [Fact]
        public void Score_ReturnsSummaryString()
        {
            var result = _scorer.Score("Summarize this text.");
            Assert.Contains("Complexity:", result.Summary);
            Assert.Contains("/10", result.Summary);
        }

        [Fact]
        public void Score_HasEightDimensions()
        {
            var result = _scorer.Score("Explain quantum computing step by step using JSON format.");
            Assert.Equal(8, result.Dimensions.Count);
        }

        [Fact]
        public void Score_DimensionNamesAreCorrect()
        {
            var result = _scorer.Score("Hello world");
            var names = result.Dimensions.Select(d => d.Name).ToList();
            Assert.Contains("Instruction Density", names);
            Assert.Contains("Nesting Depth", names);
            Assert.Contains("Variable Load", names);
            Assert.Contains("Ambiguity", names);
            Assert.Contains("Domain Specificity", names);
            Assert.Contains("Output Constraints", names);
            Assert.Contains("Reasoning Depth", names);
            Assert.Contains("Context Dependency", names);
        }

        [Fact]
        public void Score_VariablesDetected()
        {
            var result = _scorer.Score("Hello {{name}}, your order {{orderId}} is ready. Use {code}.");
            var varDim = result.Dimensions.First(d => d.Name == "Variable Load");
            Assert.True(varDim.Score > 0);
            Assert.NotEmpty(varDim.Evidence);
        }

        [Fact]
        public void Score_NestingDetected()
        {
            var prompt = "If the user is a premium member, show their dashboard. Otherwise, show the upgrade page. Unless they have a trial, in that case show trial info.";
            var result = _scorer.Score(prompt);
            var nesting = result.Dimensions.First(d => d.Name == "Nesting Depth");
            Assert.True(nesting.Score > 0);
        }

        [Fact]
        public void Score_AmbiguityDetected()
        {
            var prompt = "Maybe summarize this text. Perhaps include some key points or something, roughly 200 words, etc.";
            var result = _scorer.Score(prompt);
            var ambiguity = result.Dimensions.First(d => d.Name == "Ambiguity");
            Assert.True(ambiguity.Score > 0);
        }

        [Fact]
        public void Score_DomainTermsDetected()
        {
            var prompt = "Use the REST API with OAuth and JWT tokens. Ensure CORS is configured. Apply OWASP best practices.";
            var result = _scorer.Score(prompt);
            var domain = result.Dimensions.First(d => d.Name == "Domain Specificity");
            Assert.True(domain.Score > 0);
            Assert.NotEmpty(domain.Evidence);
        }

        [Fact]
        public void Score_OutputConstraintsDetected()
        {
            var prompt = "Return results as JSON. Include a markdown table with columns: name, score, rank.";
            var result = _scorer.Score(prompt);
            var output = result.Dimensions.First(d => d.Name == "Output Constraints");
            Assert.True(output.Score > 0);
        }

        [Fact]
        public void Score_ReasoningDetected()
        {
            var prompt = "Think step by step. First, analyze the data. Then compare and contrast the options. Finally, synthesize your findings.";
            var result = _scorer.Score(prompt);
            var reasoning = result.Dimensions.First(d => d.Name == "Reasoning Depth");
            Assert.True(reasoning.Score > 0);
        }

        [Fact]
        public void Score_ContextDependencyDetected()
        {
            var prompt = "Based on the above conversation, and referring to the provided documents, summarize the previous discussion.";
            var result = _scorer.Score(prompt);
            var context = result.Dimensions.First(d => d.Name == "Context Dependency");
            Assert.True(context.Score > 0);
        }

        [Fact]
        public void Score_RecommendationHasExampleModels()
        {
            var result = _scorer.Score("Hello");
            Assert.NotEmpty(result.Recommendation.ExampleModels);
            Assert.False(string.IsNullOrEmpty(result.Recommendation.Reasoning));
        }

        [Fact]
        public void Score_EstimatedReasoningStepsAtLeastOne()
        {
            var result = _scorer.Score("Summarize this.");
            Assert.True(result.EstimatedReasoningSteps >= 1);
        }

        [Fact]
        public void Score_OverallScoreCappedAt10()
        {
            // Throw everything at it
            var prompt = @"If the user provides a W-2 and 1099-DIV, you must always compute step by step.
                Otherwise, analyze the REST API response. Perhaps use {{var1}} and {{var2}} and {{var3}}
                and {{var4}} and {{var5}} and {{var6}}. Format as JSON table with columns.
                Think carefully, compare and contrast, synthesize, evaluate each option.
                Based on the above, referring to the provided context, recall that HIPAA GDPR OWASP
                OAuth JWT CORS SQL PCA LSTM GAN apply. Maybe approximately etc. ensure never avoid
                do not use only include exclude list explain generate create write output return.
                Unless it fails, in that case, when applicable, given that, provided that, if not,
                else show the results. Always validate. You must ensure compliance. Never skip steps.
                Remember to cross-reference. Summarize, compare, break it down, first then next.";
            var result = _scorer.Score(prompt);
            Assert.True(result.OverallScore <= 10);
        }

        [Fact]
        public void Score_RiskFactorsPopulatedForComplexPrompts()
        {
            var prompt = @"Maybe perhaps you could possibly analyze the REST API with OAuth JWT CORS
                OWASP HIPAA GDPR PCA using {{a}} {{b}} {{c}}. Step by step compare and contrast
                then synthesize and evaluate. If premium, otherwise, unless trial, given that,
                when applicable. Format as JSON table markdown. Do not skip. Always ensure. You must
                include. Never exclude. Avoid errors. Use only approved methods. etc. or something.
                Sort of roughly approximately as needed if possible optionally you may also.";
            var result = _scorer.Score(prompt);
            Assert.NotEmpty(result.RiskFactors);
        }

        [Fact]
        public void Score_MediumTierForModeratePrompt()
        {
            var prompt = @"You are a product reviewer. Analyze the given product description
                and provide a structured review. Include pros, cons, and a rating from 1 to 5.
                Format your response as JSON.";
            var result = _scorer.Score(prompt);
            // Should be somewhere in the moderate range
            Assert.True(result.OverallScore >= 1.0 && result.OverallScore <= 6.0,
                $"Expected moderate range, got {result.OverallScore}");
        }

        [Fact]
        public void Score_DimensionScoresNonNegative()
        {
            var result = _scorer.Score("Test prompt for validation.");
            foreach (var dim in result.Dimensions)
            {
                Assert.True(dim.Score >= 0, $"{dim.Name} score was negative: {dim.Score}");
                Assert.True(dim.Weight > 0, $"{dim.Name} weight should be positive");
            }
        }
    }
}
