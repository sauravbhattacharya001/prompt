using System;
using Xunit;
using Prompt;

namespace Prompt.Tests
{
    public class PromptOutputValidatorTests
    {
        [Fact]
        public void EmptyValidator_PassesEverything()
        {
            var v = new PromptOutputValidator();
            var r = v.Validate("anything");
            Assert.True(r.IsValid);
        }

        [Fact]
        public void MaxLength_RejectsLongOutput()
        {
            var r = new PromptOutputValidator().MaxLength(5).Validate("too long");
            Assert.False(r.IsValid);
            Assert.Contains("MaxLength", r.Violations[0].Rule);
        }

        [Fact]
        public void MaxLength_AcceptsShortOutput()
        {
            Assert.True(new PromptOutputValidator().MaxLength(10).Validate("ok").IsValid);
        }

        [Fact]
        public void MinLength_RejectsShort()
        {
            Assert.False(new PromptOutputValidator().MinLength(10).Validate("hi").IsValid);
        }

        [Fact]
        public void MinLength_AcceptsLong()
        {
            Assert.True(new PromptOutputValidator().MinLength(2).Validate("hello").IsValid);
        }

        [Fact]
        public void MaxWords_Rejects()
        {
            Assert.False(new PromptOutputValidator().MaxWords(2).Validate("one two three").IsValid);
        }

        [Fact]
        public void MinWords_Rejects()
        {
            Assert.False(new PromptOutputValidator().MinWords(3).Validate("one").IsValid);
        }

        [Fact]
        public void MustMatchRegex_Works()
        {
            var v = new PromptOutputValidator().MustMatchRegex(@"^\d+$");
            Assert.True(v.Validate("123").IsValid);
            Assert.False(v.Validate("abc").IsValid);
        }

        [Fact]
        public void MustNotMatchRegex_Works()
        {
            var v = new PromptOutputValidator().MustNotMatchRegex(@"error", "no errors");
            Assert.True(v.Validate("all good").IsValid);
            Assert.False(v.Validate("got error here").IsValid);
        }

        [Fact]
        public void MustContain_Works()
        {
            var v = new PromptOutputValidator().MustContain("result");
            Assert.True(v.Validate("the result is 42").IsValid);
            Assert.False(v.Validate("nothing here").IsValid);
        }

        [Fact]
        public void MustNotContain_Works()
        {
            var v = new PromptOutputValidator().MustNotContain("secret");
            Assert.True(v.Validate("public info").IsValid);
            Assert.False(v.Validate("this is secret").IsValid);
        }

        [Fact]
        public void OneOf_Works()
        {
            var v = new PromptOutputValidator().OneOf("yes", "no", "maybe");
            Assert.True(v.Validate("yes").IsValid);
            Assert.False(v.Validate("perhaps").IsValid);
        }

        [Fact]
        public void MustStartWith_Works()
        {
            var v = new PromptOutputValidator().MustStartWith("Hello");
            Assert.True(v.Validate("Hello world").IsValid);
            Assert.False(v.Validate("Goodbye").IsValid);
        }

        [Fact]
        public void MustEndWith_Works()
        {
            var v = new PromptOutputValidator().MustEndWith(".");
            Assert.True(v.Validate("Done.").IsValid);
            Assert.False(v.Validate("Done").IsValid);
        }

        [Fact]
        public void ExactLineCount_Works()
        {
            var v = new PromptOutputValidator().ExactLineCount(3);
            Assert.True(v.Validate("a\nb\nc").IsValid);
            Assert.False(v.Validate("a\nb").IsValid);
        }

        [Fact]
        public void LineCountBetween_Works()
        {
            var v = new PromptOutputValidator().LineCountBetween(2, 4);
            Assert.True(v.Validate("a\nb\nc").IsValid);
            Assert.False(v.Validate("a").IsValid);
        }

        [Fact]
        public void MustBeJson_ValidObject()
        {
            Assert.True(new PromptOutputValidator().MustBeJson().Validate("{\"key\": \"value\"}").IsValid);
        }

        [Fact]
        public void MustBeJson_ValidArray()
        {
            Assert.True(new PromptOutputValidator().MustBeJson().Validate("[1, 2, 3]").IsValid);
        }

        [Fact]
        public void MustBeJson_RejectsPlainText()
        {
            Assert.False(new PromptOutputValidator().MustBeJson().Validate("not json").IsValid);
        }

        [Fact]
        public void MustBeJson_RejectsUnbalanced()
        {
            Assert.False(new PromptOutputValidator().MustBeJson().Validate("{\"key\": {\"nested\": true}").IsValid);
        }

        [Fact]
        public void MustContainJsonKey_Works()
        {
            var v = new PromptOutputValidator().MustContainJsonKey("status");
            Assert.True(v.Validate("{\"status\": \"ok\"}").IsValid);
            Assert.False(v.Validate("{\"data\": 1}").IsValid);
        }

        [Fact]
        public void NotEmpty_Works()
        {
            var v = new PromptOutputValidator().NotEmpty();
            Assert.True(v.Validate("content").IsValid);
            Assert.False(v.Validate("   ").IsValid);
        }

        [Fact]
        public void CustomRule_Works()
        {
            var v = new PromptOutputValidator()
                .AddRule("NoProfanity", s => s.Contains("damn") ? "Contains profanity" : null);
            Assert.True(v.Validate("nice text").IsValid);
            Assert.False(v.Validate("damn it").IsValid);
        }

        [Fact]
        public void WarningRule_DoesNotFailValidation()
        {
            var v = new PromptOutputValidator()
                .AddWarning("TooShort", s => s.Length < 10 ? "Output is short" : null);
            var r = v.Validate("hi");
            Assert.True(r.IsValid); // Warnings don't fail by default
            Assert.Single(r.Violations);
            Assert.Equal(ViolationSeverity.Warning, r.Violations[0].Severity);
        }

        [Fact]
        public void StrictMode_WarningsFail()
        {
            var v = new PromptOutputValidator(new OutputValidatorOptions { StrictMode = true })
                .AddWarning("TooShort", s => s.Length < 10 ? "Output is short" : null);
            var r = v.Validate("hi");
            Assert.False(r.IsValid);
        }

        [Fact]
        public void ChainedRules_AllChecked()
        {
            var v = new PromptOutputValidator()
                .NotEmpty()
                .MinLength(5)
                .MaxLength(100)
                .MustContain("answer");
            var r = v.Validate("the answer is 42");
            Assert.True(r.IsValid);
            Assert.Equal(4, r.PassedRules.Count);
        }

        [Fact]
        public void MultipleViolations_AllReported()
        {
            var v = new PromptOutputValidator()
                .MaxLength(5)
                .MustContain("xyz");
            var r = v.Validate("this is too long without xyz match... wait");
            Assert.False(r.IsValid);
            Assert.Equal(2, r.Violations.Count);
        }

        [Fact]
        public void ValidateOrThrow_ThrowsOnInvalid()
        {
            var v = new PromptOutputValidator().MaxLength(3);
            Assert.Throws<OutputValidationException>(() => v.ValidateOrThrow("toolong"));
        }

        [Fact]
        public void ValidateOrThrow_ReturnsOnValid()
        {
            var result = new PromptOutputValidator().MaxLength(10).ValidateOrThrow("ok");
            Assert.Equal("ok", result);
        }

        [Fact]
        public void Summary_DescribesResult()
        {
            var r = new PromptOutputValidator().MaxLength(3).Validate("toolong");
            Assert.Contains("Invalid", r.Summary);
            Assert.Contains("MaxLength", r.Summary);
        }

        [Fact]
        public void TrimOutput_TrimsBeforeValidation()
        {
            // With trim (default), " ok " becomes "ok" (length 2)
            Assert.True(new PromptOutputValidator().MaxLength(5).Validate("  ok  ").IsValid);
        }

        [Fact]
        public void NoTrim_PreservesWhitespace()
        {
            var v = new PromptOutputValidator(new OutputValidatorOptions { TrimOutput = false })
                .MaxLength(3);
            Assert.False(v.Validate("  ok  ").IsValid); // length 6
        }

        [Fact]
        public void Validate_NullInput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptOutputValidator().Validate(null));
        }

        [Fact]
        public void RuleCount_TracksRules()
        {
            var v = new PromptOutputValidator().MaxLength(10).MinLength(1).NotEmpty();
            Assert.Equal(3, v.RuleCount);
        }
    }
}
