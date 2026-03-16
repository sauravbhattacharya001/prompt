namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptPromotionManagerTests
    {
        [Fact] public void Register_Valid() { var m=new PromptPromotionManager(); var p=m.Register("greet","Hello","alice"); Assert.Equal("greet",p.Id); Assert.Equal(PromptStage.Draft,p.Stage); Assert.Equal(1,p.Version); Assert.Equal("alice",p.Author); Assert.Equal(1,m.Count); }
        [Fact] public void Register_WithTags() { var m=new PromptPromotionManager(); var p=m.Register("t","c","a","x","y"); Assert.Contains("x",p.Tags); Assert.Contains("y",p.Tags); }
        [Fact] public void Register_Duplicate_Throws() { var m=new PromptPromotionManager(); m.Register("a","c","u"); Assert.Throws<ArgumentException>(()=>m.Register("a","c2","u2")); }
        [Theory][InlineData(null)][InlineData("")][InlineData("  ")]
        public void Register_EmptyId_Throws(string id) => Assert.Throws<ArgumentException>(()=>new PromptPromotionManager().Register(id,"c","a"));
        [Fact] public void Register_InvalidId_Throws() => Assert.Throws<ArgumentException>(()=>new PromptPromotionManager().Register("bad id!","c","a"));
        [Theory][InlineData(null)][InlineData("")][InlineData("  ")]
        public void Register_EmptyContent_Throws(string c) => Assert.Throws<ArgumentException>(()=>new PromptPromotionManager().Register("id",c,"a"));
        [Theory][InlineData(null)][InlineData("")][InlineData("  ")]
        public void Register_EmptyAuthor_Throws(string a) => Assert.Throws<ArgumentException>(()=>new PromptPromotionManager().Register("id","c",a));

        [Fact] public void Get_Found() { var m=new PromptPromotionManager(); m.Register("x","c","a"); Assert.NotNull(m.Get("x")); }
        [Fact] public void Get_NotFound() { var m=new PromptPromotionManager(); Assert.Null(m.Get("nope")); }
        [Fact] public void Get_Null() => Assert.Null(new PromptPromotionManager().Get(null!));

        [Fact] public void UpdateContent_Draft() { var m=new PromptPromotionManager(); m.Register("x","old","a"); Assert.True(m.UpdateContent("x","new","a")); Assert.Equal("new",m.Get("x")!.Content); }
        [Fact] public void UpdateContent_Staging() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); Assert.True(m.UpdateContent("x","new","a")); }
        [Fact] public void UpdateContent_Production_Blocked() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Promote("x","a"); Assert.False(m.UpdateContent("x","new","a")); }
        [Fact] public void UpdateContent_ClearsApprovals() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Promote("x","a"); m.Approve("x","bob"); Assert.True(m.Get("x")!.CurrentApprovals.Count>0); m.UpdateContent("x","new","a"); Assert.Empty(m.Get("x")!.CurrentApprovals); }
        [Fact] public void UpdateContent_NotFound() => Assert.False(new PromptPromotionManager().UpdateContent("x","c","a"));

        [Fact] public void Promote_DraftToStaging() { var m=new PromptPromotionManager(); m.Register("x","c","a"); var r=m.Promote("x","a"); Assert.True(r.Success); Assert.Equal(PromptStage.Staging,r.NewStage); Assert.Equal(PromptStage.Staging,m.Get("x")!.Stage); Assert.Equal(2,m.Get("x")!.Version); }
        [Fact] public void Promote_StagingToProduction_NoApprovers() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); var r=m.Promote("x","a"); Assert.True(r.Success); Assert.Equal(PromptStage.Production,r.NewStage); }
        [Fact] public void Promote_StagingToProduction_Approved() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Promote("x","a"); m.Approve("x","bob"); var r=m.Promote("x","a"); Assert.True(r.Success); Assert.Equal(PromptStage.Production,r.NewStage); }
        [Fact] public void Promote_StagingToProduction_Blocked() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Promote("x","a"); var r=m.Promote("x","a"); Assert.False(r.Success); Assert.NotEmpty(r.BlockReasons); Assert.Equal(PromptStage.Staging,m.Get("x")!.Stage); }
        [Fact] public void Promote_ProductionToDeprecated() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Promote("x","a"); var r=m.Promote("x","a"); Assert.True(r.Success); Assert.Equal(PromptStage.Deprecated,r.NewStage); }
        [Fact] public void Promote_Deprecated_Fails() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Promote("x","a"); m.Promote("x","a"); var r=m.Promote("x","a"); Assert.False(r.Success); }
        [Fact] public void Promote_NotFound() { var r=new PromptPromotionManager().Promote("nope","a"); Assert.False(r.Success); }
        [Fact] public void Promote_RecordsHistory() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","actor1","go!"); var h=m.Get("x")!.History; Assert.Single(h); Assert.Equal(PromptStage.Draft,h[0].FromStage); Assert.Equal(PromptStage.Staging,h[0].ToStage); Assert.Equal("actor1",h[0].Actor); Assert.Equal("go!",h[0].Reason); Assert.False(h[0].IsRollback); Assert.Equal("c",h[0].PromptSnapshot); }

        [Fact] public void Rollback_ProductionToStaging() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Promote("x","a"); var r=m.Rollback("x","a","bug"); Assert.True(r.Success); Assert.Equal(PromptStage.Staging,r.NewStage); Assert.Equal(PromptStage.Staging,m.Get("x")!.Stage); }
        [Fact] public void Rollback_StagingToDraft() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); var r=m.Rollback("x","a"); Assert.True(r.Success); Assert.Equal(PromptStage.Draft,r.NewStage); }
        [Fact] public void Rollback_Draft_Fails() { var m=new PromptPromotionManager(); m.Register("x","c","a"); Assert.False(m.Rollback("x","a").Success); }
        [Fact] public void Rollback_Deprecated_Fails() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Promote("x","a"); m.Promote("x","a"); Assert.False(m.Rollback("x","a").Success); }
        [Fact] public void Rollback_ClearsApprovals() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Promote("x","a"); m.Approve("x","bob"); m.Rollback("x","a"); Assert.Empty(m.Get("x")!.CurrentApprovals); }
        [Fact] public void Rollback_NotFound() => Assert.False(new PromptPromotionManager().Rollback("nope","a").Success);
        [Fact] public void Rollback_RecordsHistory() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Rollback("x","actor","oops"); var h=m.Get("x")!.History.Last(); Assert.True(h.IsRollback); Assert.Equal("oops",h.Reason); }

        [Fact] public void Approve_Valid() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); Assert.True(m.Approve("x","bob")); }
        [Fact] public void Approve_NotApprover() { var m=new PromptPromotionManager(); m.Register("x","c","a"); Assert.False(m.Approve("x","stranger")); }
        [Fact] public void Approve_Duplicate() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Approve("x","bob"); Assert.False(m.Approve("x","bob")); }
        [Fact] public void Approve_NotFound() => Assert.False(new PromptPromotionManager().Approve("x","bob"));

        [Fact] public void IsFullyApproved_NoApprovers() { var m=new PromptPromotionManager(); m.Register("x","c","a"); Assert.True(m.IsFullyApproved("x")); }
        [Fact] public void IsFullyApproved_AllApproved() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","b1"); m.AddApprover("x","b2"); m.Approve("x","b1"); m.Approve("x","b2"); Assert.True(m.IsFullyApproved("x")); }
        [Fact] public void IsFullyApproved_Partial() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","b1"); m.AddApprover("x","b2"); m.Approve("x","b1"); Assert.False(m.IsFullyApproved("x")); }

        [Fact] public void AddApprover_OK() { var m=new PromptPromotionManager(); m.Register("x","c","a"); Assert.True(m.AddApprover("x","bob")); Assert.Contains("bob",m.Get("x")!.RequiredApprovers); }
        [Fact] public void AddApprover_NotFound() => Assert.False(new PromptPromotionManager().AddApprover("x","bob"));
        [Fact] public void RemoveApprover_OK() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); Assert.True(m.RemoveApprover("x","bob")); }
        [Fact] public void RemoveApprover_NotFound() => Assert.False(new PromptPromotionManager().RemoveApprover("x","bob"));

        [Fact] public void RestoreFromHistory_OK() { var m=new PromptPromotionManager(); m.Register("x","v1","a"); m.Promote("x","a"); m.UpdateContent("x","v2","a"); Assert.True(m.RestoreFromHistory("x",0,"a")); Assert.Equal("v1",m.Get("x")!.Content); }
        [Fact] public void RestoreFromHistory_BadIndex() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); Assert.False(m.RestoreFromHistory("x",99,"a")); Assert.False(m.RestoreFromHistory("x",-1,"a")); }
        [Fact] public void RestoreFromHistory_NotFound() => Assert.False(new PromptPromotionManager().RestoreFromHistory("x",0,"a"));
        [Fact] public void RestoreFromHistory_ClearsApprovals() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Promote("x","a"); m.Approve("x","bob"); m.RestoreFromHistory("x",0,"a"); Assert.Empty(m.Get("x")!.CurrentApprovals); }

        [Fact] public void List_All() { var m=new PromptPromotionManager(); m.Register("b","c","a"); m.Register("a","c","a"); Assert.Equal(new[]{"a","b"},m.List()); }
        [Fact] public void List_ByStage() { var m=new PromptPromotionManager(); m.Register("draft1","c","a"); m.Register("staged","c","a"); m.Promote("staged","a"); Assert.Equal(new[]{"staged"},m.List(stage:PromptStage.Staging)); }
        [Fact] public void List_ByTag() { var m=new PromptPromotionManager(); m.Register("a","c","u","t1"); m.Register("b","c","u","t2"); Assert.Equal(new[]{"a"},m.List(tag:"t1")); }
        [Fact] public void List_Empty() => Assert.Empty(new PromptPromotionManager().List());

        [Fact] public void Remove_Draft() { var m=new PromptPromotionManager(); m.Register("x","c","a"); Assert.True(m.Remove("x")); Assert.Equal(0,m.Count); }
        [Fact] public void Remove_Deprecated() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Promote("x","a"); m.Promote("x","a"); Assert.True(m.Remove("x")); }
        [Fact] public void Remove_Staging_Blocked() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); Assert.False(m.Remove("x")); }
        [Fact] public void Remove_Production_Blocked() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.Promote("x","a"); m.Promote("x","a"); Assert.False(m.Remove("x")); }
        [Fact] public void Remove_NotFound() => Assert.False(new PromptPromotionManager().Remove("x"));

        [Fact] public void GenerateReport_Basic() { var m=new PromptPromotionManager(); m.Register("a","c","u"); m.Register("b","c","u"); m.Promote("b","u"); var r=m.GenerateReport(); Assert.Equal(2,r.TotalPrompts); Assert.Equal(1,r.ByStage[PromptStage.Draft]); Assert.Equal(1,r.ByStage[PromptStage.Staging]); Assert.Equal(1,r.TotalEvents); }
        [Fact] public void GenerateReport_PendingApproval() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Promote("x","a"); var r=m.GenerateReport(); Assert.Contains("x",r.PendingApproval); }
        [Fact] public void GenerateReport_Empty() { var r=new PromptPromotionManager().GenerateReport(); Assert.Equal(0,r.TotalPrompts); }

        [Fact] public void ExportJson_NotEmpty() { var m=new PromptPromotionManager(); m.Register("x","c","a"); var json=m.ExportJson(); Assert.Contains("\"x\"",json); Assert.Contains("Draft",json); }

        [Fact] public void ExportText_Format() { var m=new PromptPromotionManager(); m.Register("x","c","alice"); var txt=m.ExportText(); Assert.Contains("[Draft]",txt); Assert.Contains("x v1 by alice",txt); }
        [Fact] public void ExportText_ShowsApprovals() { var m=new PromptPromotionManager(); m.Register("x","c","a"); m.AddApprover("x","bob"); m.Promote("x","a"); var txt=m.ExportText(); Assert.Contains("Approvals: 0/1",txt); }

        [Fact] public void FullLifecycle() {
            var m=new PromptPromotionManager();
            m.Register("deploy","Summarize {{topic}}","alice","prod");
            m.AddApprover("deploy","bob");
            m.AddApprover("deploy","carol");

            // Draft → Staging
            var r1=m.Promote("deploy","alice");
            Assert.True(r1.Success);
            Assert.Equal(PromptStage.Staging,m.Get("deploy")!.Stage);

            // Staging → Production blocked (missing approvals)
            var r2=m.Promote("deploy","alice");
            Assert.False(r2.Success);
            Assert.Equal(2,r2.BlockReasons.Count);

            // Partial approval
            m.Approve("deploy","bob");
            var r3=m.Promote("deploy","alice");
            Assert.False(r3.Success);

            // Full approval
            m.Approve("deploy","carol");
            var r4=m.Promote("deploy","alice");
            Assert.True(r4.Success);
            Assert.Equal(PromptStage.Production,m.Get("deploy")!.Stage);

            // Rollback
            var r5=m.Rollback("deploy","alice","regression");
            Assert.True(r5.Success);
            Assert.Equal(PromptStage.Staging,m.Get("deploy")!.Stage);

            // History
            Assert.Equal(3,m.Get("deploy")!.History.Count);

            // Deprecate (promote back up then deprecate)
            m.Approve("deploy","bob"); m.Approve("deploy","carol");
            m.Promote("deploy","alice"); // staging → production
            m.Promote("deploy","alice"); // production → deprecated
            Assert.Equal(PromptStage.Deprecated,m.Get("deploy")!.Stage);
        }
    }
}
