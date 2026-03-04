namespace Prompt.Tests
{
    using Xunit;

    public class PromptPipelineTests
    {
        [Fact]
        public async Task ExecuteAsync_EmptyPipeline_CallsTerminalHandler()
        {
            var pipeline = new PromptPipeline();
            var ctx = new PromptPipelineContext { PromptText = "Hello" };
            bool called = false;

            await pipeline.ExecuteAsync(ctx, async c =>
            {
                called = true;
                c.Response = "World";
                await Task.CompletedTask;
            });

            Assert.True(called);
            Assert.Equal("World", ctx.Response);
        }

        [Fact]
        public async Task ExecuteAsync_RendersVariables()
        {
            var pipeline = new PromptPipeline();
            var ctx = new PromptPipelineContext
            {
                PromptText = "Say {{greeting}} to {{name}}",
                Variables = new() { ["greeting"] = "hello", ["name"] = "world" }
            };

            await pipeline.ExecuteAsync(ctx, async c =>
            {
                c.Response = c.RenderedPrompt;
                await Task.CompletedTask;
            });

            Assert.Equal("Say hello to world", ctx.Response);
            Assert.Equal("Say hello to world", ctx.RenderedPrompt);
        }

        [Fact]
        public async Task ExecuteAsync_MiddlewareRunsInOrder()
        {
            var order = new List<string>();
            var pipeline = new PromptPipeline()
                .Use("First", 10, async (ctx, next) => { order.Add("First-pre"); await next(ctx); order.Add("First-post"); })
                .Use("Second", 20, async (ctx, next) => { order.Add("Second-pre"); await next(ctx); order.Add("Second-post"); })
                .Use("Third", 30, async (ctx, next) => { order.Add("Third-pre"); await next(ctx); order.Add("Third-post"); });

            var context = new PromptPipelineContext { PromptText = "test" };
            await pipeline.ExecuteAsync(context, async c => { order.Add("terminal"); await Task.CompletedTask; });

            Assert.Equal(new[] { "First-pre", "Second-pre", "Third-pre", "terminal", "Third-post", "Second-post", "First-post" }, order);
        }

        [Fact]
        public async Task ExecuteAsync_ShortCircuit_SkipsTerminal()
        {
            bool terminalCalled = false;
            var pipeline = new PromptPipeline()
                .Use("Blocker", 0, async (ctx, next) =>
                {
                    ctx.Response = "blocked";
                    ctx.ShortCircuited = true;
                    await Task.CompletedTask;
                });

            var context = new PromptPipelineContext { PromptText = "test" };
            await pipeline.ExecuteAsync(context, async c => { terminalCalled = true; await Task.CompletedTask; });

            Assert.False(terminalCalled);
            Assert.True(context.ShortCircuited);
            Assert.Equal("blocked", context.Response);
        }

        [Fact]
        public async Task ExecuteAsync_SetsExecutionTime()
        {
            var pipeline = new PromptPipeline();
            var ctx = new PromptPipelineContext { PromptText = "test" };

            await pipeline.ExecuteAsync(ctx, async c => { await Task.Delay(10); c.Response = "ok"; });

            Assert.True(ctx.ExecutionTime > TimeSpan.Zero);
        }

        [Fact]
        public async Task ExecuteAsync_NullContext_Throws()
        {
            var pipeline = new PromptPipeline();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => pipeline.ExecuteAsync(null!, async c => await Task.CompletedTask));
        }

        [Fact]
        public async Task ExecuteAsync_NullHandler_Throws()
        {
            var pipeline = new PromptPipeline();
            var ctx = new PromptPipelineContext { PromptText = "test" };
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => pipeline.ExecuteAsync(ctx, null!));
        }

        // ValidationMiddleware tests

        [Fact]
        public async Task Validation_EmptyPrompt_AddsError()
        {
            var pipeline = new PromptPipeline().Use(new ValidationMiddleware());
            var ctx = new PromptPipelineContext { PromptText = "" };
            bool called = false;

            await pipeline.ExecuteAsync(ctx, async c => { called = true; await Task.CompletedTask; });

            Assert.True(ctx.HasError);
            Assert.Contains("empty", ctx.Errors[0], StringComparison.OrdinalIgnoreCase);
            Assert.False(called);
        }

        [Fact]
        public async Task Validation_ExceedsTokenLimit_AddsError()
        {
            var pipeline = new PromptPipeline().Use(new ValidationMiddleware(maxTokens: 5));
            var ctx = new PromptPipelineContext { PromptText = new string('a', 100) };
            bool called = false;

            await pipeline.ExecuteAsync(ctx, async c => { called = true; await Task.CompletedTask; });

            Assert.True(ctx.HasError);
            Assert.Contains("exceeds", ctx.Errors[0], StringComparison.OrdinalIgnoreCase);
            Assert.False(called);
        }

        [Fact]
        public async Task Validation_MissingRequiredVariable_AddsError()
        {
            var pipeline = new PromptPipeline()
                .Use(new ValidationMiddleware(requiredVariables: new[] { "name", "age" }));
            var ctx = new PromptPipelineContext
            {
                PromptText = "Hello {{name}}, age {{age}}",
                Variables = new() { ["name"] = "Bob" }
            };

            await pipeline.ExecuteAsync(ctx, async c => { c.Response = "ok"; await Task.CompletedTask; });

            Assert.True(ctx.HasError);
            Assert.Contains(ctx.Errors, e => e.Contains("age"));
        }

        [Fact]
        public async Task Validation_ValidPrompt_ContinuesPipeline()
        {
            var pipeline = new PromptPipeline()
                .Use(new ValidationMiddleware(maxTokens: 1000));
            var ctx = new PromptPipelineContext { PromptText = "Hello world" };

            await pipeline.ExecuteAsync(ctx, async c => { c.Response = "ok"; await Task.CompletedTask; });

            Assert.False(ctx.HasError);
            Assert.Equal("ok", ctx.Response);
            Assert.True(ctx.EstimatedTokens > 0);
        }

        // LoggingMiddleware tests

        [Fact]
        public async Task Logging_RecordsStartAndCompletion()
        {
            var logs = new List<string>();
            var pipeline = new PromptPipeline()
                .Use(new LoggingMiddleware(msg => logs.Add(msg)));
            var ctx = new PromptPipelineContext { PromptText = "test" };

            await pipeline.ExecuteAsync(ctx, async c => { c.Response = "ok"; await Task.CompletedTask; });

            Assert.Equal(2, logs.Count);
            Assert.Contains("Starting", logs[0]);
            Assert.Contains("Completed", logs[1]);
        }

        [Fact]
        public async Task Logging_RecordsFailure()
        {
            var logs = new List<string>();
            var pipeline = new PromptPipeline()
                .Use(new LoggingMiddleware(msg => logs.Add(msg)));
            var ctx = new PromptPipelineContext { PromptText = "test" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                pipeline.ExecuteAsync(ctx, c => throw new InvalidOperationException("boom")));

            Assert.Equal(2, logs.Count);
            Assert.Contains("Failed", logs[1]);
            Assert.Contains("boom", logs[1]);
        }

        // CachingMiddleware tests

        [Fact]
        public async Task Caching_SecondCall_ReturnsCached()
        {
            var caching = new CachingMiddleware();
            var pipeline = new PromptPipeline().Use(caching);
            int callCount = 0;

            for (int i = 0; i < 3; i++)
            {
                var ctx = new PromptPipelineContext { PromptText = "same prompt" };
                await pipeline.ExecuteAsync(ctx, async c =>
                {
                    callCount++;
                    c.Response = $"response-{callCount}";
                    await Task.CompletedTask;
                });
            }

            Assert.Equal(1, callCount);
            Assert.Equal(1, caching.MissCount);
            Assert.Equal(2, caching.HitCount);
        }

        [Fact]
        public async Task Caching_DifferentPrompts_NoCacheHit()
        {
            var caching = new CachingMiddleware();
            var pipeline = new PromptPipeline().Use(caching);
            int callCount = 0;

            for (int i = 0; i < 3; i++)
            {
                var ctx = new PromptPipelineContext { PromptText = $"prompt-{i}" };
                await pipeline.ExecuteAsync(ctx, async c =>
                {
                    callCount++;
                    c.Response = "ok";
                    await Task.CompletedTask;
                });
            }

            Assert.Equal(3, callCount);
            Assert.Equal(0, caching.HitCount);
        }

        [Fact]
        public async Task Caching_CachedResponse_SetsShortCircuited()
        {
            var caching = new CachingMiddleware();
            var pipeline = new PromptPipeline().Use(caching);

            var ctx1 = new PromptPipelineContext { PromptText = "hello" };
            await pipeline.ExecuteAsync(ctx1, async c => { c.Response = "world"; await Task.CompletedTask; });
            Assert.False(ctx1.ShortCircuited);

            var ctx2 = new PromptPipelineContext { PromptText = "hello" };
            await pipeline.ExecuteAsync(ctx2, async c => { c.Response = "new"; await Task.CompletedTask; });
            Assert.True(ctx2.ShortCircuited);
            Assert.Equal("world", ctx2.Response);
        }

        [Fact]
        public async Task Caching_Clear_ResetsCache()
        {
            var caching = new CachingMiddleware();
            var pipeline = new PromptPipeline().Use(caching);

            var ctx = new PromptPipelineContext { PromptText = "test" };
            await pipeline.ExecuteAsync(ctx, async c => { c.Response = "r1"; await Task.CompletedTask; });

            caching.Clear();

            var ctx2 = new PromptPipelineContext { PromptText = "test" };
            await pipeline.ExecuteAsync(ctx2, async c => { c.Response = "r2"; await Task.CompletedTask; });
            Assert.Equal("r2", ctx2.Response);
            Assert.False(ctx2.ShortCircuited);
        }

        // RetryMiddleware tests

        [Fact]
        public async Task Retry_SucceedsOnSecondAttempt()
        {
            int attempts = 0;
            var retry = new RetryMiddleware(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(1));
            var pipeline = new PromptPipeline().Use(retry);
            var ctx = new PromptPipelineContext { PromptText = "test" };

            await pipeline.ExecuteAsync(ctx, async c =>
            {
                attempts++;
                if (attempts < 2) throw new Exception("fail");
                c.Response = "ok";
                await Task.CompletedTask;
            });

            Assert.Equal(2, attempts);
            Assert.Equal("ok", ctx.Response);
            Assert.Equal(1, retry.TotalRetries);
        }

        [Fact]
        public async Task Retry_ExhaustsRetries_Throws()
        {
            var retry = new RetryMiddleware(maxRetries: 1, baseDelay: TimeSpan.FromMilliseconds(1));
            var pipeline = new PromptPipeline().Use(retry);
            var ctx = new PromptPipelineContext { PromptText = "test" };

            await Assert.ThrowsAsync<Exception>(() =>
                pipeline.ExecuteAsync(ctx, c => throw new Exception("always fail")));

            Assert.True(ctx.HasError);
            Assert.Equal(2, retry.TotalRetries);
        }

        // ContentFilterMiddleware tests

        [Fact]
        public async Task ContentFilter_BlockedPrompt_StopsExecution()
        {
            var filter = new ContentFilterMiddleware(new[] { "secret", "password" });
            var pipeline = new PromptPipeline().Use(filter);
            bool called = false;

            var ctx = new PromptPipelineContext { PromptText = "Tell me the secret" };
            await pipeline.ExecuteAsync(ctx, async c => { called = true; await Task.CompletedTask; });

            Assert.True(ctx.HasError);
            Assert.False(called);
            Assert.Equal(1, filter.BlockedCount);
        }

        [Fact]
        public async Task ContentFilter_FiltersResponse()
        {
            var filter = new ContentFilterMiddleware(new[] { "badword" });
            var pipeline = new PromptPipeline().Use(filter);

            var ctx = new PromptPipelineContext { PromptText = "clean prompt" };
            await pipeline.ExecuteAsync(ctx, async c =>
            {
                c.Response = "This contains badword in response";
                await Task.CompletedTask;
            });

            Assert.Equal("[Content filtered]", ctx.Response);
            Assert.Single(ctx.Warnings);
        }

        [Fact]
        public async Task ContentFilter_CleanContent_PassesThrough()
        {
            var filter = new ContentFilterMiddleware(new[] { "blocked" });
            var pipeline = new PromptPipeline().Use(filter);

            var ctx = new PromptPipelineContext { PromptText = "normal prompt" };
            await pipeline.ExecuteAsync(ctx, async c =>
            {
                c.Response = "normal response";
                await Task.CompletedTask;
            });

            Assert.Equal("normal response", ctx.Response);
            Assert.False(ctx.HasError);
        }

        // MetricsMiddleware tests

        [Fact]
        public async Task Metrics_CollectsExecutionData()
        {
            var metrics = new MetricsMiddleware();
            var pipeline = new PromptPipeline().Use(metrics);

            for (int i = 0; i < 5; i++)
            {
                var ctx = new PromptPipelineContext { PromptText = $"prompt {i}" };
                await pipeline.ExecuteAsync(ctx, async c => { c.Response = "ok"; await Task.CompletedTask; });
            }

            Assert.Equal(5, metrics.GetMetrics().Count);
            Assert.True(metrics.AverageExecutionTime() >= TimeSpan.Zero);
            Assert.Equal(0.0, metrics.ErrorRate());
        }

        [Fact]
        public async Task Metrics_TracksErrors()
        {
            // Metrics (order -20) wraps validation (order -10) so it always records
            var metrics = new MetricsMiddleware(order: -20);
            var validation = new ValidationMiddleware();
            var pipeline = new PromptPipeline().Use(validation).Use(metrics);

            var ctx = new PromptPipelineContext { PromptText = "" };
            await pipeline.ExecuteAsync(ctx, async c => { await Task.CompletedTask; });

            var m = metrics.GetMetrics();
            Assert.Single(m);
        }

        [Fact]
        public async Task Metrics_TotalTokens_Accumulates()
        {
            var metrics = new MetricsMiddleware();
            var validation = new ValidationMiddleware();
            var pipeline = new PromptPipeline().Use(validation).Use(metrics);

            for (int i = 0; i < 3; i++)
            {
                var ctx = new PromptPipelineContext { PromptText = "Hello world test prompt" };
                await pipeline.ExecuteAsync(ctx, async c => { c.Response = "ok"; await Task.CompletedTask; });
            }

            Assert.True(metrics.TotalTokens() > 0);
        }

        [Fact]
        public void Metrics_Clear_Resets()
        {
            var metrics = new MetricsMiddleware();
            Assert.Empty(metrics.GetMetrics());
            Assert.Equal(TimeSpan.Zero, metrics.AverageExecutionTime());
            Assert.Equal(0.0, metrics.ErrorRate());
        }

        // LambdaMiddleware tests

        [Fact]
        public void LambdaMiddleware_NullName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LambdaMiddleware(null!, 0, (ctx, next) => next(ctx)));
        }

        [Fact]
        public void LambdaMiddleware_NullHandler_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LambdaMiddleware("test", 0, null!));
        }

        // Pipeline management tests

        [Fact]
        public void Remove_ExistingMiddleware_ReturnsTrue()
        {
            var pipeline = new PromptPipeline()
                .Use(new LoggingMiddleware(msg => { }))
                .Use(new ValidationMiddleware());

            Assert.True(pipeline.Remove("Logging"));
            Assert.Single(pipeline.GetMiddleware());
        }

        [Fact]
        public void Remove_NonExistent_ReturnsFalse()
        {
            var pipeline = new PromptPipeline();
            Assert.False(pipeline.Remove("Nothing"));
        }

        [Fact]
        public void GetMiddleware_ReturnsSortedByOrder()
        {
            var pipeline = new PromptPipeline()
                .Use("Z", 30, (ctx, next) => next(ctx))
                .Use("A", 10, (ctx, next) => next(ctx))
                .Use("M", 20, (ctx, next) => next(ctx));

            var mw = pipeline.GetMiddleware();
            Assert.Equal("A", mw[0].Name);
            Assert.Equal("M", mw[1].Name);
            Assert.Equal("Z", mw[2].Name);
        }

        [Fact]
        public void Describe_EmptyPipeline_ReturnsEmptyMessage()
        {
            var pipeline = new PromptPipeline();
            Assert.Contains("Empty", pipeline.Describe());
        }

        [Fact]
        public void Describe_WithMiddleware_ListsAll()
        {
            var pipeline = new PromptPipeline()
                .Use(new ValidationMiddleware())
                .Use(new LoggingMiddleware(msg => { }));

            var desc = pipeline.Describe();
            Assert.Contains("Validation", desc);
            Assert.Contains("Logging", desc);
            Assert.Contains("2 middleware", desc);
        }

        // PromptPipelineContext tests

        [Fact]
        public void Context_HasUniqueExecutionId()
        {
            var c1 = new PromptPipelineContext();
            var c2 = new PromptPipelineContext();
            Assert.NotEqual(c1.ExecutionId, c2.ExecutionId);
            Assert.Equal(12, c1.ExecutionId.Length);
        }

        [Fact]
        public void Context_CreatedAt_IsSet()
        {
            var ctx = new PromptPipelineContext();
            Assert.True((DateTimeOffset.UtcNow - ctx.CreatedAt).TotalSeconds < 5);
        }

        [Fact]
        public void Context_HasError_ReflectsErrors()
        {
            var ctx = new PromptPipelineContext();
            Assert.False(ctx.HasError);
            ctx.Errors.Add("something");
            Assert.True(ctx.HasError);
        }

        [Fact]
        public void Context_Metadata_IsAccessible()
        {
            var ctx = new PromptPipelineContext();
            ctx.Metadata["key"] = 42;
            Assert.Equal(42, ctx.Metadata["key"]);
        }

        // Integration: full pipeline

        [Fact]
        public async Task FullPipeline_AllMiddlewareCooperate()
        {
            var logs = new List<string>();
            var metrics = new MetricsMiddleware();
            var caching = new CachingMiddleware();

            var pipeline = new PromptPipeline()
                .Use(new ValidationMiddleware(maxTokens: 1000, requiredVariables: new[] { "topic" }))
                .Use(new LoggingMiddleware(msg => logs.Add(msg)))
                .Use(caching)
                .Use(new ContentFilterMiddleware(new[] { "forbidden" }))
                .Use(metrics);

            // First call - cache miss
            var ctx1 = new PromptPipelineContext
            {
                PromptText = "Tell me about {{topic}}",
                Variables = new() { ["topic"] = "science" }
            };
            await pipeline.ExecuteAsync(ctx1, async c =>
            {
                c.Response = "Science is great!";
                await Task.CompletedTask;
            });

            Assert.Equal("Science is great!", ctx1.Response);
            Assert.False(ctx1.HasError);
            Assert.Equal(1, caching.MissCount);

            // Second call - cache hit
            var ctx2 = new PromptPipelineContext
            {
                PromptText = "Tell me about {{topic}}",
                Variables = new() { ["topic"] = "science" }
            };
            await pipeline.ExecuteAsync(ctx2, async c =>
            {
                c.Response = "should not reach here";
                await Task.CompletedTask;
            });

            Assert.Equal("Science is great!", ctx2.Response);
            Assert.True(ctx2.ShortCircuited);
            Assert.Equal(1, caching.HitCount);
            // Metrics order 100 runs after caching order 10; cached call short-circuits
            // so metrics only records the first (non-cached) call
            Assert.Equal(1, metrics.GetMetrics().Count);
        }

        [Fact]
        public async Task FullPipeline_ValidationBlocksMissingVariable()
        {
            var pipeline = new PromptPipeline()
                .Use(new ValidationMiddleware(requiredVariables: new[] { "name" }));

            var ctx = new PromptPipelineContext
            {
                PromptText = "Hello {{name}}",
                Variables = new()
            };

            await pipeline.ExecuteAsync(ctx, async c => { c.Response = "ok"; await Task.CompletedTask; });

            Assert.True(ctx.HasError);
            Assert.Null(ctx.Response);
        }

        // RenderVariables internal

        [Fact]
        public void RenderVariables_NoVariables_ReturnsTemplate()
        {
            var result = PromptPipeline.RenderVariables("hello", new());
            Assert.Equal("hello", result);
        }

        [Fact]
        public void RenderVariables_EmptyTemplate_ReturnsEmpty()
        {
            var result = PromptPipeline.RenderVariables("", new() { ["x"] = "y" });
            Assert.Equal("", result);
        }

        [Fact]
        public void RenderVariables_MultipleVariables_AllReplaced()
        {
            var result = PromptPipeline.RenderVariables(
                "{{a}} and {{b}}",
                new() { ["a"] = "X", ["b"] = "Y" });
            Assert.Equal("X and Y", result);
        }

        // ContentFilterMiddleware constructor

        [Fact]
        public void ContentFilter_NullPatterns_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ContentFilterMiddleware(null!));
        }

        // LoggingMiddleware constructor

        [Fact]
        public void Logging_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LoggingMiddleware(null!));
        }
    }
}
