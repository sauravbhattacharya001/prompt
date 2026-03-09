namespace Prompt.Tests
{
    using Xunit;
    using Prompt;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PromptNegotiatorTests
    {
        // --- ValidationRule factory tests ---

        [Fact]
        public void ValidationRule_Json_CreatesCorrectRule()
        {
            var rule = ValidationRule.Json();
            Assert.Equal(ValidationRuleKind.JsonFormat, rule.Kind);
            Assert.Equal("json_format", rule.Name);
        }

        [Fact]
        public void ValidationRule_Regex_CreatesCorrectRule()
        {
            var rule = ValidationRule.Regex(@"^\d+$", "digits_only");
            Assert.Equal(ValidationRuleKind.RegexMatch, rule.Kind);
            Assert.Equal(@"^\d+$", rule.Parameter);
            Assert.Equal("digits_only", rule.Name);
        }

        [Fact]
        public void ValidationRule_Length_CreatesCorrectRule()
        {
            var rule = ValidationRule.Length(10, 200);
            Assert.Equal(ValidationRuleKind.LengthRange, rule.Kind);
            Assert.Equal("10", rule.ExtraParams["min"]);
            Assert.Equal("200", rule.ExtraParams["max"]);
        }

        [Fact]
        public void ValidationRule_Contains_CreatesCorrectRule()
        {
            var rule = ValidationRule.Contains(new[] { "hello", "world" });
            Assert.Equal(ValidationRuleKind.ContainsKeywords, rule.Kind);
            Assert.Equal("hello|world", rule.Parameter);
        }

        [Fact]
        public void ValidationRule_Excludes_CreatesCorrectRule()
        {
            var rule = ValidationRule.Excludes(new[] { "error", "fail" });
            Assert.Equal(ValidationRuleKind.ExcludesKeywords, rule.Kind);
        }

        [Fact]
        public void ValidationRule_Enum_CreatesCorrectRule()
        {
            var rule = ValidationRule.Enum(new[] { "yes", "no", "maybe" });
            Assert.Equal(ValidationRuleKind.EnumValue, rule.Kind);
            Assert.Equal("yes|no|maybe", rule.Parameter);
        }

        [Fact]
        public void ValidationRule_JsonFields_CreatesCorrectRule()
        {
            var rule = ValidationRule.JsonFields(new[] { "name", "age" });
            Assert.Equal(ValidationRuleKind.JsonFields, rule.Kind);
        }

        [Fact]
        public void ValidationRule_FromDelegate_CreatesCorrectRule()
        {
            var rule = ValidationRule.FromDelegate(s => s.Length > 5, "custom", "too short");
            Assert.Equal(ValidationRuleKind.Custom, rule.Kind);
            Assert.NotNull(rule.CustomValidator);
        }

        // --- Validate tests ---

        [Fact]
        public void Validate_ValidJson_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            var result = n.Validate("{\"key\": \"value\"}");
            Assert.True(result.IsValid);
            Assert.Equal(1.0, result.Score);
        }

        [Fact]
        public void Validate_InvalidJson_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            var result = n.Validate("not json");
            Assert.False(result.IsValid);
            Assert.Equal(0.0, result.Score);
            Assert.Contains("json_format", result.FailedRules.Keys);
        }

        [Fact]
        public void Validate_JsonArray_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            var result = n.Validate("[1, 2, 3]");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_RegexMatch_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Regex(@"^\d{3}-\d{4}$"));
            var result = n.Validate("123-4567");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_RegexMatch_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Regex(@"^\d+$"));
            var result = n.Validate("abc");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_LengthInRange_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Length(5, 20));
            var result = n.Validate("hello world");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_LengthTooShort_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Length(10, 100));
            var result = n.Validate("short");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_LengthTooLong_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Length(1, 5));
            var result = n.Validate("too long text");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_ContainsKeywords_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Contains(new[] { "hello", "world" }));
            var result = n.Validate("hello beautiful world");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ContainsKeywords_CaseInsensitive()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Contains(new[] { "Hello" }));
            var result = n.Validate("hello there");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ContainsKeywords_MissingKeyword_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Contains(new[] { "hello", "missing" }));
            var result = n.Validate("hello there");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_ExcludesKeywords_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Excludes(new[] { "error", "fail" }));
            var result = n.Validate("everything is fine");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ExcludesKeywords_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Excludes(new[] { "error" }));
            var result = n.Validate("an error occurred");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_EnumValue_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Enum(new[] { "yes", "no" }));
            var result = n.Validate("yes");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_EnumValue_WithWhitespace_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Enum(new[] { "yes", "no" }));
            var result = n.Validate("  yes  ");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_EnumValue_Invalid_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Enum(new[] { "yes", "no" }));
            var result = n.Validate("maybe");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_JsonFields_AllPresent_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.JsonFields(new[] { "name", "age" }));
            var result = n.Validate("{\"name\": \"Alice\", \"age\": 30}");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_JsonFields_MissingField_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.JsonFields(new[] { "name", "age" }));
            var result = n.Validate("{\"name\": \"Alice\"}");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_JsonFields_NotObject_Fails()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.JsonFields(new[] { "name" }));
            var result = n.Validate("[1, 2, 3]");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_CustomRule_Passes()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.FromDelegate(s => s.Contains("magic"), "has_magic", "Must contain magic"));
            var result = n.Validate("this is magic");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_CustomRule_Fails()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.FromDelegate(s => s.Contains("magic"), "has_magic", "Must contain magic"));
            var result = n.Validate("no special word here");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_MultipleRules_PartialPass_Score()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.Length(1, 100));
            // Not JSON but within length
            var result = n.Validate("hello");
            Assert.False(result.IsValid);
            Assert.Equal(0.5, result.Score, 2);
            Assert.Single(result.PassedRules);
            Assert.Single(result.FailedRules);
        }

        [Fact]
        public void Validate_WeightedRules_ScoreReflectsWeights()
        {
            var r1 = ValidationRule.Json();
            r1.Weight = 3.0;
            var r2 = ValidationRule.Length(1, 100);
            r2.Weight = 1.0;

            var n = new PromptNegotiator().AddRule(r1).AddRule(r2);
            // Fails JSON (weight 3) but passes length (weight 1)
            var result = n.Validate("hello");
            Assert.Equal(0.25, result.Score, 2);
        }

        [Fact]
        public void Validate_NoRules_ScoreIsOne()
        {
            var n = new PromptNegotiator();
            var result = n.Validate("anything");
            Assert.True(result.IsValid);
            Assert.Equal(1.0, result.Score);
        }

        [Fact]
        public void Validate_NullResponse_Throws()
        {
            var n = new PromptNegotiator();
            Assert.Throws<ArgumentNullException>(() => n.Validate(null!));
        }

        [Fact]
        public void Validate_Feedback_CombinesFailureMessages()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.Length(100, 200));
            var result = n.Validate("short");
            Assert.Contains("valid JSON", result.Feedback);
            Assert.Contains("between 100 and 200", result.Feedback);
        }

        [Fact]
        public void Validate_AllPass_FeedbackSaysAllPassed()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Length(1, 100));
            var result = n.Validate("hello");
            Assert.Contains("passed", result.Feedback);
        }

        // --- Negotiate tests ---

        [Fact]
        public void Negotiate_SuccessOnFirstRound()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Contains(new[] { "hello" }));

            var result = n.Negotiate("Say hello", _ => "hello world");
            Assert.Equal(NegotiationOutcome.Success, result.Outcome);
            Assert.Equal(1, result.TotalRounds);
            Assert.Equal("hello world", result.BestResponse);
        }

        [Fact]
        public void Negotiate_SuccessOnLaterRound()
        {
            int call = 0;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .WithOptions(o => o.MaxRounds = 5);

            var result = n.Negotiate("Give JSON", _ =>
            {
                call++;
                return call >= 3 ? "{\"ok\": true}" : "not json yet";
            });

            Assert.Equal(NegotiationOutcome.Success, result.Outcome);
            Assert.Equal(3, result.TotalRounds);
        }

        [Fact]
        public void Negotiate_Exhausted()
        {
            int call = 0;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.Length(1, 100))
                .WithOptions(o => { o.MaxRounds = 2; o.StallThreshold = 10; });

            var result = n.Negotiate("Give JSON", _ =>
            {
                call++;
                return call == 1 ? "short" : "also not json but different";
            });
            Assert.Equal(NegotiationOutcome.Exhausted, result.Outcome);
            Assert.Equal(2, result.TotalRounds);
        }

        [Fact]
        public void Negotiate_PartialSuccess_ScoreImproves()
        {
            int call = 0;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.JsonFields(new[] { "a", "b" }))
                .WithOptions(o => o.MaxRounds = 3);

            var result = n.Negotiate("Give JSON with a and b", _ =>
            {
                call++;
                if (call == 1) return "not json";
                if (call == 2) return "{\"a\": 1}"; // JSON but missing b
                return "{\"a\": 1}"; // still missing b
            });

            Assert.True(result.BestScore > result.Rounds[0].Validation.Score);
        }

        [Fact]
        public void Negotiate_StallDetection()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .WithOptions(o =>
                {
                    o.MaxRounds = 10;
                    o.MinImprovement = 0.1;
                    o.StallThreshold = 2;
                });

            var result = n.Negotiate("Give JSON", _ => "same bad response");
            Assert.Equal(NegotiationOutcome.Stalled, result.Outcome);
            Assert.True(result.TotalRounds <= 4); // should stall early
        }

        [Fact]
        public void Negotiate_PromptIsRefined()
        {
            int call = 0;
            string? lastPrompt = null;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .WithOptions(o => o.MaxRounds = 3);

            var result = n.Negotiate("Give JSON", prompt =>
            {
                call++;
                lastPrompt = prompt;
                return call >= 3 ? "{}" : "nope";
            });

            // The prompt should have been refined
            Assert.NotEqual("Give JSON", lastPrompt);
        }

        [Fact]
        public void Negotiate_NoRules_Throws()
        {
            var n = new PromptNegotiator();
            Assert.Throws<InvalidOperationException>(() =>
                n.Negotiate("test", _ => "response"));
        }

        [Fact]
        public void Negotiate_NullPrompt_Throws()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            Assert.Throws<ArgumentNullException>(() => n.Negotiate(null!, _ => "r"));
        }

        [Fact]
        public void Negotiate_NullProvider_Throws()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            Assert.Throws<ArgumentNullException>(() => n.Negotiate("test", null!));
        }

        [Fact]
        public void Negotiate_RecordsAllRounds()
        {
            int call = 0;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .WithOptions(o => o.MaxRounds = 3);

            var result = n.Negotiate("Give JSON", _ =>
            {
                call++;
                return call == 3 ? "{}" : "nope";
            });

            Assert.Equal(3, result.Rounds.Count);
            Assert.All(result.Rounds, r =>
            {
                Assert.True(r.Round > 0);
                Assert.NotEmpty(r.Prompt);
                Assert.NotEmpty(r.Response);
            });
        }

        [Fact]
        public void Negotiate_BestRoundTracked()
        {
            int call = 0;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.Length(1, 100))
                .WithOptions(o => o.MaxRounds = 3);

            var result = n.Negotiate("test", _ =>
            {
                call++;
                return call == 2 ? "{\"x\": 1}" : "nope"; // round 2 is best
            });

            Assert.Equal(2, result.BestRound);
            Assert.Equal("{\"x\": 1}", result.BestResponse);
        }

        [Fact]
        public void Negotiate_ScoreImprovement_Calculated()
        {
            int call = 0;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.Length(1, 50))
                .WithOptions(o => o.MaxRounds = 2);

            var result = n.Negotiate("test", _ =>
            {
                call++;
                return call == 1 ? "nope" : "{}"; // improves
            });

            Assert.True(result.ScoreImprovement > 0);
        }

        [Fact]
        public void Negotiate_OriginalPromptStored()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Contains(new[] { "x" }));
            var result = n.Negotiate("my prompt", _ => "x");
            Assert.Equal("my prompt", result.OriginalPrompt);
        }

        // --- RefinePrompt tests ---

        [Fact]
        public void RefinePrompt_AppendConstraints_AddsFailures()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            var validation = n.Validate("bad");
            var refined = n.RefinePrompt("original", validation, RefinementStrategy.AppendConstraints);
            Assert.Contains("original", refined);
            Assert.Contains("CONSTRAINTS", refined);
        }

        [Fact]
        public void RefinePrompt_WrapWithFormat_AddsJsonInstruction()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            var validation = n.Validate("bad");
            var refined = n.RefinePrompt("give json", validation, RefinementStrategy.WrapWithFormat);
            Assert.Contains("valid JSON", refined);
        }

        [Fact]
        public void RefinePrompt_ProgressiveTightening_EscalatesLanguage()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            var validation = n.Validate("bad");

            var r1 = n.RefinePrompt("test", validation, RefinementStrategy.ProgressiveTightening, 1);
            var r3 = n.RefinePrompt("test", validation, RefinementStrategy.ProgressiveTightening, 3);

            Assert.Contains("Please", r1);
            Assert.Contains("CRITICAL", r3);
        }

        [Fact]
        public void RefinePrompt_SchemaDirected_BuildsSchema()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.JsonFields(new[] { "name", "age" }));
            var validation = n.Validate("bad");
            var refined = n.RefinePrompt("test", validation, RefinementStrategy.SchemaDirected);
            Assert.Contains("structure", refined);
        }

        [Fact]
        public void RefinePrompt_ValidResponse_NoChange()
        {
            var validation = new ValidationResult { Score = 1.0 };
            var n = new PromptNegotiator();
            var refined = n.RefinePrompt("original", validation, RefinementStrategy.AppendConstraints);
            Assert.Equal("original", refined);
        }

        // --- Factory methods ---

        [Fact]
        public void ForJson_CreatesJsonNegotiator()
        {
            var n = PromptNegotiator.ForJson(new[] { "id", "name" });
            Assert.Equal(2, n.Rules.Count);
            var valid = n.Validate("{\"id\": 1, \"name\": \"test\"}");
            Assert.True(valid.IsValid);
        }

        [Fact]
        public void ForJson_WithoutFields_SingleRule()
        {
            var n = PromptNegotiator.ForJson();
            Assert.Single(n.Rules);
        }

        [Fact]
        public void ForEnum_CreatesEnumNegotiator()
        {
            var n = PromptNegotiator.ForEnum(new[] { "cat", "dog" });
            Assert.Single(n.Rules);
            Assert.Equal(3, n.Options.MaxRounds);
            Assert.True(n.Validate("cat").IsValid);
            Assert.False(n.Validate("fish").IsValid);
        }

        [Fact]
        public void ForStructuredText_CreatesLengthNegotiator()
        {
            var n = PromptNegotiator.ForStructuredText(10, 50, new[] { "summary" });
            Assert.Equal(2, n.Rules.Count);
        }

        // --- Fluent API ---

        [Fact]
        public void FluentApi_Chaining()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.Length(1, 100))
                .WithOptions(o => o.MaxRounds = 3);

            Assert.Equal(2, n.Rules.Count);
            Assert.Equal(3, n.Options.MaxRounds);
        }

        [Fact]
        public void AddRules_Batch()
        {
            var rules = new[] { ValidationRule.Json(), ValidationRule.Length(1, 100) };
            var n = new PromptNegotiator().AddRules(rules);
            Assert.Equal(2, n.Rules.Count);
        }

        [Fact]
        public void ClearRules_RemovesAll()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .ClearRules();
            Assert.Empty(n.Rules);
        }

        [Fact]
        public void AddRule_NullRule_Throws()
        {
            var n = new PromptNegotiator();
            Assert.Throws<ArgumentNullException>(() => n.AddRule(null!));
        }

        // --- Config report ---

        [Fact]
        public void GetConfigReport_IncludesRules()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .AddRule(ValidationRule.Length(10, 200));
            var report = n.GetConfigReport();
            Assert.Contains("Rules: 2", report);
            Assert.Contains("JsonFormat", report);
            Assert.Contains("LengthRange", report);
        }

        // --- Summary ---

        [Fact]
        public void NegotiationResult_Summary_FormatsCorrectly()
        {
            var result = new NegotiationResult
            {
                Outcome = NegotiationOutcome.Success,
                BestScore = 1.0,
                BestRound = 2,
                Rounds = { new NegotiationRound(), new NegotiationRound() }
            };
            Assert.Contains("Success", result.Summary);
            Assert.Contains("Rounds: 2", result.Summary);
        }

        // --- Escalating strategy ---

        [Fact]
        public void Negotiate_EscalatingStrategy_UsesMultipleStrategies()
        {
            int call = 0;
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .WithOptions(o =>
                {
                    o.MaxRounds = 5;
                    o.Strategy = RefinementStrategy.Escalating;
                });

            var result = n.Negotiate("Give JSON", _ =>
            {
                call++;
                return call >= 5 ? "{}" : "nope";
            });

            // Check different strategies were used across rounds
            var strategies = result.Rounds
                .Where(r => r.StrategyUsed.HasValue)
                .Select(r => r.StrategyUsed!.Value)
                .Distinct()
                .ToList();
            Assert.True(strategies.Count >= 2);
        }

        // --- Edge cases ---

        [Fact]
        public void Validate_EmptyString_EvaluatesRules()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Length(1, 100));
            var result = n.Validate("");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_JsonWithWhitespace_Passes()
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            var result = n.Validate("  { \"key\": \"value\" }  ");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Negotiate_SingleRound_MaxRoundsOne()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Json())
                .WithOptions(o => o.MaxRounds = 1);

            var result = n.Negotiate("test", _ => "not json");
            Assert.Equal(1, result.TotalRounds);
            Assert.Equal(NegotiationOutcome.Exhausted, result.Outcome);
        }

        [Fact]
        public void Validate_ExcludesEmpty_Passes()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Excludes(Array.Empty<string>()));
            var result = n.Validate("anything");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ContainsEmpty_Passes()
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Contains(Array.Empty<string>()));
            var result = n.Validate("anything");
            Assert.True(result.IsValid);
        }
    }
}
