namespace Prompt.Tests
{
    using Xunit;
    using System.Linq;

    public class PromptStyleTransferTests
    {
        [Fact]
        public void DetectStyle_EmptyText_ReturnsConcise()
        {
            Assert.Equal(PromptStyle.Concise, PromptStyleTransfer.DetectStyle(""));
        }

        [Fact]
        public void DetectStyle_FormalText_ReturnsFormal()
        {
            var text = "Hereby we request that you furthermore provide the data pursuant to the agreement.";
            Assert.Equal(PromptStyle.Formal, PromptStyleTransfer.DetectStyle(text));
        }

        [Fact]
        public void DetectStyle_CasualText_ReturnsCasual()
        {
            var text = "Hey gonna wanna grab some awesome stuff yeah cool things";
            Assert.Equal(PromptStyle.Casual, PromptStyleTransfer.DetectStyle(text));
        }

        [Fact]
        public void DetectStyle_TechnicalText_ReturnsTechnical()
        {
            var text = "Configure the API endpoint to accept JSON payload with async HTTP calls and validate the schema parameter for optimal latency.";
            Assert.Equal(PromptStyle.Technical, PromptStyleTransfer.DetectStyle(text));
        }

        [Fact]
        public void DetectStyle_InstructionalText_ReturnsInstructional()
        {
            var text = "Step 1: Open the file.\nStep 2: Edit the content.\nStep 3: Save and close.";
            Assert.Equal(PromptStyle.Instructional, PromptStyleTransfer.DetectStyle(text));
        }

        [Fact]
        public void DetectStyle_ShortText_ReturnsConcise()
        {
            Assert.Equal(PromptStyle.Concise, PromptStyleTransfer.DetectStyle("List files"));
        }

        [Fact]
        public void ToFormal_ExpandsContractions()
        {
            var result = PromptStyleTransfer.Transfer("Don't do that, it's bad", PromptStyle.Formal);
            Assert.Contains("do not", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("it is", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Expanded contractions", result.Changes);
        }

        [Fact]
        public void ToFormal_ReplacesCasualWords()
        {
            var result = PromptStyleTransfer.Transfer("Hey that's awesome stuff", PromptStyle.Formal);
            Assert.DoesNotContain("hey", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("awesome", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ToCasual_ContractsFormalPhrases()
        {
            var result = PromptStyleTransfer.Transfer("I do not want to utilize this.", PromptStyle.Casual);
            Assert.Contains("don't", result.Transformed);
        }

        [Fact]
        public void ToCasual_SimplifiesVocabulary()
        {
            var result = PromptStyleTransfer.Transfer("Furthermore, please commence the process.", PromptStyle.Casual);
            Assert.DoesNotContain("furthermore", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("commence", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ToCasual_RemovesPleasePrefix()
        {
            var result = PromptStyleTransfer.Transfer("Please provide the data.", PromptStyle.Casual);
            Assert.False(result.Transformed.StartsWith("Please"));
        }

        [Fact]
        public void ToConcise_RemovesFillerPhrases()
        {
            var result = PromptStyleTransfer.Transfer(
                "I would like you to please kindly show me the results. Thank you.",
                PromptStyle.Concise);
            Assert.DoesNotContain("I would like you to", result.Transformed);
            Assert.DoesNotContain("Thank you", result.Transformed);
        }

        [Fact]
        public void ToConcise_CapitalizesStart()
        {
            var result = PromptStyleTransfer.Transfer("basically show me results", PromptStyle.Concise);
            Assert.True(char.IsUpper(result.Transformed[0]));
        }

        [Fact]
        public void ToVerbose_AddsFraming()
        {
            var result = PromptStyleTransfer.Transfer("List all files", PromptStyle.Verbose);
            Assert.Contains("I would like you to", result.Transformed);
        }

        [Fact]
        public void ToVerbose_AddsDetailRequest()
        {
            var result = PromptStyleTransfer.Transfer("Show results", PromptStyle.Verbose);
            Assert.Contains("detailed", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ToTechnical_UsesPreciseTerms()
        {
            var result = PromptStyleTransfer.Transfer("Check the error and fix it", PromptStyle.Technical);
            Assert.Contains("validate", result.Transformed, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ToInstructional_NumbersSentences()
        {
            var result = PromptStyleTransfer.Transfer(
                "Open the file. Edit the content. Save it.", PromptStyle.Instructional);
            Assert.Contains("1.", result.Transformed);
            Assert.Contains("2.", result.Transformed);
            Assert.Contains("3.", result.Transformed);
        }

        [Fact]
        public void ToInstructional_SplitsCompoundSentence()
        {
            var result = PromptStyleTransfer.Transfer(
                "Open the file, edit the content, and save it", PromptStyle.Instructional);
            Assert.Contains("1.", result.Transformed);
        }

        [Fact]
        public void ToFriendly_AddsGreeting()
        {
            var result = PromptStyleTransfer.Transfer("Generate a report.", PromptStyle.Friendly);
            Assert.StartsWith("Hey!", result.Transformed);
        }

        [Fact]
        public void ToFriendly_SoftensDirectives()
        {
            var result = PromptStyleTransfer.Transfer("You must complete this now.", PromptStyle.Friendly);
            Assert.DoesNotContain("You must", result.Transformed);
        }

        [Fact]
        public void ToFriendly_AddsEncouragingCloser()
        {
            var result = PromptStyleTransfer.Transfer("Generate a report.", PromptStyle.Friendly);
            Assert.Contains("Thanks", result.Transformed);
        }

        [Fact]
        public void Transfer_NullInput_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => PromptStyleTransfer.Transfer(null!, PromptStyle.Formal));
        }

        [Fact]
        public void Transfer_ResultHasCorrectTargetStyle()
        {
            var result = PromptStyleTransfer.Transfer("Hello world", PromptStyle.Concise);
            Assert.Equal(PromptStyle.Concise, result.TargetStyle);
        }

        [Fact]
        public void Transfer_ResultTracksLengthDelta()
        {
            var result = PromptStyleTransfer.Transfer("List files", PromptStyle.Verbose);
            Assert.True(result.LengthDelta > 0, "Verbose should be longer");
        }

        [Fact]
        public void Transfer_ResultRecordsChanges()
        {
            var result = PromptStyleTransfer.Transfer("Don't do that", PromptStyle.Formal);
            Assert.NotEmpty(result.Changes);
        }

        [Fact]
        public void AvailableStyles_Returns7Styles()
        {
            var styles = PromptStyleTransfer.AvailableStyles();
            Assert.Equal(7, styles.Count);
        }

        [Fact]
        public void Transfer_PreservesOriginal()
        {
            var input = "Generate a detailed report now.";
            var result = PromptStyleTransfer.Transfer(input, PromptStyle.Casual);
            Assert.Equal(input, result.Original);
        }

        [Fact]
        public void RoundTrip_FormalToCasualAndBack()
        {
            var input = "Please do not forget to provide the data.";
            var casual = PromptStyleTransfer.Transfer(input, PromptStyle.Casual);
            var formal = PromptStyleTransfer.Transfer(casual.Transformed, PromptStyle.Formal);
            Assert.Contains("do not", formal.Transformed, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AllStyles_ProduceValidResults()
        {
            var input = "Write a function that sorts a list of numbers and returns the median.";
            foreach (PromptStyle style in System.Enum.GetValues(typeof(PromptStyle)))
            {
                var result = PromptStyleTransfer.Transfer(input, style);
                Assert.NotNull(result.Transformed);
                Assert.NotEmpty(result.Transformed);
                Assert.Equal(style, result.TargetStyle);
            }
        }
    }
}
