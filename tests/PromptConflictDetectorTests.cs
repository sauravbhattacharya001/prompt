namespace Prompt.Tests
{
    using System.Collections.Generic;
    using Xunit;

    public class PromptConflictDetectorTests
    {
        private readonly PromptConflictDetector _detector = new();

        [Fact]
        public void Analyze_EmptyText_ReturnsNoConflicts()
        {
            var report = _detector.Analyze("");
            Assert.False(report.HasConflicts);
        }

        [Fact]
        public void Analyze_DetectsAntonymContradiction()
        {
            var report = _detector.Analyze("Be concise in your answers. Always provide detailed explanations.");
            Assert.True(report.HasConflicts);
            Assert.Contains(report.Conflicts, c => c.Type == ConflictType.DirectContradiction);
        }

        [Fact]
        public void AnalyzeMultiple_DetectsCrossPromptContradiction()
        {
            var prompts = new Dictionary<string, string>
            {
                ["system"] = "You are a formal assistant.",
                ["preamble"] = "Be casual and relaxed in your responses."
            };

            var report = _detector.AnalyzeMultiple(prompts);
            Assert.True(report.HasConflicts);
        }

        [Fact]
        public void Analyze_DetectsNumericConflict()
        {
            var report = _detector.Analyze("Use at most 50 words. Use at least 200 words.");
            Assert.True(report.HasConflicts);
            Assert.Contains(report.Conflicts, c => c.Type == ConflictType.NumericConflict);
        }

        [Fact]
        public void Analyze_DetectsRoleConflict()
        {
            var prompts = new Dictionary<string, string>
            {
                ["system"] = "You are a pirate captain.",
                ["override"] = "Act as a formal legal advisor."
            };

            var report = _detector.AnalyzeMultiple(prompts);
            Assert.Contains(report.Conflicts, c => c.Type == ConflictType.RoleConflict);
        }

        [Fact]
        public void Analyze_NoConflictsForConsistentPrompt()
        {
            var report = _detector.Analyze("Be helpful and friendly. Answer questions clearly.");
            Assert.False(report.HasConflicts);
        }

        [Fact]
        public void ToSummary_FormatsCorrectly()
        {
            var report = _detector.Analyze("Be concise. Be verbose and detailed.");
            var summary = report.ToSummary();
            Assert.Contains("conflict", summary, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ToJson_SerializesCorrectly()
        {
            var report = _detector.Analyze("Be concise. Be verbose.");
            var json = report.ToJson();
            Assert.Contains("DirectContradiction", json);
        }
    }
}
