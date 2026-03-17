namespace Prompt.Tests
{
    using Xunit;

    public class PromptChainVisualizerTests
    {
        private PromptChain CreateSampleChain()
        {
            var chain = new PromptChain();
            chain.AddStep("extract", new PromptTemplate("Extract entities from: {{input}}"), "entities");
            chain.AddStep("analyze", new PromptTemplate("Analyze these entities: {{entities}}"), "analysis");
            chain.AddStep("summarize", new PromptTemplate("Summarize: {{analysis}}"), "summary");
            return chain;
        }

        [Fact]
        public void Visualize_NullChain_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PromptChainVisualizer.Visualize(null!, ChainVisualizationFormat.Mermaid));
        }

        [Fact]
        public void VisualizeResult_NullResult_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PromptChainVisualizer.VisualizeResult(null!, ChainVisualizationFormat.Mermaid));
        }

        [Fact]
        public void Visualize_InvalidFormat_Throws()
        {
            var chain = CreateSampleChain();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PromptChainVisualizer.Visualize(chain, (ChainVisualizationFormat)99));
        }

        [Fact]
        public void Mermaid_EmptyChain_ShowsEmptyNode()
        {
            var chain = new PromptChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid);
            Assert.Contains("Empty Chain", output);
        }

        [Fact]
        public void Mermaid_ContainsFlowchartHeader()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid);
            Assert.StartsWith("flowchart TB", output);
        }

        [Fact]
        public void Mermaid_ContainsAllStepNames()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid);
            Assert.Contains("extract", output);
            Assert.Contains("analyze", output);
            Assert.Contains("summarize", output);
        }

        [Fact]
        public void Mermaid_ShowsVariables()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid);
            Assert.Contains("entities", output);
            Assert.Contains("analysis", output);
            Assert.Contains("summary", output);
        }

        [Fact]
        public void Mermaid_HidesVariablesWhenDisabled()
        {
            var chain = CreateSampleChain();
            var opts = new ChainVisualizationOptions { ShowVariables = false };
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid, opts);
            Assert.DoesNotContain("-- \"entities\"", output);
        }

        [Fact]
        public void Mermaid_CustomDirection()
        {
            var chain = CreateSampleChain();
            var opts = new ChainVisualizationOptions { MermaidDirection = "LR" };
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid, opts);
            Assert.StartsWith("flowchart LR", output);
        }

        [Fact]
        public void Mermaid_WithLegend()
        {
            var chain = CreateSampleChain();
            var opts = new ChainVisualizationOptions { IncludeLegend = true };
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid, opts);
            Assert.Contains("Legend", output);
            Assert.Contains("Steps: 3", output);
        }

        [Fact]
        public void Dot_EmptyChain_ShowsEmptyNode()
        {
            var chain = new PromptChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Dot);
            Assert.Contains("Empty Chain", output);
        }

        [Fact]
        public void Dot_ContainsDigraph()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Dot);
            Assert.StartsWith("digraph", output);
            Assert.Contains("start", output);
            Assert.Contains("finish", output);
        }

        [Fact]
        public void Dot_ContainsAllSteps()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Dot);
            Assert.Contains("extract", output);
            Assert.Contains("analyze", output);
            Assert.Contains("summarize", output);
        }

        [Fact]
        public void Dot_WithGraphLabel()
        {
            var chain = CreateSampleChain();
            var opts = new ChainVisualizationOptions { GraphLabel = "My Pipeline" };
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Dot, opts);
            Assert.Contains("My Pipeline", output);
        }

        [Fact]
        public void Ascii_EmptyChain_ShowsEmptyMessage()
        {
            var chain = new PromptChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Ascii);
            Assert.Contains("empty chain", output);
        }

        [Fact]
        public void Ascii_ContainsStartAndDone()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Ascii);
            Assert.Contains("Start", output);
            Assert.Contains("Done", output);
        }

        [Fact]
        public void Ascii_ContainsAllStepNames()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Ascii);
            Assert.Contains("extract", output);
            Assert.Contains("analyze", output);
            Assert.Contains("summarize", output);
        }

        [Fact]
        public void Ascii_ContainsBoxCharacters()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Ascii);
            Assert.Contains("┌", output);
            Assert.Contains("┘", output);
            Assert.Contains("│", output);
        }

        [Fact]
        public void Ascii_ShowsVariablesBetweenSteps()
        {
            var chain = CreateSampleChain();
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Ascii);
            Assert.Contains("(entities)", output);
            Assert.Contains("(analysis)", output);
        }

        [Fact]
        public void Ascii_HidesStepNumbersWhenDisabled()
        {
            var chain = CreateSampleChain();
            var opts = new ChainVisualizationOptions { ShowStepNumbers = false };
            var output = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Ascii, opts);
            Assert.DoesNotContain("1.", output);
            Assert.DoesNotContain("2.", output);
        }

        [Fact]
        public void SingleStep_AllFormats_Work()
        {
            var chain = new PromptChain();
            chain.AddStep("only", new PromptTemplate("Do something: {{input}}"), "result");

            foreach (var fmt in new[] { ChainVisualizationFormat.Mermaid, ChainVisualizationFormat.Dot, ChainVisualizationFormat.Ascii })
            {
                var output = PromptChainVisualizer.Visualize(chain, fmt);
                Assert.Contains("only", output);
                Assert.NotEmpty(output);
            }
        }
    }
}
