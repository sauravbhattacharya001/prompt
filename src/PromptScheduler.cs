namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Status of a scheduled prompt job.</summary>
    public enum ScheduleStatus
    {
        /// <summary>Job is active and will fire on schedule.</summary>
        Active,
        /// <summary>Job is paused and will not fire until resumed.</summary>
        Paused,
        /// <summary>Job completed its max executions and is retired.</summary>
        Completed,
        /// <summary>Job was cancelled.</summary>
        Cancelled
    }

    /// <summary>Result of a single scheduled execution.</summary>
    public sealed class ScheduleExecutionResult
    {
        /// <summary>When this execution ran.</summary>
        public DateTimeOffset ExecutedAt { get; }

        /// <summary>Whether the execution succeeded.</summary>
        public bool Success { get; }

        /// <summary>Duration of the execution.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Error message if execution failed.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Prompt text that was executed.</summary>
        public string PromptText { get; }

        public ScheduleExecutionResult(DateTimeOffset executedAt, bool success, TimeSpan duration, string promptText, string? errorMessage = null)
        {
            ExecutedAt = executedAt;
            Success = success;
            Duration = duration;
            PromptText = promptText;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>A scheduled prompt job with cron-like timing and execution tracking.</summary>
    public sealed class ScheduledPromptJob
    {
        /// <summary>Unique job identifier.</summary>
        public string Id { get; }

        /// <summary>Human-readable job name.</summary>
        public string Name { get; set; }

        /// <summary>The prompt template to execute.</summary>
        public PromptTemplate Template { get; }

        /// <summary>Cron expression (simplified: minute hour dayOfMonth month dayOfWeek).</summary>
        public string CronExpression { get; }

        /// <summary>Current status.</summary>
        public ScheduleStatus Status { get; internal set; }

        /// <summary>Maximum number of executions (null = unlimited).</summary>
        public int? MaxExecutions { get; set; }

        /// <summary>When the job was created.</summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>Next scheduled fire time.</summary>
        public DateTimeOffset? NextFireTime { get; internal set; }

        /// <summary>Tags for filtering/grouping jobs.</summary>
        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Execution history.</summary>
        public List<ScheduleExecutionResult> History { get; } = new();

        /// <summary>Total successful executions.</summary>
        public int SuccessCount => History.Count(h => h.Success);

        /// <summary>Total failed executions.</summary>
        public int FailureCount => History.Count(h => !h.Success);

        /// <summary>Average execution duration.</summary>
        public TimeSpan AverageDuration => History.Count > 0
            ? TimeSpan.FromMilliseconds(History.Average(h => h.Duration.TotalMilliseconds))
            : TimeSpan.Zero;

        public ScheduledPromptJob(string name, PromptTemplate template, string cronExpression)
        {
            Id = Guid.NewGuid().ToString("N")[..12];
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Template = template ?? throw new ArgumentNullException(nameof(template));
            CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
            Status = ScheduleStatus.Active;
            CreatedAt = DateTimeOffset.UtcNow;
            NextFireTime = CronParser.GetNextOccurrence(cronExpression, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>Minimal cron expression parser (minute hour dayOfMonth month dayOfWeek).</summary>
    public static class CronParser
    {
        /// <summary>Parses a simplified cron expression and returns the next occurrence after the given time.</summary>
        public static DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset after)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return null;

            var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
                throw new FormatException($"Cron expression must have 5 fields (minute hour dayOfMonth month dayOfWeek), got {parts.Length}.");

            var minutes = ParseField(parts[0], 0, 59);
            var hours = ParseField(parts[1], 0, 23);
            var daysOfMonth = ParseField(parts[2], 1, 31);
            var months = ParseField(parts[3], 1, 12);
            var daysOfWeek = ParseField(parts[4], 0, 6);

            // Search forward up to 1 year
            var candidate = after.AddMinutes(1);
            candidate = new DateTimeOffset(candidate.Year, candidate.Month, candidate.Day,
                candidate.Hour, candidate.Minute, 0, candidate.Offset);

            var limit = after.AddYears(1);

            while (candidate < limit)
            {
                if (months.Contains(candidate.Month) &&
                    daysOfMonth.Contains(candidate.Day) &&
                    daysOfWeek.Contains((int)candidate.DayOfWeek) &&
                    hours.Contains(candidate.Hour) &&
                    minutes.Contains(candidate.Minute))
                {
                    return candidate;
                }

                candidate = candidate.AddMinutes(1);

                // Skip ahead if month doesn't match
                if (!months.Contains(candidate.Month))
                {
                    candidate = candidate.AddMonths(1);
                    candidate = new DateTimeOffset(candidate.Year, candidate.Month, 1, 0, 0, 0, candidate.Offset);
                }
            }

            return null;
        }

        /// <summary>Parses a single cron field into a set of valid values.</summary>
        public static HashSet<int> ParseField(string field, int min, int max)
        {
            var result = new HashSet<int>();

            foreach (var segment in field.Split(','))
            {
                var trimmed = segment.Trim();

                if (trimmed == "*")
                {
                    for (int i = min; i <= max; i++) result.Add(i);
                    continue;
                }

                // Handle step values: */5 or 1-10/2
                if (trimmed.Contains('/'))
                {
                    var stepParts = trimmed.Split('/');
                    var step = int.Parse(stepParts[1]);
                    int start = min, end = max;

                    if (stepParts[0] != "*")
                    {
                        if (stepParts[0].Contains('-'))
                        {
                            var rangeParts = stepParts[0].Split('-');
                            start = int.Parse(rangeParts[0]);
                            end = int.Parse(rangeParts[1]);
                        }
                        else
                        {
                            start = int.Parse(stepParts[0]);
                        }
                    }

                    for (int i = start; i <= end; i += step) result.Add(i);
                    continue;
                }

                // Handle ranges: 1-5
                if (trimmed.Contains('-'))
                {
                    var rangeParts = trimmed.Split('-');
                    int start = int.Parse(rangeParts[0]);
                    int end = int.Parse(rangeParts[1]);
                    for (int i = start; i <= end; i++) result.Add(i);
                    continue;
                }

                // Single value
                result.Add(int.Parse(trimmed));
            }

            return result;
        }
    }

    /// <summary>
    /// Manages scheduled prompt jobs — create, pause, resume, cancel, and execute
    /// prompts on cron-like schedules with full execution history tracking.
    /// </summary>
    public sealed class PromptScheduler
    {
        private readonly Dictionary<string, ScheduledPromptJob> _jobs = new();
        private readonly object _lock = new();

        /// <summary>All registered jobs.</summary>
        public IReadOnlyList<ScheduledPromptJob> Jobs
        {
            get { lock (_lock) return _jobs.Values.ToList(); }
        }

        /// <summary>Creates and registers a new scheduled job.</summary>
        /// <param name="name">Human-readable job name.</param>
        /// <param name="template">The prompt template to schedule.</param>
        /// <param name="cronExpression">Cron expression (minute hour dayOfMonth month dayOfWeek).</param>
        /// <param name="tags">Optional tags for grouping.</param>
        /// <param name="maxExecutions">Optional max execution count.</param>
        /// <returns>The created job.</returns>
        public ScheduledPromptJob Schedule(string name, PromptTemplate template, string cronExpression,
            IEnumerable<string>? tags = null, int? maxExecutions = null)
        {
            var job = new ScheduledPromptJob(name, template, cronExpression)
            {
                MaxExecutions = maxExecutions
            };

            if (tags != null)
                foreach (var tag in tags)
                    job.Tags.Add(tag);

            lock (_lock)
                _jobs[job.Id] = job;

            return job;
        }

        /// <summary>Gets a job by ID.</summary>
        public ScheduledPromptJob? GetJob(string jobId)
        {
            lock (_lock)
                return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }

        /// <summary>Pauses a job.</summary>
        public bool Pause(string jobId)
        {
            lock (_lock)
            {
                if (!_jobs.TryGetValue(jobId, out var job) || job.Status != ScheduleStatus.Active)
                    return false;
                job.Status = ScheduleStatus.Paused;
                return true;
            }
        }

        /// <summary>Resumes a paused job.</summary>
        public bool Resume(string jobId)
        {
            lock (_lock)
            {
                if (!_jobs.TryGetValue(jobId, out var job) || job.Status != ScheduleStatus.Paused)
                    return false;
                job.Status = ScheduleStatus.Active;
                job.NextFireTime = CronParser.GetNextOccurrence(job.CronExpression, DateTimeOffset.UtcNow);
                return true;
            }
        }

        /// <summary>Cancels a job permanently.</summary>
        public bool Cancel(string jobId)
        {
            lock (_lock)
            {
                if (!_jobs.TryGetValue(jobId, out var job))
                    return false;
                job.Status = ScheduleStatus.Cancelled;
                job.NextFireTime = null;
                return true;
            }
        }

        /// <summary>Removes a job entirely.</summary>
        public bool Remove(string jobId)
        {
            lock (_lock)
                return _jobs.Remove(jobId);
        }

        /// <summary>Lists jobs filtered by status and/or tags.</summary>
        public IReadOnlyList<ScheduledPromptJob> ListJobs(ScheduleStatus? statusFilter = null, string? tagFilter = null)
        {
            lock (_lock)
            {
                IEnumerable<ScheduledPromptJob> query = _jobs.Values;

                if (statusFilter.HasValue)
                    query = query.Where(j => j.Status == statusFilter.Value);

                if (!string.IsNullOrEmpty(tagFilter))
                    query = query.Where(j => j.Tags.Contains(tagFilter));

                return query.OrderBy(j => j.NextFireTime).ToList();
            }
        }

        /// <summary>
        /// Checks all active jobs and executes any that are due, using the provided
        /// execution function. Returns list of jobs that fired.
        /// </summary>
        /// <param name="executeFunc">Function that takes a prompt string and returns the result string.</param>
        /// <param name="now">Current time (defaults to UtcNow).</param>
        public async Task<List<ScheduleExecutionResult>> TickAsync(
            Func<string, Task<string>> executeFunc,
            DateTimeOffset? now = null)
        {
            var currentTime = now ?? DateTimeOffset.UtcNow;
            var results = new List<ScheduleExecutionResult>();
            List<ScheduledPromptJob> dueJobs;

            lock (_lock)
            {
                dueJobs = _jobs.Values
                    .Where(j => j.Status == ScheduleStatus.Active &&
                                j.NextFireTime.HasValue &&
                                j.NextFireTime.Value <= currentTime)
                    .ToList();
            }

            foreach (var job in dueJobs)
            {
                var promptText = job.Template.Render(new Dictionary<string, string>());
                var sw = System.Diagnostics.Stopwatch.StartNew();
                ScheduleExecutionResult result;

                try
                {
                    await executeFunc(promptText);
                    sw.Stop();
                    result = new ScheduleExecutionResult(currentTime, true, sw.Elapsed, promptText);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    result = new ScheduleExecutionResult(currentTime, false, sw.Elapsed, promptText, ex.Message);
                }

                lock (_lock)
                {
                    job.History.Add(result);
                    results.Add(result);

                    if (job.MaxExecutions.HasValue && job.History.Count >= job.MaxExecutions.Value)
                    {
                        job.Status = ScheduleStatus.Completed;
                        job.NextFireTime = null;
                    }
                    else
                    {
                        job.NextFireTime = CronParser.GetNextOccurrence(job.CronExpression, currentTime);
                    }
                }
            }

            return results;
        }

        /// <summary>Gets a summary of all jobs as a formatted string.</summary>
        public string GetSummary()
        {
            lock (_lock)
            {
                if (_jobs.Count == 0)
                    return "No scheduled jobs.";

                var lines = new List<string>
                {
                    $"Scheduled Jobs ({_jobs.Count} total)",
                    new string('─', 60)
                };

                foreach (var job in _jobs.Values.OrderBy(j => j.NextFireTime ?? DateTimeOffset.MaxValue))
                {
                    lines.Add($"  [{job.Status}] {job.Name} (id: {job.Id})");
                    lines.Add($"    Cron: {job.CronExpression}");
                    lines.Add($"    Next: {job.NextFireTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—"}");
                    lines.Add($"    Runs: {job.SuccessCount} ok / {job.FailureCount} fail / {job.History.Count} total");

                    if (job.Tags.Count > 0)
                        lines.Add($"    Tags: {string.Join(", ", job.Tags)}");

                    if (job.AverageDuration > TimeSpan.Zero)
                        lines.Add($"    Avg duration: {job.AverageDuration.TotalMilliseconds:F0}ms");

                    lines.Add("");
                }

                return string.Join(Environment.NewLine, lines);
            }
        }

        /// <summary>Exports all jobs and their history to JSON.</summary>
        public string ExportToJson()
        {
            lock (_lock)
            {
                var data = _jobs.Values.Select(j => new
                {
                    j.Id,
                    j.Name,
                    j.CronExpression,
                    Status = j.Status.ToString(),
                    j.MaxExecutions,
                    CreatedAt = j.CreatedAt.ToString("o"),
                    NextFireTime = j.NextFireTime?.ToString("o"),
                    Tags = j.Tags.ToList(),
                    SuccessCount = j.SuccessCount,
                    FailureCount = j.FailureCount,
                    History = j.History.Select(h => new
                    {
                        ExecutedAt = h.ExecutedAt.ToString("o"),
                        h.Success,
                        DurationMs = h.Duration.TotalMilliseconds,
                        h.ErrorMessage
                    })
                });

                return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            }
        }
    }
}
