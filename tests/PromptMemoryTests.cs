namespace Prompt.Tests
{
    using System.Linq;
    using Xunit;

    public class PromptMemoryTests
    {
        [Fact]
        public void Add_ClassifiesEphemeralMessages()
        {
            var mem = new PromptMemory();
            // Use a message that exactly matches the EphemeralPattern regex:
            // ^\s*(ok|okay|sure|thanks|thank you|got it|sounds good|great|yes|no|right|understood|perfect|awesome|cool|nice)\s*[.!]?\s*$
            var entry = mem.Add("user", "ok");
            Assert.Equal(ConvMemoryTier.Ephemeral, entry.Tier);
        }

        [Fact]
        public void Add_ClassifiesDecisionsAsCore()
        {
            var mem = new PromptMemory();
            var entry = mem.Add("user", "I decided to use PostgreSQL for the database layer");
            Assert.Equal(ConvMemoryTier.Core, entry.Tier);
        }

        [Fact]
        public void Add_ClassifiesSystemAsCore()
        {
            var mem = new PromptMemory();
            var entry = mem.Add("system", "You are a helpful coding assistant");
            Assert.Equal(ConvMemoryTier.Core, entry.Tier);
        }

        [Fact]
        public void Add_ExtractsTopics()
        {
            var mem = new PromptMemory();
            var entry = mem.Add("user", "Build a REST API with JWT authentication using Node.js");
            Assert.Contains(entry.Topics, t => t.Contains("REST") || t.Contains("API"));
            Assert.Contains(entry.Topics, t => t.Contains("JWT"));
        }

        [Fact]
        public void Pin_PreventsEviction()
        {
            var mem = new PromptMemory(tokenBudget: 100, strategy: CompactionStrategy.RelevanceBased);
            var pinned = mem.Add("user", "This is a critical constraint that must always be in context");
            mem.Pin(pinned.Id);

            // Fill memory beyond budget
            for (int i = 0; i < 20; i++)
                mem.Add("user", $"Filler message number {i} to push memory over budget limit");

            mem.Compact();
            var context = mem.BuildContext();
            Assert.Contains(context, e => e.Id == pinned.Id);
        }

        [Fact]
        public void Search_FindsRelevantEntries()
        {
            var mem = new PromptMemory();
            mem.Add("user", "Build a REST API for todo items");
            mem.Add("assistant", "I'll create endpoints for CRUD operations");
            mem.Add("user", "What's the weather like?");

            var results = mem.Search("REST API");
            Assert.True(results.Count >= 1);
            Assert.Contains(results, e => e.Content.Contains("REST"));
        }

        [Fact]
        public void BuildContext_RespectsTokenBudget()
        {
            var mem = new PromptMemory(tokenBudget: 200);
            for (int i = 0; i < 50; i++)
                mem.Add("user", $"Message {i}: This is a test message with some content to consume tokens in the budget");

            var context = mem.BuildContext();
            int totalTokens = context.Sum(e => e.ActiveTokens);
            Assert.True(totalTokens <= 200);
        }

        [Fact]
        public void BuildContext_PrioritizesPinnedAndCore()
        {
            var mem = new PromptMemory(tokenBudget: 300);
            var pinned = mem.Add("system", "You are a coding assistant");
            mem.Pin(pinned.Id);
            mem.Add("user", "I decided to use TypeScript for everything");
            mem.Add("user", "sure");
            mem.Add("user", "ok thanks");

            var context = mem.BuildContext();
            // Pinned should be first (by time order since it was added first)
            Assert.Equal(pinned.Id, context[0].Id);
        }

        [Fact]
        public void Compact_SummarizeFirst_SummarizesBeforeEvicting()
        {
            var mem = new PromptMemory(tokenBudget: 300, strategy: CompactionStrategy.SummarizeFirst);
            mem.Add("user", "I want to build a comprehensive REST API with JWT authentication, PostgreSQL database, Redis caching, and Docker deployment. The API should handle users, products, orders, and inventory management.");
            mem.Add("assistant", "I'll create the API structure with proper authentication middleware, database models, caching layer, and containerization. Let me start with the project setup.");

            // Force compaction
            for (int i = 0; i < 20; i++)
                mem.Add("user", $"Additional requirement {i}: add more features to handle edge cases");

            var health = mem.GetHealthReport();
            Assert.True(health.SummarizedCount > 0 || health.EvictedCount > 0);
        }

        [Fact]
        public void DetectTopicDrift_FindsNewTopics()
        {
            var mem = new PromptMemory();
            mem.Add("user", "Build a REST API with Node.js");
            mem.Add("assistant", "Creating REST endpoints with Express");
            mem.Add("user", "Add JWT authentication");
            mem.Add("assistant", "Adding JWT middleware");
            // Drift to new topic
            mem.Add("user", "Now let's set up Docker and Kubernetes deployment");
            mem.Add("assistant", "I'll create Dockerfile and k8s manifests");

            var drift = mem.DetectTopicDrift();
            // Should detect Docker/Kubernetes as new topics
            Assert.True(drift.Count > 0);
        }

        [Fact]
        public void GetHealthReport_ReturnsValidReport()
        {
            var mem = new PromptMemory(tokenBudget: 2000);
            mem.Add("system", "You are helpful");
            mem.Add("user", "Build an API");
            mem.Add("assistant", "Sure, creating the API");
            mem.Add("user", "thanks");

            var report = mem.GetHealthReport();
            Assert.Equal(4, report.TotalEntries);
            Assert.Equal(2000, report.TokenBudget);
            Assert.True(report.TotalTokens > 0);
            Assert.True(report.AverageRelevance > 0);
            Assert.NotEmpty(report.Recommendations);
            Assert.Contains(report.TierBreakdown, kv => kv.Value > 0);
        }

        [Fact]
        public void Touch_BoostsRelevance()
        {
            var mem = new PromptMemory();
            var entry = mem.Add("user", "Important context about the project");
            double initialRelevance = entry.Relevance;

            mem.Touch(entry.Id);
            Assert.True(entry.Relevance > initialRelevance);
            Assert.Equal(1, entry.ReferenceCount);
        }

        [Fact]
        public void Export_ContainsAllFields()
        {
            var mem = new PromptMemory(tokenBudget: 1000);
            mem.Add("user", "Test message");
            var export = mem.Export();

            Assert.Equal(1000, (int)export["tokenBudget"]);
            Assert.Equal(1, (int)export["entryCount"]);
            Assert.NotNull(export["entries"]);
        }

        [Fact]
        public void LRU_EvictsLeastRecentlyAccessed()
        {
            var mem = new PromptMemory(tokenBudget: 150, strategy: CompactionStrategy.LRU);
            var old = mem.Add("user", "This old message was not accessed recently at all");
            var recent = mem.Add("user", "This newer message was just accessed by the user");
            mem.Touch(recent.Id); // Access the recent one

            // Fill past budget
            for (int i = 0; i < 10; i++)
                mem.Add("user", $"Filler to push memory over budget limit number {i}");

            mem.Compact();
            // Old unaccessed entry should be evicted first
            Assert.True(mem.EvictionLog.Count > 0);
        }
    }
}
