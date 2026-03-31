namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class PromptConversationSimulatorTests
    {
        [Fact]
        public void Run_BasicScript_ProducesTranscript()
        {
            var sim = new PromptConversationSimulator();
            sim.AddSystem("You are a helpful assistant.");
            sim.AddUser("Hello!");
            sim.AddAssistant("Hi there! How can I help?");

            var result = sim.Run();

            Assert.Equal(3, result.Transcript.Count);
            Assert.Equal(3, result.TurnsExecuted);
            Assert.True(result.Success);
            Assert.Equal(SimRole.System, result.Transcript[0].Role);
            Assert.Equal(SimRole.User, result.Transcript[1].Role);
            Assert.Equal(SimRole.Assistant, result.Transcript[2].Role);
        }

        [Fact]
        public void Run_VariableInterpolation_RendersCorrectly()
        {
            var sim = new PromptConversationSimulator();
            sim.AddSystem("You are a {{persona}} assistant.");
            sim.AddUser("My name is {{name}}.");
            sim.AddAssistant("Hello {{name}}!");

            var result = sim.Run(new Dictionary<string, string>
            {
                ["persona"] = "friendly",
                ["name"] = "Alice"
            });

            Assert.Contains("friendly", result.Transcript[0].Content);
            Assert.Contains("Alice", result.Transcript[1].Content);
            Assert.Contains("Alice", result.Transcript[2].Content);
            Assert.DoesNotContain("{{", result.Transcript[0].Content);
        }

        [Fact]
        public void Run_UnsetVariable_PreservesPlaceholder()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Hello {{unknown}}!");

            var result = sim.Run();

            Assert.Contains("{{unknown}}", result.Transcript[0].Content);
        }

        [Fact]
        public void Run_ConditionTrue_IncludesTurn()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Base message.");
            sim.AddUser("Conditional message.", condition: "showExtra");

            var result = sim.Run(new Dictionary<string, string> { ["showExtra"] = "true" });

            Assert.Equal(2, result.TurnsExecuted);
            Assert.Equal(0, result.TurnsSkipped);
        }

        [Fact]
        public void Run_ConditionFalse_SkipsTurn()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Base message.");
            sim.AddUser("Conditional message.", condition: "showExtra");

            var result = sim.Run(); // no variables

            Assert.Equal(1, result.TurnsExecuted);
            Assert.Equal(1, result.TurnsSkipped);
        }

        [Fact]
        public void Run_ConditionFalseString_SkipsTurn()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Conditional.", condition: "flag");

            var result = sim.Run(new Dictionary<string, string> { ["flag"] = "false" });

            Assert.Equal(0, result.TurnsExecuted);
            Assert.Equal(1, result.TurnsSkipped);
        }

        [Fact]
        public void Run_ConditionZero_SkipsTurn()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Conditional.", condition: "flag");

            var result = sim.Run(new Dictionary<string, string> { ["flag"] = "0" });

            Assert.Equal(0, result.TurnsExecuted);
        }

        [Fact]
        public void Run_Branching_JumpsToLabel()
        {
            var sim = new PromptConversationSimulator();
            sim.AddSystem("System message.");
            sim.AddUser("Branch here.", branchTo: "target");
            sim.AddAssistant("This should be skipped.");
            sim.AddUser("Target turn.", label: "target");
            sim.AddAssistant("After target.");

            var result = sim.Run();

            Assert.Equal(4, result.TurnsExecuted); // system, branch-user, target-user, after-target
            Assert.Equal(1, result.BranchesTaken);
            // "This should be skipped." should not appear
            Assert.DoesNotContain(result.Transcript, m => m.Content == "This should be skipped.");
        }

        [Fact]
        public void Run_ExpectPattern_PassesOnMatch()
        {
            var sim = new PromptConversationSimulator();
            sim.AddAssistant("The answer is 42.");
            sim.AddUser("Good.", expectPattern: @"\d+");

            var result = sim.Run();

            Assert.True(result.Success);
            Assert.Empty(result.ValidationFailures);
        }

        [Fact]
        public void Run_ExpectPattern_FailsOnMismatch()
        {
            var sim = new PromptConversationSimulator();
            sim.AddAssistant("No numbers here.");
            sim.AddUser("Check.", expectPattern: @"^\d+$");

            var result = sim.Run();

            Assert.False(result.Success);
            Assert.Single(result.ValidationFailures);
        }

        [Fact]
        public void Run_MaxTokensWarning_RecordsWarning()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Short text.", maxTokens: 1);

            var result = sim.Run();

            Assert.Single(result.Warnings);
            Assert.Contains("exceeds limit", result.Warnings[0]);
        }

        [Fact]
        public void Run_EmptyScript_ReturnsEmptyResult()
        {
            var sim = new PromptConversationSimulator();
            var result = sim.Run();

            Assert.Empty(result.Transcript);
            Assert.Equal(0, result.TurnsExecuted);
            Assert.True(result.Success);
        }

        [Fact]
        public void MaxTurns_PreventsInfiniteLoop()
        {
            var sim = new PromptConversationSimulator { MaxTurns = 5 };
            // Create a loop: turn 0 branches to turn 0
            sim.AddUser("Loop.", label: "start", branchTo: "start");

            var result = sim.Run();

            Assert.Equal(5, result.TurnsExecuted);
            Assert.Contains(result.Warnings, w => w.Contains("MaxTurns"));
        }

        [Fact]
        public void MaxTurns_ThrowsOnInvalidValue()
        {
            var sim = new PromptConversationSimulator();
            Assert.Throws<ArgumentOutOfRangeException>(() => sim.MaxTurns = 0);
        }

        [Fact]
        public void ToText_FormatsReadably()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Hello.");
            sim.AddAssistant("Hi!");

            var result = sim.Run();
            string text = result.ToText();

            Assert.Contains("[User] Hello.", text);
            Assert.Contains("[Assistant] Hi!", text);
        }

        [Fact]
        public void ToChatJson_ProducesValidJson()
        {
            var sim = new PromptConversationSimulator();
            sim.AddSystem("System.");
            sim.AddUser("User.");

            var result = sim.Run();
            string json = result.ToChatJson();

            Assert.Contains("\"messages\"", json);
            Assert.Contains("\"system\"", json);
            Assert.Contains("\"user\"", json);
        }

        [Fact]
        public void ToJsonl_ProducesJsonl()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Hello.");
            sim.AddAssistant("Hi.");

            var result = sim.Run();
            string jsonl = result.ToJsonl();

            Assert.Contains("\"messages\"", jsonl);
            // Should be a single line (plus trailing newline)
            var lines = jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines);
        }

        [Fact]
        public void ToJson_RoundTrips()
        {
            var sim = new PromptConversationSimulator();
            sim.AddSystem("System {{var}}.", condition: "hasVar");
            sim.AddUser("User turn.", label: "start");
            sim.AddAssistant("Response.");

            string json = sim.ToJson();
            var restored = PromptConversationSimulator.FromJson(json);

            Assert.Equal(3, restored.Script.Count);
            Assert.Equal(SimRole.System, restored.Script[0].Role);
            Assert.Equal("hasVar", restored.Script[0].Condition);
            Assert.Equal("start", restored.Script[1].Label);
        }

        [Fact]
        public void FromJson_ThrowsOnEmpty()
        {
            Assert.Throws<ArgumentException>(() => PromptConversationSimulator.FromJson(""));
            Assert.Throws<ArgumentException>(() => PromptConversationSimulator.FromJson("  "));
        }

        [Fact]
        public void RunScenarios_RunsMultipleVariableSets()
        {
            var sim = new PromptConversationSimulator();
            sim.AddSystem("You help with {{topic}}.");
            sim.AddUser("Tell me about {{topic}}.");

            var results = sim.RunScenarios(new Dictionary<string, Dictionary<string, string>>
            {
                ["coding"] = new() { ["topic"] = "C#" },
                ["cooking"] = new() { ["topic"] = "pasta" }
            });

            Assert.Equal(2, results.Count);
            Assert.Contains("C#", results["coding"].Transcript[0].Content);
            Assert.Contains("pasta", results["cooking"].Transcript[0].Content);
        }

        [Fact]
        public void RunScenarios_ThrowsOnNull()
        {
            var sim = new PromptConversationSimulator();
            Assert.Throws<ArgumentNullException>(() => sim.RunScenarios(null!));
        }

        [Fact]
        public void CompareScenarios_ProducesReport()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Hello {{name}}.");
            sim.AddAssistant("Hi {{name}}!");

            var results = sim.RunScenarios(new Dictionary<string, Dictionary<string, string>>
            {
                ["alice"] = new() { ["name"] = "Alice" },
                ["bob"] = new() { ["name"] = "Bob" }
            });

            string report = PromptConversationSimulator.CompareScenarios(results);

            Assert.Contains("Scenario Comparison", report);
            Assert.Contains("alice", report);
            Assert.Contains("bob", report);
        }

        [Fact]
        public void CompareScenarios_EmptyReturnsMessage()
        {
            string report = PromptConversationSimulator.CompareScenarios(
                new Dictionary<string, SimulationResult>());
            Assert.Equal("No scenarios to compare.", report);
        }

        [Fact]
        public void Clear_RemovesAllTurns()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("A").AddUser("B").AddUser("C");
            Assert.Equal(3, sim.Script.Count);

            sim.Clear();
            Assert.Empty(sim.Script);
        }

        [Fact]
        public void AddTurn_ThrowsOnNull()
        {
            var sim = new PromptConversationSimulator();
            Assert.Throws<ArgumentNullException>(() => sim.AddTurn(null!));
        }

        [Fact]
        public void FinalVariables_CapturesState()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("{{name}}");

            var vars = new Dictionary<string, string> { ["name"] = "Test" };
            var result = sim.Run(vars);

            Assert.Equal("Test", result.FinalVariables["name"]);
        }

        [Fact]
        public void TokenEstimates_ArePositive()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("This is a test message with some words.");

            var result = sim.Run();

            Assert.True(result.TotalEstimatedTokens > 0);
            Assert.True(result.Transcript[0].EstimatedTokens > 0);
        }

        [Fact]
        public void FluentApi_Chains()
        {
            var sim = new PromptConversationSimulator()
                .AddSystem("System.")
                .AddUser("User.")
                .AddAssistant("Assistant.");

            Assert.Equal(3, sim.Script.Count);
        }

        [Fact]
        public void ConditionIsCaseInsensitive()
        {
            var sim = new PromptConversationSimulator();
            sim.AddUser("Conditional.", condition: "MyFlag");

            var result = sim.Run(new Dictionary<string, string> { ["myflag"] = "yes" });

            Assert.Equal(1, result.TurnsExecuted);
        }
    }
}
