using System;
using System.Linq;
using Prompt;
using Xunit;

namespace Prompt.Tests
{
    public class PromptGoalPlannerTests
    {
        private readonly PromptGoalPlanner _planner = new PromptGoalPlanner();

        [Fact]
        public void Decompose_CreatesValidPlan()
        {
            var plan = _planner.Decompose("Build a REST API for user management");
            Assert.NotNull(plan);
            Assert.True(plan.Tasks.Count >= 3);
            Assert.Equal("Build a REST API for user management", plan.Goal);
            Assert.True(plan.TotalEstimatedTokens > 0);
        }

        [Theory]
        [InlineData(DecompositionStrategy.Sequential)]
        [InlineData(DecompositionStrategy.Parallel)]
        [InlineData(DecompositionStrategy.Recursive)]
        [InlineData(DecompositionStrategy.Balanced)]
        public void Decompose_AllStrategiesProduceValidPlans(DecompositionStrategy strategy)
        {
            var plan = _planner.Decompose("Build an app", strategy);
            var validation = _planner.Validate(plan);
            Assert.True(validation.IsValid, string.Join("; ", validation.Issues));
        }

        [Fact]
        public void Decompose_DetectsApiDomain()
        {
            var plan = _planner.Decompose("Create a REST API endpoint for orders");
            Assert.Contains(plan.Tasks, t => t.Name.Contains("endpoint", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("model", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("API", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Decompose_HasReadyTasks()
        {
            var plan = _planner.Decompose("Write a blog post about AI safety");
            Assert.True(plan.ReadyTasks.Count > 0, "Plan should have at least one ready task.");
        }

        [Fact]
        public void CompleteTask_AdvancesPlan()
        {
            var plan = _planner.Decompose("Debug a crash in production", DecompositionStrategy.Sequential);
            var ready = plan.ReadyTasks;
            Assert.True(ready.Count > 0);

            var first = ready[0];
            var advancement = _planner.CompleteTask(plan, first.Id, "Issue reproduced.");
            Assert.True(advancement.NewlyReady.Count > 0 || advancement.PlanComplete);
        }

        [Fact]
        public void FailTask_SkipsDependents()
        {
            var plan = _planner.Decompose("Learn machine learning", DecompositionStrategy.Sequential);
            var first = plan.ReadyTasks[0];

            var advancement = _planner.FailTask(plan, first.Id, "Could not access resources");
            Assert.True(advancement.Skipped.Count > 0 || plan.Tasks.Count(t => t.Status == PlanTaskStatus.Skipped) > 0);
        }

        [Fact]
        public void StartTask_SetsRunning()
        {
            var plan = _planner.Decompose("Analyze sales data");
            var ready = plan.ReadyTasks[0];
            _planner.StartTask(plan, ready.Id);
            Assert.Equal(PlanTaskStatus.Running, ready.Status);
        }

        [Fact]
        public void GenerateRecoveryTasks_ProducesAlternatives()
        {
            var plan = _planner.Decompose("Build an app", DecompositionStrategy.Sequential);
            var first = plan.ReadyTasks[0];
            _planner.FailTask(plan, first.Id, "Compilation error");

            var recovery = _planner.GenerateRecoveryTasks(plan, first.Id);
            Assert.True(recovery.Count >= 2);
            Assert.Contains(recovery, r => r.Tags.Contains("recovery"));
        }

        [Fact]
        public void ApplyRecovery_RewiresGraph()
        {
            var plan = _planner.Decompose("Build an app", DecompositionStrategy.Sequential);
            var first = plan.ReadyTasks[0];
            _planner.FailTask(plan, first.Id, "Error");

            var recovery = _planner.GenerateRecoveryTasks(plan, first.Id);
            int countBefore = plan.Tasks.Count;
            _planner.ApplyRecovery(plan, first.Id, recovery);
            Assert.True(plan.Tasks.Count > countBefore);
        }

        [Fact]
        public void Validate_DetectsNoIssues()
        {
            var plan = _planner.Decompose("Write documentation");
            var result = _planner.Validate(plan);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ExportMarkdown_ProducesOutput()
        {
            var plan = _planner.Decompose("Build a REST API");
            var md = _planner.ExportMarkdown(plan);
            Assert.Contains("# Execution Plan", md);
            Assert.Contains("mermaid", md);
            Assert.Contains("task-1", md);
        }

        [Fact]
        public void ExportJson_ProducesOutput()
        {
            var plan = _planner.Decompose("Analyze user behavior");
            var json = _planner.ExportJson(plan);
            Assert.Contains("\"planId\"", json);
            Assert.Contains("\"tasks\"", json);
        }

        [Fact]
        public void Decompose_ThrowsOnEmptyGoal()
        {
            Assert.Throws<ArgumentException>(() => _planner.Decompose(""));
        }

        [Fact]
        public void Decompose_ThrowsOnInvalidMaxTasks()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _planner.Decompose("Goal", maxTasks: 1));
        }

        [Fact]
        public void FullLifecycle_CompletesAllTasks()
        {
            var plan = _planner.Decompose("Fix a bug", DecompositionStrategy.Sequential, maxTasks: 4);

            while (!plan.IsComplete)
            {
                var ready = plan.ReadyTasks;
                if (ready.Count == 0) break;
                foreach (var task in ready)
                {
                    _planner.StartTask(plan, task.Id);
                    _planner.CompleteTask(plan, task.Id, $"Result for {task.Name}");
                }
            }

            Assert.True(plan.IsComplete);
            Assert.Equal(1.0, plan.Progress);
        }
    }
}
