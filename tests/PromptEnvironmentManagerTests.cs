namespace Prompt.Tests
{
    using Xunit;

    public class PromptEnvironmentManagerTests
    {
        private PromptEnvironmentManager CreateManager()
        {
            var mgr = new PromptEnvironmentManager();
            mgr.AddEnvironment(new PromptEnvironment("dev",
                new Dictionary<string, string> { ["tone"] = "casual", ["verbosity"] = "high" },
                description: "Development"));
            mgr.AddEnvironment(new PromptEnvironment("staging",
                new Dictionary<string, string> { ["tone"] = "professional", ["verbosity"] = "medium" }));
            mgr.AddEnvironment(new PromptEnvironment("prod",
                new Dictionary<string, string> { ["tone"] = "formal", ["verbosity"] = "low" },
                description: "Production"));
            return mgr;
        }

        private PromptTemplate MakeTemplate(string text = "You are a {{tone}} assistant with {{verbosity}} output.")
            => new PromptTemplate(text, new Dictionary<string, string> { ["tone"] = "default", ["verbosity"] = "medium" });

        // === Environment Management ===

        [Fact]
        public void AddEnvironment_Success()
        {
            var mgr = new PromptEnvironmentManager();
            mgr.AddEnvironment(new PromptEnvironment("dev"));
            Assert.Single(mgr.Environments);
            Assert.Equal("dev", mgr.Environments[0].Name);
        }

        [Fact]
        public void AddEnvironment_Duplicate_Throws()
        {
            var mgr = new PromptEnvironmentManager();
            mgr.AddEnvironment(new PromptEnvironment("dev"));
            Assert.Throws<InvalidOperationException>(() =>
                mgr.AddEnvironment(new PromptEnvironment("dev")));
        }

        [Fact]
        public void AddEnvironment_Null_Throws()
        {
            var mgr = new PromptEnvironmentManager();
            Assert.Throws<ArgumentNullException>(() => mgr.AddEnvironment(null!));
        }

        [Fact]
        public void GetEnvironment_Found()
        {
            var mgr = CreateManager();
            var env = mgr.GetEnvironment("dev");
            Assert.Equal("dev", env.Name);
            Assert.Equal("Development", env.Description);
        }

        [Fact]
        public void GetEnvironment_NotFound_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<KeyNotFoundException>(() => mgr.GetEnvironment("unknown"));
        }

        [Fact]
        public void GetEnvironment_CaseInsensitive()
        {
            var mgr = CreateManager();
            var env = mgr.GetEnvironment("DEV");
            Assert.Equal("dev", env.Name);
        }

        [Fact]
        public void RemoveEnvironment_Success()
        {
            var mgr = CreateManager();
            Assert.True(mgr.RemoveEnvironment("dev"));
            Assert.Equal(2, mgr.Environments.Count);
        }

        [Fact]
        public void RemoveEnvironment_Locked_Throws()
        {
            var mgr = CreateManager();
            mgr.GetEnvironment("prod").Locked = true;
            Assert.Throws<InvalidOperationException>(() => mgr.RemoveEnvironment("prod"));
        }

        // === Environment Variables ===

        [Fact]
        public void SetVariable_Success()
        {
            var env = new PromptEnvironment("test");
            env.SetVariable("key", "value");
            Assert.Equal("value", env.Variables["key"]);
        }

        [Fact]
        public void SetVariable_Locked_Throws()
        {
            var env = new PromptEnvironment("test");
            env.Locked = true;
            Assert.Throws<InvalidOperationException>(() => env.SetVariable("key", "value"));
        }

        [Fact]
        public void SetVariable_EmptyKey_Throws()
        {
            var env = new PromptEnvironment("test");
            Assert.Throws<ArgumentException>(() => env.SetVariable("", "value"));
        }

        [Fact]
        public void RemoveVariable_Success()
        {
            var env = new PromptEnvironment("test", new Dictionary<string, string> { ["k"] = "v" });
            Assert.True(env.RemoveVariable("k"));
            Assert.Empty(env.Variables);
        }

        [Fact]
        public void RemoveVariable_Locked_Throws()
        {
            var env = new PromptEnvironment("test", new Dictionary<string, string> { ["k"] = "v" });
            env.Locked = true;
            Assert.Throws<InvalidOperationException>(() => env.RemoveVariable("k"));
        }

        // === PromptEnvironment Construction ===

        [Fact]
        public void Environment_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PromptEnvironment(""));
        }

        [Fact]
        public void Environment_InvalidChars_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PromptEnvironment("my env!"));
        }

        [Fact]
        public void Environment_ValidNames()
        {
            var e1 = new PromptEnvironment("dev-01");
            var e2 = new PromptEnvironment("staging_v2");
            Assert.Equal("dev-01", e1.Name);
            Assert.Equal("staging_v2", e2.Name);
        }

        // === Prompt Registration & Rendering ===

        [Fact]
        public void RegisterPrompt_Success()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            Assert.Contains("test", mgr.PromptNames);
        }

        [Fact]
        public void RegisterPrompt_EmptyName_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<ArgumentException>(() => mgr.RegisterPrompt("", MakeTemplate()));
        }

        [Fact]
        public void RegisterPrompt_NullTemplate_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<ArgumentNullException>(() => mgr.RegisterPrompt("test", null!));
        }

        [Fact]
        public void Render_WithEnvVariables()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var result = mgr.Render("test", "dev");
            Assert.Equal("You are a casual assistant with high output.", result);
        }

        [Fact]
        public void Render_DifferentEnvs_DifferentResults()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var dev = mgr.Render("test", "dev");
            var prod = mgr.Render("test", "prod");
            Assert.NotEqual(dev, prod);
            Assert.Contains("casual", dev);
            Assert.Contains("formal", prod);
        }

        [Fact]
        public void Render_WithAdditionalVars()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var result = mgr.Render("test", "dev",
                new Dictionary<string, string> { ["tone"] = "override" });
            Assert.Contains("override", result);
        }

        [Fact]
        public void Render_UnknownPrompt_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<KeyNotFoundException>(() => mgr.Render("missing", "dev"));
        }

        [Fact]
        public void Render_UnknownEnv_Throws()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            Assert.Throws<KeyNotFoundException>(() => mgr.Render("test", "missing"));
        }

        // === Prompt Overrides ===

        [Fact]
        public void SetPromptOverride_Success()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var overrideTemplate = new PromptTemplate("PROD: Be {{tone}}.");
            mgr.SetPromptOverride("test", "prod", overrideTemplate);
            var result = mgr.Render("test", "prod");
            Assert.Equal("PROD: Be formal.", result);
        }

        [Fact]
        public void SetPromptOverride_UnknownPrompt_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<KeyNotFoundException>(() =>
                mgr.SetPromptOverride("missing", "dev", MakeTemplate()));
        }

        [Fact]
        public void SetPromptOverride_LockedEnv_Throws()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.GetEnvironment("prod").Locked = true;
            Assert.Throws<InvalidOperationException>(() =>
                mgr.SetPromptOverride("test", "prod", MakeTemplate()));
        }

        // === Promotion ===

        [Fact]
        public void Promote_Basic()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var devOverride = new PromptTemplate("Dev version: {{tone}}");
            mgr.SetPromptOverride("test", "dev", devOverride);

            var record = mgr.Promote("test", "dev", "staging", promotedBy: "alice");
            Assert.Equal("test", record.PromptName);
            Assert.Equal("dev", record.FromEnvironment);
            Assert.Equal("staging", record.ToEnvironment);
            Assert.Equal("alice", record.PromotedBy);

            var stagingResult = mgr.Render("test", "staging");
            Assert.Equal("Dev version: professional", stagingResult);
        }

        [Fact]
        public void Promote_SameEnv_Throws()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            Assert.Throws<ArgumentException>(() => mgr.Promote("test", "dev", "dev"));
        }

        [Fact]
        public void Promote_LockedTarget_Throws()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.GetEnvironment("prod").Locked = true;
            Assert.Throws<InvalidOperationException>(() => mgr.Promote("test", "dev", "prod"));
        }

        [Fact]
        public void Promote_UnknownPrompt_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<KeyNotFoundException>(() => mgr.Promote("missing", "dev", "staging"));
        }

        [Fact]
        public void Promote_RecordsHistory()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.Promote("test", "dev", "staging", promotedBy: "alice", notes: "v1");
            mgr.Promote("test", "staging", "prod", promotedBy: "bob", notes: "v1 release");

            Assert.Equal(2, mgr.PromotionHistory.Count);
            var history = mgr.GetPromotionHistory("test");
            Assert.Equal(2, history.Count);
        }

        // === Pipeline ===

        [Fact]
        public void SetPromotionPipeline_Success()
        {
            var mgr = CreateManager();
            mgr.SetPromotionPipeline("dev", "staging", "prod");
            Assert.Equal(3, mgr.PromotionPipeline.Count);
        }

        [Fact]
        public void SetPromotionPipeline_TooFewStages_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<ArgumentException>(() => mgr.SetPromotionPipeline("dev"));
        }

        [Fact]
        public void SetPromotionPipeline_UnknownEnv_Throws()
        {
            var mgr = CreateManager();
            Assert.Throws<ArgumentException>(() => mgr.SetPromotionPipeline("dev", "unknown"));
        }

        [Fact]
        public void Promote_PipelineViolation_Throws()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.SetPromotionPipeline("dev", "staging", "prod");
            Assert.Throws<InvalidOperationException>(() => mgr.Promote("test", "dev", "prod"));
        }

        [Fact]
        public void Promote_PipelineCompliant_Succeeds()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.SetPromotionPipeline("dev", "staging", "prod");
            mgr.Promote("test", "dev", "staging");
            mgr.Promote("test", "staging", "prod");
            Assert.Equal(2, mgr.PromotionHistory.Count);
        }

        // === Rollback ===

        [Fact]
        public void Rollback_ToPreviousPromotion()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var v1 = new PromptTemplate("V1: {{tone}}");
            var v2 = new PromptTemplate("V2: {{tone}}");
            mgr.SetPromptOverride("test", "dev", v1);
            mgr.Promote("test", "dev", "staging");
            mgr.SetPromptOverride("test", "dev", v2);
            mgr.Promote("test", "dev", "staging");

            var result = mgr.Render("test", "staging");
            Assert.Equal("V2: professional", result);

            mgr.Rollback("test", "staging");
            result = mgr.Render("test", "staging");
            Assert.Equal("V1: professional", result);
        }

        [Fact]
        public void Rollback_NoHistory_RevertsToBase()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.Promote("test", "dev", "staging");
            mgr.Rollback("test", "staging");
            // Should render base template with staging vars
            var result = mgr.Render("test", "staging");
            Assert.Contains("professional", result);
        }

        [Fact]
        public void Rollback_LockedEnv_Throws()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.GetEnvironment("prod").Locked = true;
            Assert.Throws<InvalidOperationException>(() => mgr.Rollback("test", "prod"));
        }

        // === Compare ===

        [Fact]
        public void Compare_DifferentEnvs()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var cmp = mgr.Compare("test", "dev", "prod");
            Assert.False(cmp.AreIdentical);
            Assert.Contains("casual", cmp.RenderA);
            Assert.Contains("formal", cmp.RenderB);
            Assert.True(cmp.VariableDiffs.Count > 0);
        }

        [Fact]
        public void Compare_SameVars_Identical()
        {
            var mgr = new PromptEnvironmentManager();
            mgr.AddEnvironment(new PromptEnvironment("a", new Dictionary<string, string> { ["x"] = "1" }));
            mgr.AddEnvironment(new PromptEnvironment("b", new Dictionary<string, string> { ["x"] = "1" }));
            mgr.RegisterPrompt("test", new PromptTemplate("Val: {{x}}"));
            var cmp = mgr.Compare("test", "a", "b");
            Assert.True(cmp.AreIdentical);
            Assert.Empty(cmp.VariableDiffs);
        }

        // === Validation ===

        [Fact]
        public void ValidateAcrossEnvironments_AllValid()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var results = mgr.ValidateAcrossEnvironments("test");
            Assert.Equal(3, results.Count);
            Assert.All(results.Values, v => Assert.Null(v));
        }

        // === Deployment Matrix ===

        [Fact]
        public void GetDeploymentMatrix_ShowsBaseAndOverrides()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.SetPromptOverride("test", "prod", new PromptTemplate("prod override"));
            var matrix = mgr.GetDeploymentMatrix();
            Assert.Contains("test", matrix.Keys);
            Assert.Contains(matrix["test"], e => e.Contains("prod") && !e.Contains("base"));
            Assert.Contains(matrix["test"], e => e.Contains("base"));
        }

        // === Export ===

        [Fact]
        public void ExportJson_ContainsAllData()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            mgr.SetPromotionPipeline("dev", "staging", "prod");
            mgr.Promote("test", "dev", "staging", promotedBy: "alice");
            var json = mgr.ExportJson();
            Assert.Contains("dev", json);
            Assert.Contains("staging", json);
            Assert.Contains("prod", json);
            Assert.Contains("test", json);
            Assert.Contains("alice", json);
            Assert.Contains("pipeline", json);
        }

        // === PromotionRecord ===

        [Fact]
        public void PromotionRecord_HasUniqueId()
        {
            var r1 = new PromotionRecord("p", "dev", "staging", "template",
                new Dictionary<string, string>());
            var r2 = new PromotionRecord("p", "dev", "staging", "template",
                new Dictionary<string, string>());
            Assert.NotEqual(r1.Id, r2.Id);
        }

        [Fact]
        public void PromotionRecord_SnapshotsAreImmutable()
        {
            var vars = new Dictionary<string, string> { ["k"] = "v" };
            var record = new PromotionRecord("p", "dev", "staging", "t", vars);
            vars["k"] = "changed";
            Assert.Equal("v", record.VariablesSnapshot["k"]);
        }

        // === Edge Cases ===

        [Fact]
        public void Render_BaseTemplate_WhenNoOverride()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("test", MakeTemplate());
            var result = mgr.Render("test", "dev");
            Assert.Contains("casual", result);
        }

        [Fact]
        public void MultiplePrompts_IndependentPromotions()
        {
            var mgr = CreateManager();
            mgr.RegisterPrompt("a", new PromptTemplate("A: {{tone}}"));
            mgr.RegisterPrompt("b", new PromptTemplate("B: {{tone}}"));
            mgr.Promote("a", "dev", "staging");
            Assert.Single(mgr.GetPromotionHistory("a"));
            Assert.Empty(mgr.GetPromotionHistory("b"));
        }

        [Fact]
        public void RemoveEnvironment_CleansUpPipeline()
        {
            var mgr = CreateManager();
            mgr.SetPromotionPipeline("dev", "staging", "prod");
            mgr.RemoveEnvironment("staging");
            Assert.DoesNotContain("staging", mgr.PromotionPipeline);
        }

        [Fact]
        public void Environment_ModelAndTemperatureConfig()
        {
            var env = new PromptEnvironment("test");
            env.Model = "gpt-4";
            env.MaxTokens = 2000;
            env.Temperature = 0.7;
            Assert.Equal("gpt-4", env.Model);
            Assert.Equal(2000, env.MaxTokens);
            Assert.Equal(0.7, env.Temperature);
        }
    }
}
