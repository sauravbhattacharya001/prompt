namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptBatchProcessorTests
    {
        // ── Helper ──────────────────────────────────────────────────

        private static PromptTemplate MakeTemplate(string text) =>
            new PromptTemplate(text);

        private static Dictionary<string, string> Vars(params (string k, string v)[] pairs)
        {
            var d = new Dictionary<string, string>();
            foreach (var (k, v) in pairs) d[k] = v;
            return d;
        }

        // ── Constructor / AddItem ────────────────────────────────────

        [Fact]
        public void NewProcessor_CountIsZero()
        {
            var p = new PromptBatchProcessor();
            Assert.Equal(0, p.Count);
        }

        [Fact]
        public void AddItem_IncreasesCount()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"));
            Assert.Equal(1, p.Count);
        }

        [Fact]
        public void AddItem_DuplicateId_Throws()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"));
            Assert.Throws<ArgumentException>(() =>
                p.AddItem("a", MakeTemplate("world")));
        }

        [Fact]
        public void AddItem_NullId_Throws()
        {
            var p = new PromptBatchProcessor();
            Assert.Throws<ArgumentException>(() =>
                p.AddItem(null!, MakeTemplate("hello")));
        }

        [Fact]
        public void AddItem_EmptyId_Throws()
        {
            var p = new PromptBatchProcessor();
            Assert.Throws<ArgumentException>(() =>
                p.AddItem("", MakeTemplate("hello")));
        }

        [Fact]
        public void AddItem_NullTemplate_Throws()
        {
            var p = new PromptBatchProcessor();
            Assert.Throws<ArgumentNullException>(() =>
                p.AddItem("a", null!));
        }

        [Fact]
        public void AddItem_FluentChaining()
        {
            var p = new PromptBatchProcessor();
            var result = p.AddItem("a", MakeTemplate("hello"))
                          .AddItem("b", MakeTemplate("world"));
            Assert.Same(p, result);
            Assert.Equal(2, p.Count);
        }

        // ── AddItems bulk ───────────────────────────────────────────

        [Fact]
        public void AddItems_BulkAdd()
        {
            var p = new PromptBatchProcessor();
            var t = MakeTemplate("test");
            p.AddItems(new[]
            {
                ("a", t, (Dictionary<string, string>?)null),
                ("b", t, (Dictionary<string, string>?)null),
            });
            Assert.Equal(2, p.Count);
        }

        // ── AddFromTemplate ─────────────────────────────────────────

        [Fact]
        public void AddFromTemplate_AutoNumbered()
        {
            var p = new PromptBatchProcessor();
            var t = MakeTemplate("Say {{word}}");
            p.AddFromTemplate(t, new[]
            {
                Vars(("word", "hello")),
                Vars(("word", "world")),
            }, idPrefix: "greet");

            Assert.Equal(2, p.Count);
            Assert.NotNull(p.GetItem("greet-0"));
            Assert.NotNull(p.GetItem("greet-1"));
        }

        [Fact]
        public void AddFromTemplate_WithTags()
        {
            var p = new PromptBatchProcessor();
            var t = MakeTemplate("test");
            p.AddFromTemplate(t, new[] { new Dictionary<string, string>() },
                tags: new[] { "important" });

            var items = p.GetItemsByTag("important");
            Assert.Single(items);
        }

        // ── Clear ───────────────────────────────────────────────────

        [Fact]
        public void Clear_RemovesAllItems()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"));
            p.AddItem("b", MakeTemplate("world"));
            p.Clear();
            Assert.Equal(0, p.Count);
        }

        // ── ProcessAll basic ────────────────────────────────────────

        [Fact]
        public void ProcessAll_RendersTemplates()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("Hello {{name}}"),
                Vars(("name", "World")));

            var result = p.ProcessAll();
            Assert.True(result.AllSucceeded);
            Assert.Equal(1, result.SucceededCount);
            Assert.Equal(0, result.FailedCount);
            Assert.Equal("Hello World", result.Items[0].RenderedPrompt);
        }

        [Fact]
        public void ProcessAll_MultipleItems()
        {
            var p = new PromptBatchProcessor();
            var t = MakeTemplate("Hi {{name}}");
            p.AddItem("a", t, Vars(("name", "Alice")));
            p.AddItem("b", t, Vars(("name", "Bob")));
            p.AddItem("c", t, Vars(("name", "Carol")));

            var result = p.ProcessAll();
            Assert.Equal(3, result.SucceededCount);
            Assert.True(result.AllSucceeded);
            Assert.Equal(100.0, result.SuccessRate);
        }

        [Fact]
        public void ProcessAll_EmptyBatch()
        {
            var p = new PromptBatchProcessor();
            var result = p.ProcessAll();
            Assert.Equal(0, result.Items.Count);
            Assert.True(result.AllSucceeded);
            Assert.Equal(100.0, result.SuccessRate);
        }

        [Fact]
        public void ProcessAll_SetsAttempts()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            var result = p.ProcessAll();
            Assert.Equal(1, result.Items[0].Attempts);
        }

        [Fact]
        public void ProcessAll_SetsDuration()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            var result = p.ProcessAll();
            Assert.True(result.Items[0].DurationMs >= 0);
            Assert.True(result.TotalDurationMs >= 0);
        }

        // ── WithProcessor ───────────────────────────────────────────

        [Fact]
        public void WithProcessor_TransformsOutput()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"));
            p.WithProcessor(rendered => rendered.ToUpperInvariant());

            var result = p.ProcessAll();
            Assert.Equal("HELLO", result.Items[0].RenderedPrompt);
        }

        [Fact]
        public void WithProcessor_NullReturn_SoftFailure()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"));
            p.WithProcessor(_ => null);

            var result = p.ProcessAll();
            Assert.Equal(BatchItemStatus.Failed, result.Items[0].Status);
            Assert.Contains("soft failure", result.Items[0].ErrorMessage,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, result.Items[0].Attempts); // no retry on soft failure
        }

        [Fact]
        public void WithProcessor_Exception_Fails()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"));
            p.WithProcessor(_ => throw new InvalidOperationException("API down"));

            var result = p.ProcessAll();
            Assert.Equal(BatchItemStatus.Failed, result.Items[0].Status);
            Assert.Equal("API down", result.Items[0].ErrorMessage);
        }

        // ── Retry ───────────────────────────────────────────────────

        [Fact]
        public void Retry_RetriesOnException()
        {
            int callCount = 0;
            var p = new PromptBatchProcessor
            {
                RetryPolicy = new RetryPolicy
                {
                    MaxRetries = 2,
                    BaseDelayMs = 1,
                    ExponentialBackoff = false,
                    Jitter = false
                }
            };
            p.AddItem("a", MakeTemplate("hello"));
            p.WithProcessor(_ =>
            {
                callCount++;
                if (callCount < 3) throw new Exception("fail");
                return "ok";
            });

            var result = p.ProcessAll();
            Assert.Equal(BatchItemStatus.Succeeded, result.Items[0].Status);
            Assert.Equal(3, result.Items[0].Attempts);
            Assert.Equal("ok", result.Items[0].RenderedPrompt);
        }

        [Fact]
        public void Retry_ExhaustsRetries()
        {
            var p = new PromptBatchProcessor
            {
                RetryPolicy = new RetryPolicy
                {
                    MaxRetries = 2,
                    BaseDelayMs = 1,
                    ExponentialBackoff = false,
                    Jitter = false
                }
            };
            p.AddItem("a", MakeTemplate("hello"));
            p.WithProcessor(_ => throw new Exception("always fails"));

            var result = p.ProcessAll();
            Assert.Equal(BatchItemStatus.Failed, result.Items[0].Status);
            Assert.Equal(3, result.Items[0].Attempts); // 1 + 2 retries
            Assert.Equal(2, result.TotalRetries);
        }

        [Fact]
        public void Retry_NoRetryByDefault()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"));
            p.WithProcessor(_ => throw new Exception("fail"));

            var result = p.ProcessAll();
            Assert.Equal(1, result.Items[0].Attempts);
        }

        // ── RetryPolicy ─────────────────────────────────────────────

        [Fact]
        public void RetryPolicy_None_HasZeroRetries()
        {
            var policy = RetryPolicy.None;
            Assert.Equal(0, policy.MaxRetries);
        }

        [Fact]
        public void RetryPolicy_Default_HasThreeRetries()
        {
            var policy = RetryPolicy.Default;
            Assert.Equal(3, policy.MaxRetries);
            Assert.True(policy.ExponentialBackoff);
            Assert.True(policy.Jitter);
        }

        [Fact]
        public void RetryPolicy_GetDelay_ZeroAttempt()
        {
            var policy = new RetryPolicy { BaseDelayMs = 100, Jitter = false };
            Assert.Equal(0, policy.GetDelay(0));
        }

        [Fact]
        public void RetryPolicy_GetDelay_ExponentialBackoff()
        {
            var policy = new RetryPolicy
            {
                BaseDelayMs = 100,
                ExponentialBackoff = true,
                MaxDelayMs = 10000,
                Jitter = false
            };
            Assert.Equal(200, policy.GetDelay(1));
            Assert.Equal(400, policy.GetDelay(2));
            Assert.Equal(800, policy.GetDelay(3));
        }

        [Fact]
        public void RetryPolicy_GetDelay_CappedAtMax()
        {
            var policy = new RetryPolicy
            {
                BaseDelayMs = 1000,
                ExponentialBackoff = true,
                MaxDelayMs = 2000,
                Jitter = false
            };
            Assert.Equal(2000, policy.GetDelay(1));
            Assert.Equal(2000, policy.GetDelay(5));
        }

        [Fact]
        public void RetryPolicy_GetDelay_LinearWithoutBackoff()
        {
            var policy = new RetryPolicy
            {
                BaseDelayMs = 100,
                ExponentialBackoff = false,
                MaxDelayMs = 5000,
                Jitter = false
            };
            Assert.Equal(100, policy.GetDelay(1));
            Assert.Equal(100, policy.GetDelay(3));
        }

        [Fact]
        public void RetryPolicy_GetDelay_WithJitter_Varies()
        {
            var policy = new RetryPolicy
            {
                BaseDelayMs = 1000,
                ExponentialBackoff = false,
                Jitter = true
            };
            // With jitter, values should vary but stay within ±25% of base
            int delay = policy.GetDelay(1);
            Assert.InRange(delay, 750, 1250);
        }

        // ── Filter ──────────────────────────────────────────────────

        [Fact]
        public void WithFilter_SkipsFilteredItems()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("keep", MakeTemplate("hello"), tags: new[] { "include" });
            p.AddItem("skip", MakeTemplate("world"), tags: new[] { "exclude" });
            p.WithFilter(item => item.Tags.Contains("include"));

            var result = p.ProcessAll();
            Assert.Equal(BatchItemStatus.Succeeded, result.Items[0].Status);
            Assert.Equal(BatchItemStatus.Skipped, result.Items[1].Status);
            Assert.Equal(1, result.SucceededCount);
            Assert.Equal(1, result.SkippedCount);
        }

        // ── StopOnFirstFailure ──────────────────────────────────────

        [Fact]
        public void StopOnFirstFailure_SkipsRemaining()
        {
            int callCount = 0;
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("ok"));
            p.AddItem("b", MakeTemplate("fail"));
            p.AddItem("c", MakeTemplate("never reached"));
            p.WithProcessor(rendered =>
            {
                callCount++;
                if (rendered == "fail") throw new Exception("boom");
                return rendered;
            });
            p.StopOnFirstFailure();

            var result = p.ProcessAll();
            Assert.Equal(BatchItemStatus.Succeeded, result.Items[0].Status);
            Assert.Equal(BatchItemStatus.Failed, result.Items[1].Status);
            Assert.Equal(BatchItemStatus.Skipped, result.Items[2].Status);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void StopOnFirstFailure_False_ContinuesProcessing()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("fail"));
            p.AddItem("b", MakeTemplate("ok"));
            p.WithProcessor(rendered =>
            {
                if (rendered == "fail") throw new Exception("boom");
                return rendered;
            });
            p.StopOnFirstFailure(false);

            var result = p.ProcessAll();
            Assert.Equal(BatchItemStatus.Failed, result.Items[0].Status);
            Assert.Equal(BatchItemStatus.Succeeded, result.Items[1].Status);
        }

        // ── ProcessSingle ───────────────────────────────────────────

        [Fact]
        public void ProcessSingle_ProcessesOneItem()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("Hello {{x}}"), Vars(("x", "World")));
            p.AddItem("b", MakeTemplate("Bye"));

            var item = p.ProcessSingle("a");
            Assert.NotNull(item);
            Assert.Equal(BatchItemStatus.Succeeded, item!.Status);
            Assert.Equal("Hello World", item.RenderedPrompt);

            // b should still be pending
            var b = p.GetItem("b");
            Assert.Equal(BatchItemStatus.Pending, b!.Status);
        }

        [Fact]
        public void ProcessSingle_NotFound_ReturnsNull()
        {
            var p = new PromptBatchProcessor();
            Assert.Null(p.ProcessSingle("nonexistent"));
        }

        // ── GetItem / GetItemsByTag ─────────────────────────────────

        [Fact]
        public void GetItem_ReturnsCorrectItem()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("x", MakeTemplate("test"));
            var item = p.GetItem("x");
            Assert.NotNull(item);
            Assert.Equal("x", item!.Id);
        }

        [Fact]
        public void GetItem_NotFound_ReturnsNull()
        {
            var p = new PromptBatchProcessor();
            Assert.Null(p.GetItem("missing"));
        }

        [Fact]
        public void GetItemsByTag_FiltersCorrectly()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"), tags: new[] { "urgent" });
            p.AddItem("b", MakeTemplate("test"), tags: new[] { "low" });
            p.AddItem("c", MakeTemplate("test"), tags: new[] { "urgent", "low" });

            var urgent = p.GetItemsByTag("urgent");
            Assert.Equal(2, urgent.Count);
            Assert.Contains(urgent, i => i.Id == "a");
            Assert.Contains(urgent, i => i.Id == "c");
        }

        [Fact]
        public void GetItemsByTag_CaseInsensitive()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"), tags: new[] { "Urgent" });

            var items = p.GetItemsByTag("urgent");
            Assert.Single(items);
        }

        // ── GetProgress ─────────────────────────────────────────────

        [Fact]
        public void GetProgress_BeforeProcessing()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.AddItem("b", MakeTemplate("test"));

            var progress = p.GetProgress();
            Assert.Equal(2, progress.Total);
            Assert.Equal(0, progress.Completed);
            Assert.Equal(2, progress.Pending);
            Assert.Equal(0.0, progress.PercentComplete);
        }

        [Fact]
        public void GetProgress_AfterProcessing()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.ProcessAll();

            var progress = p.GetProgress();
            Assert.Equal(1, progress.Completed);
            Assert.Equal(1, progress.Succeeded);
            Assert.Equal(100.0, progress.PercentComplete);
        }

        // ── Progress callback ───────────────────────────────────────

        [Fact]
        public void OnProgress_InvokedPerItem()
        {
            var reports = new List<BatchProgress>();
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.AddItem("b", MakeTemplate("test"));
            p.OnProgress(prog => reports.Add(new BatchProgress
            {
                Total = prog.Total,
                Completed = prog.Completed,
                Succeeded = prog.Succeeded,
                Failed = prog.Failed,
                LastProcessedId = prog.LastProcessedId
            }));

            p.ProcessAll();
            Assert.Equal(2, reports.Count);
            Assert.Equal("a", reports[0].LastProcessedId);
            Assert.Equal("b", reports[1].LastProcessedId);
            Assert.Equal(1, reports[0].Completed);
            Assert.Equal(2, reports[1].Completed);
        }

        // ── RetryFailed ─────────────────────────────────────────────

        [Fact]
        public void RetryFailed_ResetsFailed()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.WithProcessor(_ => throw new Exception("fail"));
            p.ProcessAll();

            Assert.Equal(BatchItemStatus.Failed, p.GetItem("a")!.Status);

            int reset = p.RetryFailed();
            Assert.Equal(1, reset);
            Assert.Equal(BatchItemStatus.Pending, p.GetItem("a")!.Status);
        }

        [Fact]
        public void RetryFailed_DoesNotResetSucceeded()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.ProcessAll();

            int reset = p.RetryFailed();
            Assert.Equal(0, reset);
            Assert.Equal(BatchItemStatus.Succeeded, p.GetItem("a")!.Status);
        }

        // ── BatchResult ─────────────────────────────────────────────

        [Fact]
        public void BatchResult_SuccessRate_AllSucceed()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.AddItem("b", MakeTemplate("test"));

            var result = p.ProcessAll();
            Assert.Equal(100.0, result.SuccessRate);
        }

        [Fact]
        public void BatchResult_SuccessRate_SomeFail()
        {
            int i = 0;
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.AddItem("b", MakeTemplate("test"));
            p.WithProcessor(_ =>
            {
                i++;
                if (i == 1) throw new Exception("fail");
                return "ok";
            });

            var result = p.ProcessAll();
            Assert.Equal(50.0, result.SuccessRate);
        }

        [Fact]
        public void BatchResult_FailedItems()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.AddItem("b", MakeTemplate("test"));
            p.WithProcessor(_ => throw new Exception("fail"));

            var result = p.ProcessAll();
            Assert.Equal(2, result.FailedItems.Count);
            Assert.Empty(result.SucceededItems);
        }

        [Fact]
        public void BatchResult_SucceededItems()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            p.AddItem("b", MakeTemplate("test"));

            var result = p.ProcessAll();
            Assert.Equal(2, result.SucceededItems.Count);
            Assert.Empty(result.FailedItems);
        }

        [Fact]
        public void BatchResult_TotalRetries()
        {
            int callCount = 0;
            var p = new PromptBatchProcessor
            {
                RetryPolicy = new RetryPolicy
                {
                    MaxRetries = 2,
                    BaseDelayMs = 1,
                    ExponentialBackoff = false,
                    Jitter = false
                }
            };
            p.AddItem("a", MakeTemplate("test"));
            p.WithProcessor(_ =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("first fails");
                return "ok";
            });

            var result = p.ProcessAll();
            Assert.Equal(1, result.TotalRetries); // 1 retry needed
        }

        // ── ToSummary ───────────────────────────────────────────────

        [Fact]
        public void BatchResult_ToSummary_ContainsKeyInfo()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            var result = p.ProcessAll();

            string summary = result.ToSummary();
            Assert.Contains("BATCH PROCESSING RESULT", summary);
            Assert.Contains("Total items:", summary);
            Assert.Contains("Succeeded:", summary);
            Assert.Contains("Success rate:", summary);
        }

        [Fact]
        public void BatchResult_ToSummary_ShowsFailedItems()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("fail-1", MakeTemplate("test"));
            p.WithProcessor(_ => throw new Exception("API error"));

            var result = p.ProcessAll();
            string summary = result.ToSummary();
            Assert.Contains("FAILED ITEMS", summary);
            Assert.Contains("fail-1", summary);
            Assert.Contains("API error", summary);
        }

        // ── ToJson ──────────────────────────────────────────────────

        [Fact]
        public void BatchResult_ToJson_ValidJson()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("hello"), tags: new[] { "test" });
            var result = p.ProcessAll();

            string json = result.ToJson();
            Assert.Contains("\"totalItems\"", json);
            Assert.Contains("\"succeeded\"", json);
            Assert.Contains("\"hello\"", json);

            // Should parse without error
            System.Text.Json.JsonDocument.Parse(json);
        }

        [Fact]
        public void BatchResult_ToJson_Compact()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"));
            var result = p.ProcessAll();

            string compact = result.ToJson(indented: false);
            Assert.DoesNotContain("\n  ", compact);
        }

        // ── GroupByTag ──────────────────────────────────────────────

        [Fact]
        public void BatchResult_GroupByTag()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"), tags: new[] { "english" });
            p.AddItem("b", MakeTemplate("test"), tags: new[] { "english" });
            p.AddItem("c", MakeTemplate("test"), tags: new[] { "french" });

            var result = p.ProcessAll();
            var groups = result.GroupByTag();

            Assert.Equal(2, groups.Count);
            Assert.Equal(2, groups["english"].Total);
            Assert.Equal(2, groups["english"].Succeeded);
            Assert.Equal(1, groups["french"].Total);
        }

        [Fact]
        public void BatchResult_GroupByTag_MultiTagItem()
        {
            var p = new PromptBatchProcessor();
            p.AddItem("a", MakeTemplate("test"),
                tags: new[] { "urgent", "english" });

            var result = p.ProcessAll();
            var groups = result.GroupByTag();

            Assert.True(groups.ContainsKey("urgent"));
            Assert.True(groups.ContainsKey("english"));
            Assert.Equal(1, groups["urgent"].Total);
            Assert.Equal(1, groups["english"].Total);
        }

        // ── BatchItem ───────────────────────────────────────────────

        [Fact]
        public void BatchItem_NullId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new BatchItem(null!, MakeTemplate("test")));
        }

        [Fact]
        public void BatchItem_NullTemplate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BatchItem("a", null!));
        }

        [Fact]
        public void BatchItem_DefaultStatus_Pending()
        {
            var item = new BatchItem("a", MakeTemplate("test"));
            Assert.Equal(BatchItemStatus.Pending, item.Status);
        }

        [Fact]
        public void BatchItem_NullVariables_EmptyDict()
        {
            var item = new BatchItem("a", MakeTemplate("test"));
            Assert.NotNull(item.Variables);
            Assert.Empty(item.Variables);
        }

        [Fact]
        public void BatchItem_NullTags_EmptyList()
        {
            var item = new BatchItem("a", MakeTemplate("test"));
            Assert.NotNull(item.Tags);
            Assert.Empty(item.Tags);
        }

        [Fact]
        public void BatchItem_VariablesCopied()
        {
            var vars = new Dictionary<string, string> { ["k"] = "v" };
            var item = new BatchItem("a", MakeTemplate("test"), vars);
            vars["k"] = "changed";
            Assert.Equal("v", item.Variables["k"]); // original not mutated
        }

        // ── BatchProgress ───────────────────────────────────────────

        [Fact]
        public void BatchProgress_Pending_Correct()
        {
            var prog = new BatchProgress { Total = 10, Completed = 3 };
            Assert.Equal(7, prog.Pending);
        }

        [Fact]
        public void BatchProgress_PercentComplete_Zero()
        {
            var prog = new BatchProgress { Total = 0, Completed = 0 };
            Assert.Equal(100.0, prog.PercentComplete);
        }

        [Fact]
        public void BatchProgress_PercentComplete_Half()
        {
            var prog = new BatchProgress { Total = 10, Completed = 5 };
            Assert.Equal(50.0, prog.PercentComplete);
        }

        // ── Integration scenarios ───────────────────────────────────

        [Fact]
        public void Integration_TranslationBatch()
        {
            var t = MakeTemplate("Translate to {{lang}}: {{text}}");
            var p = new PromptBatchProcessor();
            p.AddFromTemplate(t, new[]
            {
                Vars(("lang", "French"), ("text", "Hello")),
                Vars(("lang", "Spanish"), ("text", "Goodbye")),
                Vars(("lang", "German"), ("text", "Thank you")),
            }, idPrefix: "translate", tags: new[] { "translation" });

            var result = p.ProcessAll();
            Assert.Equal(3, result.SucceededCount);
            Assert.Contains("Translate to French: Hello", result.Items[0].RenderedPrompt);
            Assert.Contains("Translate to Spanish: Goodbye", result.Items[1].RenderedPrompt);
        }

        [Fact]
        public void Integration_ProcessAndRetry()
        {
            int attempt = 0;
            var p = new PromptBatchProcessor
            {
                RetryPolicy = new RetryPolicy
                {
                    MaxRetries = 1,
                    BaseDelayMs = 1,
                    Jitter = false
                }
            };
            p.AddItem("a", MakeTemplate("test"));
            p.AddItem("b", MakeTemplate("test"));
            p.WithProcessor(rendered =>
            {
                attempt++;
                if (attempt == 1) throw new Exception("transient");
                return $"processed-{attempt}";
            });

            var result = p.ProcessAll();
            // Item "a" fails first, succeeds on retry
            Assert.Equal(BatchItemStatus.Succeeded, result.Items[0].Status);
            Assert.Equal(BatchItemStatus.Succeeded, result.Items[1].Status);
        }

        [Fact]
        public void Integration_LargeBatch()
        {
            var p = new PromptBatchProcessor();
            var t = MakeTemplate("Item {{n}}");
            for (int i = 0; i < 100; i++)
            {
                p.AddItem($"item-{i}", t, Vars(("n", i.ToString())));
            }

            var result = p.ProcessAll();
            Assert.Equal(100, result.SucceededCount);
            Assert.True(result.AllSucceeded);
        }
    }
}
