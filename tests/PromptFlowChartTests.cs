namespace Prompt.Tests
{
    using Xunit;
    using Prompt;
    using System.Linq;

    public class PromptFlowChartTests
    {
        [Fact]
        public void AddNode_And_Render_ProducesValidMermaid()
        {
            var chart = new PromptFlowChart("Test")
                .AddNode("a", "Start", FlowNodeShape.Stadium)
                .AddNode("b", "Process")
                .AddEdge("a", "b");

            var result = chart.Render();
            Assert.Contains("flowchart TB", result);
            Assert.Contains("a([Start])", result);
            Assert.Contains("b[Process]", result);
            Assert.Contains("a --> b", result);
        }

        [Fact]
        public void AddNode_Duplicate_Throws()
        {
            var chart = new PromptFlowChart();
            chart.AddNode("a", "Node A");
            Assert.Throws<System.InvalidOperationException>(() => chart.AddNode("a", "Dup"));
        }

        [Fact]
        public void Direction_LeftToRight_RendersLR()
        {
            var chart = new PromptFlowChart("LR Test", FlowDirection.LeftToRight);
            chart.AddNode("x", "X");
            Assert.Contains("flowchart LR", chart.Render());
        }

        [Fact]
        public void EdgeWithLabel_RendersCorrectly()
        {
            var chart = new PromptFlowChart()
                .AddNode("a", "A")
                .AddNode("b", "B")
                .AddEdge("a", "b", "yes");

            Assert.Contains("-->|yes|", chart.Render());
        }

        [Fact]
        public void DottedEdge_RendersDotted()
        {
            var chart = new PromptFlowChart()
                .AddNode("a", "A")
                .AddNode("b", "B")
                .AddEdge("a", "b", "retry", dotted: true);

            Assert.Contains("-.->|retry|", chart.Render());
        }

        [Fact]
        public void DiamondShape_RendersCorrectly()
        {
            var chart = new PromptFlowChart()
                .AddNode("d", "Decide?", FlowNodeShape.Diamond);

            Assert.Contains("d{Decide?}", chart.Render());
        }

        [Fact]
        public void Subgraph_RendersCorrectly()
        {
            var chart = new PromptFlowChart()
                .AddNode("a", "A")
                .AddNode("b", "B")
                .AddSubgraph("Group 1", "a", "b");

            var result = chart.Render();
            Assert.Contains("subgraph Group 1", result);
            Assert.Contains("end", result);
        }

        [Fact]
        public void Validate_MissingNode_ReportsIssue()
        {
            var chart = new PromptFlowChart()
                .AddNode("a", "A")
                .AddEdge("a", "missing");

            var issues = chart.Validate();
            Assert.Contains(issues, i => i.Contains("missing"));
        }

        [Fact]
        public void Validate_Cycle_Detected()
        {
            var chart = new PromptFlowChart()
                .AddNode("a", "A")
                .AddNode("b", "B")
                .AddEdge("a", "b")
                .AddEdge("b", "a");

            var issues = chart.Validate();
            Assert.Contains(issues, i => i.Contains("cycle"));
        }

        [Fact]
        public void Validate_ValidChart_NoIssues()
        {
            var chart = new PromptFlowChart()
                .AddNode("a", "A")
                .AddNode("b", "B")
                .AddEdge("a", "b");

            Assert.Empty(chart.Validate());
        }

        [Fact]
        public void RenderMarkdown_ContainsMermaidBlock()
        {
            var chart = new PromptFlowChart("MD Test")
                .AddNode("a", "A");

            var md = chart.RenderMarkdown();
            Assert.Contains("```mermaid", md);
            Assert.Contains("# MD Test", md);
        }

        [Fact]
        public void RenderHtml_ContainsMermaidScript()
        {
            var chart = new PromptFlowChart("HTML Test")
                .AddNode("a", "A");

            var html = chart.RenderHtml();
            Assert.Contains("mermaid", html);
            Assert.Contains("<html", html);
            Assert.Contains("HTML Test", html);
        }

        [Fact]
        public void FromChain_CreatesFlowchart()
        {
            var chain = new PromptChain();
            chain.AddStep("Summarize", new PromptTemplate("Summarize: {{input}}"), "summary");
            chain.AddStep("Translate", new PromptTemplate("Translate: {{summary}}"), "translation");

            var chart = PromptFlowChart.FromChain(chain);
            var result = chart.Render();

            Assert.Contains("start", result);
            Assert.Contains("end", result);
            Assert.Contains("Summarize", result);
            Assert.Contains("Translate", result);
            Assert.Equal(4, chart.Nodes.Count); // start + 2 steps + end
            Assert.Equal(3, chart.Edges.Count); // start->s0, s0->s1, s1->end
        }

        [Fact]
        public void FromWorkflow_CreatesFlowchart()
        {
            var workflow = new PromptWorkflow();
            var extractNode = new WorkflowNode("extract", "Extract", "extracted",
                new PromptTemplate("Extract from {{input}}"));
            workflow.AddNode(extractNode);
            var analyzeNode = new WorkflowNode("analyze", "Analyze", "analysis",
                new PromptTemplate("Analyze: {{extracted}}"));
            analyzeNode.DependsOn.Add("extract");
            workflow.AddNode(analyzeNode);

            var chart = PromptFlowChart.FromWorkflow(workflow);
            var result = chart.Render();

            Assert.Contains("extract", result);
            Assert.Contains("analyze", result);
            Assert.Equal(2, chart.Nodes.Count);
            Assert.Single(chart.Edges);
        }

        [Fact]
        public void Styles_AppliedCorrectly()
        {
            var chart = new PromptFlowChart()
                .AddNode("a", "A", cssClass: "highlight")
                .AddStyle("highlight", "fill:#ff0,stroke:#000");

            var result = chart.Render();
            Assert.Contains("classDef highlight fill:#ff0,stroke:#000", result);
            Assert.Contains("class a highlight", result);
        }

        [Fact]
        public void AllShapes_RenderWithoutError()
        {
            var chart = new PromptFlowChart();
            chart.AddNode("rect", "Rect", FlowNodeShape.Rectangle);
            chart.AddNode("round", "Round", FlowNodeShape.Rounded);
            chart.AddNode("stad", "Stadium", FlowNodeShape.Stadium);
            chart.AddNode("dia", "Diamond", FlowNodeShape.Diamond);
            chart.AddNode("hex", "Hex", FlowNodeShape.Hexagon);
            chart.AddNode("circ", "Circle", FlowNodeShape.Circle);
            chart.AddNode("trap", "Trap", FlowNodeShape.Trapezoid);

            var result = chart.Render();
            Assert.Equal(7, chart.Nodes.Count);
            Assert.NotEmpty(result);
        }
    }
}
