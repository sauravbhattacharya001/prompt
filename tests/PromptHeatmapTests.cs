namespace Prompt.Tests
{
    using Xunit;
    using System.Linq;

    public class PromptHeatmapTests
    {
        private readonly PromptHeatmap _heatmap = new();

        [Fact]
        public void Analyze_EmptyPrompt_ReturnsEmptyResult()
        {
            var result = _heatmap.Analyze("");
            Assert.Empty(result.Segments);
            Assert.Equal(0, result.MeanHeat);
        }

        [Fact]
        public void Analyze_SimplePrompt_ReturnsSegments()
        {
            var result = _heatmap.Analyze("Hello world, this is a simple prompt.");
            Assert.NotEmpty(result.Segments);
            Assert.All(result.Segments, s => Assert.InRange(s.Heat, 0.0, 1.0));
        }

        [Fact]
        public void Analyze_InstructionHeavyPrompt_HasHigherInstructionScore()
        {
            var instructive = "You must always ensure the output is formatted correctly. Never include extra text. Always respond strictly.";
            var casual = "Tell me a fun story about cats and dogs playing in the park on a sunny day please.";

            var instrResult = _heatmap.Analyze(instructive, segmentSize: 200);
            var casualResult = _heatmap.Analyze(casual, segmentSize: 200);

            var instrScore = instrResult.Segments[0].Dimensions["instruction"];
            var casualScore = casualResult.Segments[0].Dimensions["instruction"];

            Assert.True(instrScore > casualScore, $"Instruction score {instrScore} should be higher than {casualScore}");
        }

        [Fact]
        public void Analyze_VariableHeavyPrompt_HasHigherVariableScore()
        {
            var withVars = "Hello {{name}}, your order {{orderId}} has been shipped to {{address}}.";
            var noVars = "Hello friend, your order has been shipped to your home address today.";

            var varsResult = _heatmap.Analyze(withVars, segmentSize: 200);
            var noVarsResult = _heatmap.Analyze(noVars, segmentSize: 200);

            Assert.True(varsResult.Segments[0].Dimensions["variables"] > noVarsResult.Segments[0].Dimensions["variables"]);
        }

        [Fact]
        public void Analyze_EmphasisDetection_CapsAndBold()
        {
            var emphatic = "This is VERY IMPORTANT and **critical** information!";
            var plain = "This is some regular information for you.";

            var empResult = _heatmap.Analyze(emphatic, segmentSize: 200);
            var plainResult = _heatmap.Analyze(plain, segmentSize: 200);

            Assert.True(empResult.Segments[0].Dimensions["emphasis"] > plainResult.Segments[0].Dimensions["emphasis"]);
        }

        [Fact]
        public void Analyze_HotspotAndColdZoneCounting()
        {
            // Create a prompt with varied content
            var prompt = "You MUST always ensure correct output.\n\n" +
                         "Just some filler text here nothing special at all.\n\n" +
                         "**CRITICAL**: Never {{ignore}} the {{rules}} — strictly follow ALL instructions!";

            var result = _heatmap.Analyze(prompt, segmentSize: 40);
            Assert.True(result.Segments.Count >= 2);
            Assert.Equal(result.Segments.Count(s => s.Heat > 0.7), result.HotspotCount);
            Assert.Equal(result.Segments.Count(s => s.Heat < 0.2), result.ColdZoneCount);
        }

        [Fact]
        public void ToHtml_ProducesValidHtml()
        {
            var html = _heatmap.ToHtml("You must always return JSON. Use {{format}} template.", title: "Test");
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("<title>Test</title>", html);
            Assert.Contains("class=\"heatmap\"", html);
            Assert.Contains("class=\"segment\"", html);
            Assert.Contains("Dimension Averages", html);
        }

        [Fact]
        public void ToText_ProducesBlockVisualization()
        {
            var text = _heatmap.ToText("Analyze this prompt for heatmap testing purposes. You must follow all rules strictly.");
            Assert.Contains("PROMPT HEATMAP", text);
            Assert.Contains("Heat:", text);
            Assert.Contains("Top 5 hottest segments:", text);
        }

        [Fact]
        public void Analyze_SegmentsDontOverlap()
        {
            var prompt = "This is a test prompt with enough text to produce multiple segments for analysis purposes.";
            var result = _heatmap.Analyze(prompt, segmentSize: 20);

            for (int i = 1; i < result.Segments.Count; i++)
            {
                var prev = result.Segments[i - 1];
                var curr = result.Segments[i];
                Assert.Equal(prev.Start + prev.Length, curr.Start);
            }
        }

        [Fact]
        public void Analyze_AllDimensionsPresent()
        {
            var result = _heatmap.Analyze("Test prompt content here.");
            var expected = new[] { "instruction", "variables", "complexity", "structure", "emphasis" };
            foreach (var seg in result.Segments)
            {
                foreach (var dim in expected)
                {
                    Assert.True(seg.Dimensions.ContainsKey(dim), $"Missing dimension: {dim}");
                }
            }
        }
    }
}
