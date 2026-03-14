namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;
    using static Prompt.PromptGoldenTester;

    public class PromptGoldenTesterTests
    {
        [Fact] public void Ctor_Valid() { var t = new PromptGoldenTester("s"); Assert.Equal("s", t.Name); Assert.Equal(0, t.Count); }
        [Theory][InlineData(null)][InlineData("")][InlineData("  ")]
        public void Ctor_Empty_Throws(string n) => Assert.Throws<ArgumentException>(() => new PromptGoldenTester(n));
        [Fact] public void Record_Adds() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","in","out"); Assert.Equal(1,t.Count); var e=t.GetEntry("c"); Assert.Equal("in",e!.Input); Assert.Equal("out",e.GoldenOutput); Assert.Equal(1,e.Version); }
        [Fact] public void Record_Tags() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","i","o","a","b"); Assert.Contains("a",t.GetEntry("c")!.Tags); }
        [Fact] public void Record_Overwrites() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","o","o"); t.RecordGolden("c","n","n"); Assert.Equal(1,t.Count); Assert.Equal("n",t.GetEntry("c")!.GoldenOutput); }
        [Theory][InlineData("","i","o")][InlineData("id","","o")][InlineData("id","i","")]
        public void Record_Empty_Throws(string id,string i,string o) => Assert.Throws<ArgumentException>(()=>new PromptGoldenTester("t").RecordGolden(id,i,o));
        [Fact] public void Remove_Exists() { var t=new PromptGoldenTester("t"); t.RecordGolden("a","i","o"); Assert.True(t.RemoveGolden("a")); Assert.Equal(0,t.Count); }
        [Fact] public void Remove_Missing() => Assert.False(new PromptGoldenTester("t").RemoveGolden("x"));
        [Fact] public void List_Sorted() { var t=new PromptGoldenTester("t"); t.RecordGolden("z","i","o"); t.RecordGolden("a","i","o"); t.RecordGolden("m","i","o"); Assert.Equal(new[]{"a","m","z"},t.ListIds()); }
        [Fact] public void List_Tag() { var t=new PromptGoldenTester("t"); t.RecordGolden("a","i","o","x"); t.RecordGolden("b","i","o","y"); t.RecordGolden("c","i","o","x"); Assert.Equal(new[]{"a","c"},t.ListIds("x")); }
        [Fact] public void List_Empty() => Assert.Empty(new PromptGoldenTester("t").ListIds());
        [Fact] public void Compare_Match() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","p","The answer is 42."); var r=t.Compare("c","The answer is 42."); Assert.Equal(GoldenStatus.Match,r.Status); Assert.Equal(1.0,r.SimilarityScore,3); }
        [Fact] public void Compare_Drift() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","p","The quick brown fox jumps over the lazy dog today"); var r=t.Compare("c","The quick brown fox leaps over the lazy dog today"); Assert.Equal(GoldenStatus.Drift,r.Status); Assert.True(r.SimilarityScore>0.7&&r.SimilarityScore<0.95); }
        [Fact] public void Compare_Regression() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","p","The sun rises in the east and sets in the west"); var r=t.Compare("c","Banana milkshake with chocolate sprinkles please"); Assert.Equal(GoldenStatus.Regression,r.Status); }
        [Fact] public void Compare_Missing() => Assert.Throws<KeyNotFoundException>(()=>new PromptGoldenTester("t").Compare("x","a"));
        [Fact] public void Compare_Null() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","i","o"); Assert.Throws<ArgumentNullException>(()=>t.Compare("c",null!)); }
        [Fact] public void Compare_Updates() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","i","g"); t.Compare("c","a"); Assert.Equal("a",t.GetEntry("c")!.LastActual); }
        [Fact] public void Compare_Diffs() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","i","hello world foo"); Assert.NotEmpty(t.Compare("c","hello world bar").Diffs); }
        [Fact] public void Approve_OK() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","i","old"); t.Compare("c","new"); Assert.True(t.ApproveActual("c")); Assert.Equal("new",t.GetEntry("c")!.GoldenOutput); Assert.Equal(2,t.GetEntry("c")!.Version); }
        [Fact] public void Approve_NoLast() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","i","o"); Assert.False(t.ApproveActual("c")); }
        [Fact] public void Approve_Missing() => Assert.False(new PromptGoldenTester("t").ApproveActual("x"));
        [Fact] public void Approve_VersionInc() { var t=new PromptGoldenTester("t"); t.RecordGolden("c","i","v1"); t.Compare("c","v2"); t.ApproveActual("c"); Assert.Equal(2,t.GetEntry("c")!.Version); t.Compare("c","v3"); t.ApproveActual("c"); Assert.Equal(3,t.GetEntry("c")!.Version); }
        [Fact] public void Batch_AllMatch() { var t=new PromptGoldenTester("t"); t.RecordGolden("a","pa","ra"); t.RecordGolden("b","pb","rb"); var r=t.RunBatch(i=>i=="pa"?"ra":"rb"); Assert.Equal(2,r.MatchCount); Assert.Equal(1.0,r.AverageSimilarity,3); }
        [Fact] public void Batch_Error() { var t=new PromptGoldenTester("t"); t.RecordGolden("a","p","r"); var r=t.RunBatch(_=>throw new InvalidOperationException("boom")); Assert.Equal(1,r.ErrorCount); Assert.Contains("boom",r.Results[0].ActualOutput); }
        [Fact] public void Batch_Tag() { var t=new PromptGoldenTester("t"); t.RecordGolden("a","ia","oa","s"); t.RecordGolden("b","ib","ob","f"); t.RecordGolden("c","ic","oc","s"); var r=t.RunBatch(i=>i switch{"ia"=>"oa","ic"=>"oc",_=>"?"},tag:"s"); Assert.Equal(2,r.MatchCount); }
        [Fact] public void Batch_NullFunc() => Assert.Throws<ArgumentNullException>(()=>new PromptGoldenTester("t").RunBatch(null!));
        [Fact] public void Batch_Empty() => Assert.Equal(0,new PromptGoldenTester("t").RunBatch(_=>"x").TotalCount);
        [Fact] public void Batch_Mixed() { var t=new PromptGoldenTester("t"); t.RecordGolden("m","ia","exact match output here"); t.RecordGolden("r","ib","completely original golden text here"); var rpt=t.RunBatch(i=>i=="ia"?"exact match output here":"zzzzz"); Assert.True(rpt.MatchCount>=1); }
        [Fact] public void Threshold_Match() { var t=new PromptGoldenTester("t").WithMatchThreshold(0.80); t.RecordGolden("c","i","The quick brown fox jumps over the lazy dog today"); Assert.Equal(GoldenStatus.Match,t.Compare("c","The quick brown fox leaps over the lazy dog today").Status); }
        [Theory][InlineData(0.0)][InlineData(-0.1)][InlineData(1.1)]
        public void Threshold_Bad(double v) => Assert.Throws<ArgumentOutOfRangeException>(()=>new PromptGoldenTester("t").WithMatchThreshold(v));
        [Fact] public void Export_RoundTrip() { var t=new PromptGoldenTester("s"); t.RecordGolden("a","ia","oa"); t.RecordGolden("b","ib","ob"); var j=t.ExportJson(); var t2=new PromptGoldenTester("x"); Assert.Equal(2,t2.ImportJson(j)); Assert.Equal("oa",t2.GetEntry("a")!.GoldenOutput); }
        [Fact] public void Import_Empty() => Assert.Throws<ArgumentException>(()=>new PromptGoldenTester("t").ImportJson(""));
        [Fact] public void Import_Bad() => Assert.Throws<ArgumentException>(()=>new PromptGoldenTester("t").ImportJson("{bad}}}"));
        [Fact] public void Import_Overwrites() { var t=new PromptGoldenTester("t"); t.RecordGolden("a","oi","oo"); var s=new PromptGoldenTester("s"); s.RecordGolden("a","ni","no"); t.ImportJson(s.ExportJson()); Assert.Equal("no",t.GetEntry("a")!.GoldenOutput); }
        [Fact] public void Report_Content() { var t=new PromptGoldenTester("R"); t.RecordGolden("p","i","out"); var txt=FormatReport(t.RunBatch(_=>"out")); Assert.Contains("R",txt); Assert.Contains("Total:",txt); }
        [Fact] public void Sim_Same() => Assert.Equal(1.0,ComputeSimilarity("hello","hello"),3);
        [Fact] public void Sim_Empty() => Assert.Equal(0.0,ComputeSimilarity("hello",""),3);
        [Fact] public void Sim_Symmetric() => Assert.Equal(ComputeSimilarity("hello world","world hello"),ComputeSimilarity("world hello","hello world"),10);
        [Fact] public void Diff_None() => Assert.Empty(ComputeDiffs("hello world","hello world"));
        [Fact] public void Diff_Added() => Assert.Contains(ComputeDiffs("hello","hello world"),d=>d.Type==DiffType.Added);
        [Fact] public void Diff_Removed() => Assert.Contains(ComputeDiffs("hello world","hello"),d=>d.Type==DiffType.Removed);
        [Fact] public void GetEntry_Missing() => Assert.Null(new PromptGoldenTester("t").GetEntry("x"));
    }
}
