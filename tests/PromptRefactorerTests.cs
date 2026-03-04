namespace Prompt.Tests
{
    using Xunit;

    public class PromptRefactorerTests
    {
        private readonly PromptRefactorer _refactorer = new();

        // ── Basic Analysis ──

        [Fact]
        public void Analyze_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() => _refactorer.Analyze(null!));
        }

        [Fact]
        public void Analyze_EmptyPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() => _refactorer.Analyze("  "));
        }

        [Fact]
        public void Analyze_SimpleCleanPrompt_HighScore()
        {
            var report = _refactorer.Analyze("Summarize this text.");
            Assert.True(report.Score >= 70);
            Assert.Equal("Summarize this text.", report.OriginalPrompt);
        }

        [Fact]
        public void Analyze_ReturnsTokenCount()
        {
            var report = _refactorer.Analyze("Hello world test prompt here.");
            Assert.True(report.TokenCount > 0);
        }

        [Fact]
        public void Analyze_DetectsVariables()
        {
            var report = _refactorer.Analyze("You are a {{role}}. Help with {{topic}}.");
            Assert.Equal(2, report.VariableCount);
        }

        // ── Redundancy Detection ──

        [Fact]
        public void CheckRedundancy_RepeatedWord_Flagged()
        {
            var prompt = "Important: this is important. Very important thing. The important part is important. Remember the important stuff. The important detail is important.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s =>
                s.Category == RefactorCategory.Redundancy &&
                s.Description.Contains("important", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CheckRedundancy_DuplicatePhrase_Flagged()
        {
            var prompt = "Please make sure to follow the guidelines carefully. Always follow the guidelines carefully when working.";
            var config = new RefactorerConfig { MinPhraseLength = 3 };
            var r = new PromptRefactorer(config);
            var report = r.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Redundancy);
        }

        [Fact]
        public void CheckRedundancy_FillerPhrase_Flagged()
        {
            var prompt = "Please note that you should respond in JSON. It is important to note that errors must be handled.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s =>
                s.Category == RefactorCategory.Redundancy &&
                s.Description.Contains("filler", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void CheckRedundancy_FillerPhrase_HasAutoFix()
        {
            var prompt = "I would like you to summarize the article.";
            var report = _refactorer.Analyze(prompt);
            var filler = report.Suggestions.FirstOrDefault(s =>
                s.Description.Contains("filler", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(filler);
            Assert.True(filler!.HasAutoFix);
        }

        // ── Specificity Detection ──

        [Fact]
        public void CheckSpecificity_VaguePhrase_Flagged()
        {
            var prompt = "Try to do your best and handle appropriately.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Specificity);
        }

        [Fact]
        public void CheckSpecificity_SpecificPrompt_NoFlags()
        {
            var prompt = "List exactly 5 bullet points about photosynthesis in plants.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Specificity);
        }

        [Fact]
        public void CheckSpecificity_MultipleVaguePhrases_AllFlagged()
        {
            var prompt = "Try to do your best. Use common sense. Do the right thing. Whatever works is fine.";
            var report = _refactorer.Analyze(prompt);
            var vague = report.Suggestions.Where(s => s.Category == RefactorCategory.Specificity).ToList();
            Assert.True(vague.Count >= 3);
        }

        // ── Structure Detection ──

        [Fact]
        public void CheckStructure_LongNoSections_Flagged()
        {
            var prompt = string.Join(" ", Enumerable.Range(0, 300).Select(i => $"word{i}"));
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Structure);
        }

        [Fact]
        public void CheckStructure_WithSections_NoFlag()
        {
            var prompt = "# Role\nYou are a helpful assistant.\n\n# Task\nSummarize the document.\n\n# Format\nUse bullet points.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s =>
                s.Category == RefactorCategory.Structure &&
                s.Description.Contains("no clear sections"));
        }

        [Fact]
        public void DetectSections_MarkdownHeaders_Counted()
        {
            var prompt = "# System\nYou are a coder.\n\n## Task\nWrite code.\n\n## Constraints\nBe concise.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.SectionCount >= 3);
        }

        // ── Contradiction Detection ──

        [Fact]
        public void CheckContradictions_ConflictingInstructions_Flagged()
        {
            var prompt = "Be concise in your response. Also be detailed and thorough in your explanation.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Contradiction);
        }

        [Fact]
        public void CheckContradictions_NoConflict_Clean()
        {
            var prompt = "Be concise and clear. Focus on the main points.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Contradiction);
        }

        [Fact]
        public void CheckContradictions_FormalVsInformal_Flagged()
        {
            var prompt = "Be formal in your writing. But also be informal and relaxed.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s =>
                s.Category == RefactorCategory.Contradiction &&
                s.Severity == RefactorSeverity.Error);
        }

        // ── Persona Detection ──

        [Fact]
        public void CheckPersona_NoPersona_Flagged()
        {
            var prompt = "Analyze the following data and provide insights about the trends. Consider seasonal patterns and anomalies in the results.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Persona);
        }

        [Fact]
        public void CheckPersona_HasPersona_NoFlag()
        {
            var prompt = "You are a data scientist. Analyze the following data and provide insights about the trends.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Persona);
        }

        [Fact]
        public void CheckPersona_ActAs_Recognized()
        {
            var prompt = "Act as a senior developer. Review this code for security issues and provide feedback.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Persona);
        }

        // ── Format Detection ──

        [Fact]
        public void CheckFormat_NoFormat_Flagged()
        {
            var prompt = "You are a code reviewer. Analyze this code carefully and tell me about any problems you find in the implementation. Look at the architecture, the design patterns, the error handling, and the overall code quality. Consider edge cases and potential issues.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Format);
        }

        [Fact]
        public void CheckFormat_HasJsonFormat_NoFlag()
        {
            var prompt = "You are a reviewer. Analyze this code. Respond in JSON with fields: issues, severity, line.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Format);
        }

        [Fact]
        public void CheckFormat_BulletPoints_Recognized()
        {
            var prompt = "You are an analyst. List the top risks. Use bullet points for each risk.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Format);
        }

        // ── Length Detection ──

        [Fact]
        public void CheckLength_VeryLongPrompt_Flagged()
        {
            var prompt = string.Join(" ", Enumerable.Range(0, 3000).Select(i => $"word{i}"));
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Length);
        }

        [Fact]
        public void CheckLength_ShortPrompt_NoFlag()
        {
            var report = _refactorer.Analyze("Summarize this article.");
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Length);
        }

        // ── Extract Variable ──

        [Fact]
        public void CheckExtractVariable_RepeatedLiteral_Flagged()
        {
            var prompt = "The model \"gpt-4-turbo\" should be used. Always call \"gpt-4-turbo\" for best results.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.ExtractVariable);
        }

        [Fact]
        public void CheckExtractVariable_RepeatedNumber_Flagged()
        {
            var prompt = "Return at most 50 items. The limit is 50 per page. Show 50 results.";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.ExtractVariable);
        }

        [Fact]
        public void CheckExtractVariable_UniqueValues_NoFlag()
        {
            var prompt = "Use \"gpt-4\" for analysis. Set temperature to 0.7.";
            var report = _refactorer.Analyze(prompt);
            Assert.DoesNotContain(report.Suggestions, s =>
                s.Category == RefactorCategory.ExtractVariable &&
                s.Description.Contains("Literal"));
        }

        // ── Decomposition Detection ──

        [Fact]
        public void CheckDecomposition_ManyTasks_Flagged()
        {
            var prompt = "# Instructions\nStep 1: Read the document.\nStep 2: Identify key themes.\nStep 3: Write a summary.\n" +
                         "Then, review for accuracy. Next, format as a report. Finally, submit for review.\n" +
                         string.Join(" ", Enumerable.Range(0, 400).Select(i => $"extraword{i}"));
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Decomposition);
        }

        [Fact]
        public void CheckDecomposition_ManySections_Flagged()
        {
            var prompt = "# Role\nExpert\n# Context\nData\n# Task\nAnalyze\n# Constraints\nBrief\n# Examples\nSample\n# Format\nJSON\n# Notes\nExtra";
            var report = _refactorer.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Decomposition);
        }

        // ── Grading ──

        [Fact]
        public void Grade_WellStructuredPrompt_GetsAOrB()
        {
            var prompt = "# Role\nYou are a senior code reviewer.\n\n# Task\nReview the pull request for security vulnerabilities.\n\n# Format\nRespond in JSON with fields: issue, severity, line, fix.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.Grade == PromptGrade.A || report.Grade == PromptGrade.B);
        }

        [Fact]
        public void Grade_TerriblePrompt_GetsDOrF()
        {
            var prompt = "Try to do your best and be concise but also be detailed. " +
                         "Use common sense. Handle appropriately. Whatever works. " +
                         "Be formal but also be casual. " +
                         "I would like you to please note that basically in order to " +
                         "figure it out you should do the right thing.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.Grade == PromptGrade.D || report.Grade == PromptGrade.F);
        }

        // ── Auto-Fix ──

        [Fact]
        public void ApplyFixes_RemovesFillerPhrase()
        {
            var prompt = "I would like you to summarize this article about climate change.";
            var (refactored, applied) = _refactorer.ApplyFixes(prompt);
            Assert.True(applied.Count > 0);
            Assert.DoesNotContain("I would like you to", refactored);
        }

        [Fact]
        public void ApplyFixes_PreservesNonFixableText()
        {
            var prompt = "Try to summarize this.";
            var (refactored, _) = _refactorer.ApplyFixes(prompt);
            Assert.Contains("summarize", refactored);
        }

        [Fact]
        public void ApplyFixes_EmptyPrompt_Throws()
        {
            Assert.Throws<ArgumentException>(() => _refactorer.ApplyFixes(""));
        }

        // ── Comparison ──

        [Fact]
        public void Compare_ImprovedPrompt_PositiveDelta()
        {
            var before = "Try to do your best. Use common sense. Handle appropriately.";
            var after = "You are an expert. Analyze the data and list 3 key findings in bullet points.";
            var comparison = _refactorer.Compare(before, after);
            Assert.True(comparison.IsImprovement);
            Assert.True(comparison.ScoreDelta > 0);
        }

        [Fact]
        public void Compare_WorsenedPrompt_NegativeDelta()
        {
            var before = "You are a coder. Write a function that sorts an array. Return JSON.";
            var after = "Try to do your best and be concise but also be detailed. Use common sense. Whatever works.";
            var comparison = _refactorer.Compare(before, after);
            Assert.True(comparison.ScoreDelta <= 0);
        }

        [Fact]
        public void Compare_FixedIssues_Listed()
        {
            var before = "Try to do your best.";
            var after = "List exactly 3 improvements.";
            var comparison = _refactorer.Compare(before, after);
            Assert.True(comparison.FixedIssues.Count > 0);
        }

        [Fact]
        public void Compare_ToTextReport_ContainsScores()
        {
            var comparison = _refactorer.Compare("Do your best.", "List 3 items.");
            var text = comparison.ToTextReport();
            Assert.Contains("Before:", text);
            Assert.Contains("After:", text);
        }

        // ── Batch Analysis ──

        [Fact]
        public void BatchAnalyze_MultiplePrompts_SortedByScore()
        {
            var prompts = new[]
            {
                "You are an expert. Summarize the article. Respond in bullet points.",
                "Try to do your best and whatever works. Handle appropriately. Use common sense.",
                "List 5 items."
            };
            var results = _refactorer.BatchAnalyze(prompts);
            Assert.Equal(3, results.Count);
            Assert.True(results[0].Score <= results[1].Score);
            Assert.True(results[1].Score <= results[2].Score);
        }

        [Fact]
        public void BatchAnalyze_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _refactorer.BatchAnalyze(null!));
        }

        [Fact]
        public void BatchAnalyze_SkipsBlankPrompts()
        {
            var results = _refactorer.BatchAnalyze(new[] { "Hello.", "", "   ", "World." });
            Assert.Equal(2, results.Count);
        }

        // ── Configuration ──

        [Fact]
        public void Config_SkipCategories_Respected()
        {
            var config = new RefactorerConfig
            {
                SkipCategories = new HashSet<RefactorCategory> { RefactorCategory.Specificity }
            };
            var r = new PromptRefactorer(config);
            var report = r.Analyze("Try to do your best and handle appropriately.");
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Specificity);
        }

        [Fact]
        public void Config_DisablePersonaCheck()
        {
            var config = new RefactorerConfig { CheckPersona = false };
            var r = new PromptRefactorer(config);
            var report = r.Analyze("Analyze the data and provide insights about the trends in the chart.");
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Persona);
        }

        [Fact]
        public void Config_DisableOutputFormatCheck()
        {
            var config = new RefactorerConfig { CheckOutputFormat = false };
            var r = new PromptRefactorer(config);
            var report = r.Analyze("You are a reviewer. Analyze this code and tell me about problems in the codebase.");
            Assert.DoesNotContain(report.Suggestions, s => s.Category == RefactorCategory.Format);
        }

        [Fact]
        public void Config_CustomMaxTokens()
        {
            var config = new RefactorerConfig { MaxRecommendedTokens = 50 };
            var r = new PromptRefactorer(config);
            var prompt = string.Join(" ", Enumerable.Range(0, 80).Select(i => $"word{i}"));
            var report = r.Analyze(prompt);
            Assert.Contains(report.Suggestions, s => s.Category == RefactorCategory.Length);
        }

        // ── Report Output ──

        [Fact]
        public void ToTextReport_ContainsScore()
        {
            var report = _refactorer.Analyze("Do your best. Handle appropriately.");
            var text = report.ToTextReport();
            Assert.Contains("Score:", text);
            Assert.Contains("REFACTORING REPORT", text);
        }

        [Fact]
        public void ToJson_ValidJson()
        {
            var report = _refactorer.Analyze("Summarize this text.");
            var json = report.ToJson();
            Assert.Contains("\"score\"", json);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
            Assert.True(parsed.GetProperty("score").GetInt32() >= 0);
        }

        [Fact]
        public void ToTextReport_CleanPrompt_ShowsGreatMessage()
        {
            var report = _refactorer.Analyze("Summarize.");
            if (report.Suggestions.Count == 0)
            {
                var text = report.ToTextReport();
                Assert.Contains("looks great", text);
            }
        }

        // ── ByCategory grouping ──

        [Fact]
        public void ByCategory_GroupsSuggestions()
        {
            var prompt = "Try to do your best. I would like you to handle appropriately. Use common sense.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.ByCategory.Count > 0);
        }

        // ── Severity Counts ──

        [Fact]
        public void ErrorCount_WithContradictions_GreaterThanZero()
        {
            var prompt = "Be concise. Be detailed and thorough.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.ErrorCount > 0);
        }

        // ── Edge Cases ──

        [Fact]
        public void Analyze_SingleWord_Works()
        {
            var report = _refactorer.Analyze("Summarize");
            Assert.NotNull(report);
            Assert.True(report.Score >= 0);
        }

        [Fact]
        public void Analyze_OnlyWhitespace_Throws()
        {
            Assert.Throws<ArgumentException>(() => _refactorer.Analyze("\t\n  "));
        }

        [Fact]
        public void Analyze_PromptWithVariables_CountsCorrectly()
        {
            var prompt = "{{role}} should help {{user}} with {{topic}} about {{subject}}.";
            var report = _refactorer.Analyze(prompt);
            Assert.Equal(4, report.VariableCount);
        }

        [Fact]
        public void HasAutoFixes_WhenFillerPresent_True()
        {
            var prompt = "Please note that the data should be analyzed carefully.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.HasAutoFixes);
        }

        [Fact]
        public void Analyze_UnicodePrompt_Works()
        {
            var prompt = "你是一个专家。分析以下数据并提供见解。";
            var report = _refactorer.Analyze(prompt);
            Assert.NotNull(report);
            Assert.True(report.Score >= 0);
        }

        [Fact]
        public void Analyze_PromptWithNewlines_DetectsSections()
        {
            var prompt = "# Instructions\nDo X.\n\n# Output\nReturn JSON.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.SectionCount >= 2);
        }

        [Fact]
        public void Compare_SamePrompt_ZeroDelta()
        {
            var prompt = "Summarize this article.";
            var comparison = _refactorer.Compare(prompt, prompt);
            Assert.Equal(0, comparison.ScoreDelta);
        }

        [Fact]
        public void ApplyFixes_MultipleFillers_AllRemoved()
        {
            var prompt = "Basically, I would like you to please note that this is essentially the main point.";
            var (refactored, applied) = _refactorer.ApplyFixes(prompt);
            Assert.True(applied.Count >= 1);
            Assert.True(refactored.Length < prompt.Length);
        }

        [Fact]
        public void Config_Default_ReturnsInstance()
        {
            var config = RefactorerConfig.Default;
            Assert.Equal(3, config.MinRepeatThreshold);
            Assert.Equal(4, config.MinPhraseLength);
            Assert.Equal(2000, config.MaxRecommendedTokens);
        }

        [Fact]
        public void Grade_ScoreMapping_Correct()
        {
            // A well-structured prompt should get A or B
            var prompt = "# Role\nYou are an expert analyst.\n\n# Task\nAnalyze the data.\n\n# Format\nRespond in bullet points.";
            var report = _refactorer.Analyze(prompt);
            Assert.True(report.Grade <= PromptGrade.B);
        }

        [Fact]
        public void RefactorSuggestion_HasAutoFix_WhenSuggestionSet()
        {
            var s = new RefactorSuggestion { Suggestion = "fix" };
            Assert.True(s.HasAutoFix);
        }

        [Fact]
        public void RefactorSuggestion_NoAutoFix_WhenSuggestionNull()
        {
            var s = new RefactorSuggestion { Suggestion = null };
            Assert.False(s.HasAutoFix);
        }

        [Fact]
        public void Analyze_LongSectionInMarkdown_Flagged()
        {
            var longContent = string.Join(" ", Enumerable.Range(0, 800).Select(i => $"content{i}"));
            var prompt = $"# Big Section\n{longContent}";
            var config = new RefactorerConfig { MaxSectionTokens = 200 };
            var r = new PromptRefactorer(config);
            var report = r.Analyze(prompt);
            Assert.Contains(report.Suggestions, s =>
                s.Category == RefactorCategory.Length &&
                s.Description.Contains("Section"));
        }
    }
}
