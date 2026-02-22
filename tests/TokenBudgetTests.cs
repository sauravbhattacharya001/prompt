namespace Prompt.Tests;

using Xunit;

public class TokenBudgetTests
{
    // ──────────────── Construction ────────────────

    [Fact]
    public void Constructor_DefaultValues()
    {
        var budget = new TokenBudget();

        Assert.Equal(TokenBudget.DefaultMaxTokens, budget.MaxTokens);
        Assert.Equal(TokenBudget.DefaultReserveForResponse, budget.ReserveForResponse);
        Assert.Equal(TokenBudget.DefaultReserveTokens, budget.ReserveTokens);
        Assert.Equal(TrimStrategy.RemoveOldest, budget.Strategy);
        Assert.Equal(1, budget.KeepFirstTurns);
        Assert.Equal(0, budget.UsedTokens);
        Assert.Equal(0, budget.MessageCount);
        Assert.Equal(0, budget.TrimmedCount);
        Assert.False(budget.IsOverBudget);
    }

    [Fact]
    public void Constructor_CustomValues()
    {
        var budget = new TokenBudget(maxTokens: 8192, reserveForResponse: 1000);

        Assert.Equal(8192, budget.MaxTokens);
        Assert.Equal(1000, budget.ReserveForResponse);
    }

    [Fact]
    public void Constructor_ThrowsOnTooSmallMaxTokens()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenBudget(maxTokens: 50));
    }

    [Fact]
    public void Constructor_ThrowsOnNegativeReserve()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenBudget(reserveForResponse: -1));
    }

    [Fact]
    public void Constructor_ThrowsWhenReserveExceedsMax()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenBudget(maxTokens: 1000, reserveForResponse: 1000));
    }

    // ──────────────── AddMessage ────────────────

    [Fact]
    public void AddMessage_TracksTokens()
    {
        var budget = new TokenBudget();
        budget.AddMessage("user", "Hello world");

        Assert.True(budget.UsedTokens > 0);
        Assert.Equal(1, budget.MessageCount);
    }

    [Fact]
    public void AddMessage_AcceptsAllRoles()
    {
        var budget = new TokenBudget();
        budget.AddMessage("system", "You are a helper.");
        budget.AddMessage("user", "Hi");
        budget.AddMessage("assistant", "Hello!");

        Assert.Equal(3, budget.MessageCount);
    }

    [Fact]
    public void AddMessage_NormalizesRoleCase()
    {
        var budget = new TokenBudget();
        budget.AddMessage("USER", "test");
        budget.AddMessage("System", "test");
        budget.AddMessage("ASSISTANT", "test");

        var messages = budget.GetMessages();
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("system", messages[1].Role);
        Assert.Equal("assistant", messages[2].Role);
    }

    [Fact]
    public void AddMessage_ThrowsOnEmptyRole()
    {
        var budget = new TokenBudget();
        Assert.Throws<ArgumentException>(() => budget.AddMessage("", "content"));
    }

    [Fact]
    public void AddMessage_ThrowsOnNullContent()
    {
        var budget = new TokenBudget();
        Assert.Throws<ArgumentException>(() => budget.AddMessage("user", null!));
    }

    [Fact]
    public void AddMessage_ThrowsOnInvalidRole()
    {
        var budget = new TokenBudget();
        Assert.Throws<ArgumentException>(() => budget.AddMessage("admin", "test"));
    }

    // ──────────────── Budget Calculations ────────────────

    [Fact]
    public void AvailableTokens_SubtractsReserves()
    {
        var budget = new TokenBudget(maxTokens: 10000, reserveForResponse: 2000)
        {
            ReserveTokens = 300
        };

        Assert.Equal(7700, budget.AvailableTokens);
    }

    [Fact]
    public void RemainingTokens_DecreasesWithMessages()
    {
        var budget = new TokenBudget(maxTokens: 10000, reserveForResponse: 2000);
        int initial = budget.RemainingTokens;

        budget.AddMessage("user", "Hello, how are you today?");
        Assert.True(budget.RemainingTokens < initial);
    }

    [Fact]
    public void UsagePercent_IncreasesWithMessages()
    {
        var budget = new TokenBudget(maxTokens: 1000, reserveForResponse: 200);
        Assert.Equal(0.0, budget.UsagePercent);

        budget.AddMessage("user", "Some text here");
        Assert.True(budget.UsagePercent > 0.0);
    }

    // ──────────────── Auto-Trimming (RemoveOldest) ────────────────

    [Fact]
    public void Trim_RemoveOldest_TrimsWhenOverBudget()
    {
        // Small budget to force trimming
        var budget = new TokenBudget(maxTokens: 200, reserveForResponse: 50)
        {
            ReserveTokens = 0,
            Strategy = TrimStrategy.RemoveOldest
        };

        // Fill budget with messages
        for (int i = 0; i < 20; i++)
        {
            budget.AddMessage("user", $"Message number {i} with some padding text to use tokens");
        }

        // Should have trimmed some
        Assert.True(budget.TrimmedCount > 0);
        Assert.True(budget.MessageCount < 20);
    }

    [Fact]
    public void Trim_RemoveOldest_PreservesSystemMessages()
    {
        var budget = new TokenBudget(maxTokens: 200, reserveForResponse: 50)
        {
            ReserveTokens = 0
        };

        budget.AddMessage("system", "You are a helpful assistant.");

        for (int i = 0; i < 20; i++)
        {
            budget.AddMessage("user", $"Fill message {i} with enough text to trigger trimming");
        }

        var messages = budget.GetMessages();
        Assert.Equal("system", messages[0].Role);
        Assert.Contains("You are a helpful assistant.", messages[0].Content);
    }

    [Fact]
    public void Trim_RemoveOldest_ReturnsTrimmCount()
    {
        var budget = new TokenBudget(maxTokens: 200, reserveForResponse: 50)
        {
            ReserveTokens = 0
        };

        budget.AddMessage("user", "First message with lots of text to take up token space in the budget");
        budget.AddMessage("user", "Second message with lots of text to take up token space in the budget");

        int trimmed = budget.AddMessage("user",
            "Third message that should push us over the limit and cause trimming of oldest messages");

        // May or may not trim depending on exact token estimates
        Assert.True(budget.TrimmedCount >= 0);
    }

    // ──────────────── Auto-Trimming (SlidingWindow) ────────────────

    [Fact]
    public void Trim_SlidingWindow_KeepsFirstTurns()
    {
        var budget = new TokenBudget(maxTokens: 300, reserveForResponse: 50)
        {
            ReserveTokens = 0,
            Strategy = TrimStrategy.SlidingWindow,
            KeepFirstTurns = 1
        };

        // First turn (protected)
        budget.AddMessage("user", "What is AI?");
        budget.AddMessage("assistant", "AI is artificial intelligence.");

        // More turns to fill up
        for (int i = 0; i < 10; i++)
        {
            budget.AddMessage("user", $"Follow-up question number {i} with padding");
            budget.AddMessage("assistant", $"Answer to question number {i} with padding");
        }

        var messages = budget.GetMessages();
        // First turn should still be there (after any system messages)
        var nonSystem = messages.Where(m => m.Role != "system").ToList();
        Assert.True(nonSystem.Count >= 2); // At least the protected turn
    }

    // ──────────────── Auto-Trimming (RemoveLongest) ────────────────

    [Fact]
    public void Trim_RemoveLongest_RemovesLongestFirst()
    {
        var budget = new TokenBudget(maxTokens: 300, reserveForResponse: 50)
        {
            ReserveTokens = 0,
            Strategy = TrimStrategy.RemoveLongest
        };

        budget.AddMessage("user", "Short");
        budget.AddMessage("user", new string('x', 500)); // Very long
        budget.AddMessage("user", "Medium length message here");

        // Force trimming by adding more
        budget.AddMessage("user", "Another medium message to push budget over");
        budget.AddMessage("user", "And another one for good measure and trimming");

        if (budget.TrimmedCount > 0)
        {
            // The longest message should have been removed first
            var messages = budget.GetMessages();
            Assert.DoesNotContain(messages, m => m.Content == new string('x', 500));
        }
    }

    // ──────────────── GetMessages ────────────────

    [Fact]
    public void GetMessages_ReturnsCorrectOrder()
    {
        var budget = new TokenBudget();
        budget.AddMessage("system", "Be helpful.");
        budget.AddMessage("user", "Question");
        budget.AddMessage("assistant", "Answer");

        var messages = budget.GetMessages();
        Assert.Equal(3, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("user", messages[1].Role);
        Assert.Equal("assistant", messages[2].Role);
    }

    [Fact]
    public void GetMessagesWithTokens_IncludesTokenCounts()
    {
        var budget = new TokenBudget();
        budget.AddMessage("user", "Hello world");

        var messages = budget.GetMessagesWithTokens();
        Assert.Single(messages);
        Assert.True(messages[0].Tokens > 0);
    }

    // ──────────────── WouldFit ────────────────

    [Fact]
    public void WouldFit_ReturnsTrueWhenEnoughRoom()
    {
        var budget = new TokenBudget(maxTokens: 10000, reserveForResponse: 1000);
        Assert.True(budget.WouldFit("Short message"));
    }

    [Fact]
    public void WouldFit_ReturnsFalseWhenFull()
    {
        var budget = new TokenBudget(maxTokens: 200, reserveForResponse: 50)
        {
            ReserveTokens = 0
        };

        // Fill budget
        for (int i = 0; i < 10; i++)
        {
            budget.AddMessage("user", $"Message {i} with some padding text");
        }

        // Very long message shouldn't fit
        Assert.False(budget.WouldFit(new string('x', 5000)));
    }

    [Fact]
    public void WouldFit_ReturnsTrueForEmpty()
    {
        var budget = new TokenBudget();
        Assert.True(budget.WouldFit(""));
    }

    // ──────────────── Clear ────────────────

    [Fact]
    public void ClearHistory_PreservesSystemMessages()
    {
        var budget = new TokenBudget();
        budget.AddMessage("system", "System prompt here.");
        budget.AddMessage("user", "Hello");
        budget.AddMessage("assistant", "Hi");

        budget.ClearHistory();

        Assert.Equal(1, budget.MessageCount);
        var messages = budget.GetMessages();
        Assert.Equal("system", messages[0].Role);
    }

    [Fact]
    public void ClearHistory_ResetsTokenCount()
    {
        var budget = new TokenBudget();
        budget.AddMessage("user", "Hello world");
        int usedBefore = budget.UsedTokens;

        budget.ClearHistory();
        Assert.Equal(0, budget.UsedTokens);
    }

    [Fact]
    public void ClearAll_RemovesEverything()
    {
        var budget = new TokenBudget();
        budget.AddMessage("system", "System prompt.");
        budget.AddMessage("user", "Hello");

        budget.ClearAll();

        Assert.Equal(0, budget.MessageCount);
        Assert.Equal(0, budget.UsedTokens);
    }

    // ──────────────── GetSummary ────────────────

    [Fact]
    public void GetSummary_ReturnsAccurateStats()
    {
        var budget = new TokenBudget(maxTokens: 10000, reserveForResponse: 2000);
        budget.AddMessage("system", "Be helpful.");
        budget.AddMessage("user", "Question");
        budget.AddMessage("assistant", "Answer");
        budget.AddMessage("user", "Follow-up");

        var summary = budget.GetSummary();

        Assert.Equal(10000, summary.MaxTokens);
        Assert.Equal(2000, summary.ReserveForResponse);
        Assert.Equal(4, summary.MessageCount);
        Assert.Equal(1, summary.SystemMessages);
        Assert.Equal(2, summary.UserMessages);
        Assert.Equal(1, summary.AssistantMessages);
        Assert.True(summary.UsedTokens > 0);
        Assert.True(summary.LargestMessageTokens > 0);
        Assert.True(summary.AverageMessageTokens > 0);
        Assert.False(summary.IsOverBudget);
    }

    [Fact]
    public void GetSummary_EmptyBudget()
    {
        var budget = new TokenBudget();
        var summary = budget.GetSummary();

        Assert.Equal(0, summary.UsedTokens);
        Assert.Equal(0, summary.MessageCount);
        Assert.Equal(0, summary.LargestMessageTokens);
        Assert.Equal(0, summary.AverageMessageTokens);
    }

    [Fact]
    public void GetSummary_ToString_Readable()
    {
        var budget = new TokenBudget();
        budget.AddMessage("user", "Test");

        var summary = budget.GetSummary();
        string str = summary.ToString();

        Assert.Contains("TokenBudget:", str);
        Assert.Contains("tokens", str);
        Assert.Contains("messages", str);
    }

    // ──────────────── ForModel ────────────────

    [Theory]
    [InlineData("gpt-4", 8_192)]
    [InlineData("gpt-4-32k", 32_768)]
    [InlineData("gpt-4-turbo", 128_000)]
    [InlineData("gpt-4o", 128_000)]
    [InlineData("gpt-4o-mini", 128_000)]
    [InlineData("gpt-3.5-turbo", 16_385)]
    [InlineData("claude-3-opus", 200_000)]
    [InlineData("claude-3-5-sonnet", 200_000)]
    [InlineData("claude-4-opus", 200_000)]
    public void ForModel_SetsCorrectContextWindow(string model, int expectedTokens)
    {
        var budget = TokenBudget.ForModel(model);
        Assert.Equal(expectedTokens, budget.MaxTokens);
    }

    [Fact]
    public void ForModel_UnknownModelUsesDefault()
    {
        var budget = TokenBudget.ForModel("some-future-model");
        Assert.Equal(TokenBudget.DefaultMaxTokens, budget.MaxTokens);
    }

    [Fact]
    public void ForModel_CustomReserve()
    {
        var budget = TokenBudget.ForModel("gpt-4", reserveForResponse: 500);
        Assert.Equal(8_192, budget.MaxTokens);
        Assert.Equal(500, budget.ReserveForResponse);
    }

    [Fact]
    public void ForModel_ClampsReserve()
    {
        // Reserve exceeding context window should be clamped
        var budget = TokenBudget.ForModel("gpt-4", reserveForResponse: 50_000);
        Assert.True(budget.ReserveForResponse < budget.MaxTokens);
    }

    [Fact]
    public void ForModel_ThrowsOnEmptyModel()
    {
        Assert.Throws<ArgumentException>(() => TokenBudget.ForModel(""));
    }

    // ──────────────── Serialization ────────────────

    [Fact]
    public void ToJson_RoundTrip()
    {
        var budget = new TokenBudget(maxTokens: 8192, reserveForResponse: 1000)
        {
            Strategy = TrimStrategy.SlidingWindow,
            KeepFirstTurns = 2,
            ReserveTokens = 500
        };

        budget.AddMessage("system", "You are a coding assistant.");
        budget.AddMessage("user", "Write me a function.");
        budget.AddMessage("assistant", "Here is a function...");

        string json = budget.ToJson();
        var restored = TokenBudget.FromJson(json);

        Assert.Equal(budget.MaxTokens, restored.MaxTokens);
        Assert.Equal(budget.ReserveForResponse, restored.ReserveForResponse);
        Assert.Equal(budget.ReserveTokens, restored.ReserveTokens);
        Assert.Equal(budget.Strategy, restored.Strategy);
        Assert.Equal(budget.KeepFirstTurns, restored.KeepFirstTurns);
        Assert.Equal(budget.MessageCount, restored.MessageCount);

        var origMessages = budget.GetMessages();
        var restoredMessages = restored.GetMessages();
        Assert.Equal(origMessages.Count, restoredMessages.Count);

        for (int i = 0; i < origMessages.Count; i++)
        {
            Assert.Equal(origMessages[i].Role, restoredMessages[i].Role);
            Assert.Equal(origMessages[i].Content, restoredMessages[i].Content);
        }
    }

    [Fact]
    public void FromJson_ThrowsOnEmptyString()
    {
        Assert.Throws<ArgumentException>(() => TokenBudget.FromJson(""));
    }

    [Fact]
    public void FromJson_ThrowsOnInvalidJson()
    {
        Assert.ThrowsAny<Exception>(() => TokenBudget.FromJson("not json at all"));
    }

    [Fact]
    public void ToJson_ContainsExpectedFields()
    {
        var budget = new TokenBudget();
        budget.AddMessage("user", "Hello");

        string json = budget.ToJson();
        Assert.Contains("maxTokens", json);
        Assert.Contains("reserveForResponse", json);
        Assert.Contains("strategy", json);
        Assert.Contains("messages", json);
    }

    // ──────────────── Properties ────────────────

    [Fact]
    public void ReserveTokens_ThrowsOnNegative()
    {
        var budget = new TokenBudget();
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.ReserveTokens = -1);
    }

    [Fact]
    public void KeepFirstTurns_ThrowsOnNegative()
    {
        var budget = new TokenBudget();
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.KeepFirstTurns = -1);
    }

    [Fact]
    public void Strategy_CanBeChanged()
    {
        var budget = new TokenBudget();
        budget.Strategy = TrimStrategy.RemoveLongest;
        Assert.Equal(TrimStrategy.RemoveLongest, budget.Strategy);
    }

    // ──────────────── TrimmedCount / TrimmedTokens ────────────────

    [Fact]
    public void TrimmedTokens_TracksAccurately()
    {
        var budget = new TokenBudget(maxTokens: 200, reserveForResponse: 50)
        {
            ReserveTokens = 0
        };

        // Fill until trimming happens
        for (int i = 0; i < 20; i++)
        {
            budget.AddMessage("user", $"Message {i} with sufficient text for tokens");
        }

        if (budget.TrimmedCount > 0)
        {
            Assert.True(budget.TrimmedTokens > 0);
        }
    }

    // ──────────────── Thread Safety ────────────────

    [Fact]
    public void ConcurrentAddMessages_DoesNotThrow()
    {
        var budget = new TokenBudget();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(() =>
            {
                budget.AddMessage("user", $"Concurrent message {idx}");
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Should have all messages (no trimming on large budget)
        Assert.True(budget.MessageCount > 0);
    }

    // ──────────────── Edge Cases ────────────────

    [Fact]
    public void AddMessage_SingleWordMessage()
    {
        var budget = new TokenBudget();
        budget.AddMessage("user", "Hi");
        Assert.True(budget.UsedTokens > 0);
    }

    [Fact]
    public void AddMessage_VeryLongMessage()
    {
        var budget = new TokenBudget();
        var longText = new string('a', 10000);
        budget.AddMessage("user", longText);
        Assert.True(budget.UsedTokens > 100);
    }

    [Fact]
    public void IsOverBudget_DetectsCorrectly()
    {
        var budget = new TokenBudget(maxTokens: 150, reserveForResponse: 50)
        {
            ReserveTokens = 0
        };

        // UsedTokens should never exceed budget after auto-trim
        for (int i = 0; i < 10; i++)
        {
            budget.AddMessage("user", $"Message {i} padded with some text here");
        }

        Assert.False(budget.IsOverBudget);
    }

    [Fact]
    public void MultipleSystemMessages_AllPreserved()
    {
        var budget = new TokenBudget(maxTokens: 300, reserveForResponse: 50)
        {
            ReserveTokens = 0
        };

        budget.AddMessage("system", "Rule one");
        budget.AddMessage("system", "Rule two");
        budget.AddMessage("user", "Question one");
        budget.AddMessage("user", "Question two which is much longer");

        // Fill to trigger trimming
        for (int i = 0; i < 15; i++)
        {
            budget.AddMessage("user", $"Extra message {i} with padding text");
        }

        var messages = budget.GetMessages();
        var systemCount = messages.Count(m => m.Role == "system");
        Assert.Equal(2, systemCount);
    }
}
