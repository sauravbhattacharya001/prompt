using Xunit;

namespace Prompt.Tests
{
    public class PromptSchedulerTests
    {
        private PromptTemplate MakeTemplate(string text = "Hello {{name}}")
            => new PromptTemplate(text, new Dictionary<string, string> { ["name"] = "World" });

        [Fact]
        public void Schedule_CreatesActiveJob()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Test Job", MakeTemplate(), "0 * * * *");

            Assert.Equal("Test Job", job.Name);
            Assert.Equal(ScheduleStatus.Active, job.Status);
            Assert.NotNull(job.NextFireTime);
            Assert.Single(scheduler.Jobs);
        }

        [Fact]
        public void Pause_And_Resume_Work()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Job", MakeTemplate(), "0 * * * *");

            Assert.True(scheduler.Pause(job.Id));
            Assert.Equal(ScheduleStatus.Paused, job.Status);

            Assert.True(scheduler.Resume(job.Id));
            Assert.Equal(ScheduleStatus.Active, job.Status);
        }

        [Fact]
        public void Cancel_SetsStatusAndClearsNextFire()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Job", MakeTemplate(), "0 * * * *");

            Assert.True(scheduler.Cancel(job.Id));
            Assert.Equal(ScheduleStatus.Cancelled, job.Status);
            Assert.Null(job.NextFireTime);
        }

        [Fact]
        public void Remove_DeletesJob()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Job", MakeTemplate(), "0 * * * *");

            Assert.True(scheduler.Remove(job.Id));
            Assert.Empty(scheduler.Jobs);
        }

        [Fact]
        public void ListJobs_FiltersCorrectly()
        {
            var scheduler = new PromptScheduler();
            var job1 = scheduler.Schedule("Active", MakeTemplate(), "0 * * * *", tags: new[] { "prod" });
            var job2 = scheduler.Schedule("Paused", MakeTemplate(), "30 * * * *", tags: new[] { "dev" });
            scheduler.Pause(job2.Id);

            var active = scheduler.ListJobs(statusFilter: ScheduleStatus.Active);
            Assert.Single(active);
            Assert.Equal("Active", active[0].Name);

            var prodJobs = scheduler.ListJobs(tagFilter: "prod");
            Assert.Single(prodJobs);
        }

        [Fact]
        public async Task TickAsync_ExecutesDueJobs()
        {
            var scheduler = new PromptScheduler();
            var template = MakeTemplate("Run this");
            var job = scheduler.Schedule("Due Job", template, "* * * * *");

            // Force next fire time to past
            job.NextFireTime = DateTimeOffset.UtcNow.AddMinutes(-1);

            var results = await scheduler.TickAsync(
                async prompt => { await Task.CompletedTask; return "ok"; });

            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Single(job.History);
            Assert.Equal(1, job.SuccessCount);
        }

        [Fact]
        public async Task TickAsync_RecordsFailures()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Fail Job", MakeTemplate(), "* * * * *");
            job.NextFireTime = DateTimeOffset.UtcNow.AddMinutes(-1);

            var results = await scheduler.TickAsync(
                _ => throw new InvalidOperationException("boom"));

            Assert.Single(results);
            Assert.False(results[0].Success);
            Assert.Equal("boom", results[0].ErrorMessage);
            Assert.Equal(1, job.FailureCount);
        }

        [Fact]
        public async Task TickAsync_CompletesJobAtMaxExecutions()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Limited", MakeTemplate(), "* * * * *", maxExecutions: 1);
            job.NextFireTime = DateTimeOffset.UtcNow.AddMinutes(-1);

            await scheduler.TickAsync(async _ => { await Task.CompletedTask; return "ok"; });

            Assert.Equal(ScheduleStatus.Completed, job.Status);
            Assert.Null(job.NextFireTime);
        }

        [Fact]
        public void GetSummary_ReturnsFormattedText()
        {
            var scheduler = new PromptScheduler();
            scheduler.Schedule("Summary Job", MakeTemplate(), "0 9 * * 1", tags: new[] { "weekly" });

            var summary = scheduler.GetSummary();
            Assert.Contains("Summary Job", summary);
            Assert.Contains("weekly", summary);
            Assert.Contains("0 9 * * 1", summary);
        }

        [Fact]
        public void GetSummary_EmptyScheduler()
        {
            var scheduler = new PromptScheduler();
            Assert.Equal("No scheduled jobs.", scheduler.GetSummary());
        }

        [Fact]
        public void ExportToJson_ProducesValidJson()
        {
            var scheduler = new PromptScheduler();
            scheduler.Schedule("Export Job", MakeTemplate(), "0 0 * * *");

            var json = scheduler.ExportToJson();
            Assert.Contains("Export Job", json);

            // Should be parseable
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Single(doc.RootElement.EnumerateArray());
        }

        [Fact]
        public void CronParser_ParsesWildcard()
        {
            var values = CronParser.ParseField("*", 0, 5);
            Assert.Equal(6, values.Count);
        }

        [Fact]
        public void CronParser_ParsesRange()
        {
            var values = CronParser.ParseField("1-3", 0, 5);
            Assert.Equal(new HashSet<int> { 1, 2, 3 }, values);
        }

        [Fact]
        public void CronParser_ParsesStep()
        {
            var values = CronParser.ParseField("*/15", 0, 59);
            Assert.Contains(0, values);
            Assert.Contains(15, values);
            Assert.Contains(30, values);
            Assert.Contains(45, values);
        }

        [Fact]
        public void CronParser_ParsesCommaList()
        {
            var values = CronParser.ParseField("1,3,5", 0, 6);
            Assert.Equal(new HashSet<int> { 1, 3, 5 }, values);
        }

        [Fact]
        public void CronParser_GetNextOccurrence_ReturnsValid()
        {
            var after = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var next = CronParser.GetNextOccurrence("0 12 * * *", after);
            Assert.NotNull(next);
            Assert.Equal(12, next!.Value.Hour);
            Assert.Equal(0, next.Value.Minute);
        }

        [Fact]
        public void CronParser_InvalidExpression_Throws()
        {
            Assert.Throws<FormatException>(() =>
                CronParser.GetNextOccurrence("bad", DateTimeOffset.UtcNow));
        }

        [Fact]
        public void GetJob_ReturnsNullForUnknown()
        {
            var scheduler = new PromptScheduler();
            Assert.Null(scheduler.GetJob("nonexistent"));
        }

        [Fact]
        public void Pause_ReturnsFalseForNonActive()
        {
            var scheduler = new PromptScheduler();
            Assert.False(scheduler.Pause("nonexistent"));
        }

        [Fact]
        public void Resume_ReturnsFalseForNonPaused()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Job", MakeTemplate(), "0 * * * *");
            Assert.False(scheduler.Resume(job.Id)); // already active, not paused
        }

        [Fact]
        public void Tags_AreCaseInsensitive()
        {
            var scheduler = new PromptScheduler();
            var job = scheduler.Schedule("Tagged", MakeTemplate(), "0 * * * *", tags: new[] { "Prod" });

            Assert.Contains("prod", job.Tags);
            Assert.Contains("PROD", job.Tags);
        }
    }
}
