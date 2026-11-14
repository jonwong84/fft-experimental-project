using System.Text.Json;
using Xunit;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class FlatRosterDirectTrajectoryTests
{
    [Fact]
    public void Existing_fourteen_between_unit_and_thirteen_live_target_tapes_keep_exact_samples()
    {
        foreach(var file in new[]{"roster-trajectory-contract-v1.json","roster-live-stone-contract-v1.json"})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",file)));int count=0;
            foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
            {
                var d=row.GetProperty("input");bool live=file.Contains("live-stone");var e=live ? row.GetProperty("expected").GetProperty("path") : row.GetProperty("expected");
                var raw=d.GetProperty("units").EnumerateArray().ToArray();var s=Make(raw.Select(u=>new Spec(u.GetProperty("id").GetString()!,Tile(u.GetProperty("tile")),u.GetProperty("team").GetInt32())).ToArray(),width:8,depth:4);
                var heights=new FlatRosterProjectileHeights(raw.Select(u=>new FlatRosterSpriteHeight(new(u.GetProperty("id").GetString()!),u.GetProperty("height").GetByte())));
                string actor=d.GetProperty("actor").GetString()!,target=d.GetProperty("target").GetString()!;
                var origin=live ? Tile(raw.Single(u=>u.GetProperty("id").GetString()==actor).GetProperty("tile")) : Tile(d.GetProperty("origin"));
                var request=new FlatRosterDirectTrajectoryRequest(new(actor),new(target),origin);var before=Snapshot(s.State);
                var r=live ? FlatRosterDirectTrajectory.EvaluateToTarget(s.State,request,s.Profile,heights) : FlatRosterDirectTrajectory.EvaluateBetweenUnits(s.State,request,s.Profile,heights);
                Assert.True(r.Accepted);var o=r.Outcome!;Assert.Equal(e.GetProperty("clear").GetBoolean(),o.Clear);Assert.Equal(e.GetProperty("blocker").GetString(),o.BlockingUnitId?.Value);
                Assert.Equal(e.GetProperty("sample").ValueKind==JsonValueKind.Null ? (int?)null : e.GetProperty("sample").GetInt32(),o.BlockingSample);
                Assert.Equal(e.GetProperty("samples").GetInt32(),o.TotalSamples);Assert.Equal(e.GetProperty("distance").GetInt32(),o.DistanceQ12);
                var p=e.GetProperty("point");Assert.Equal(p.ValueKind==JsonValueKind.Null ? (FlatProjectilePoint?)null : new(p[0].GetInt32(),p[1].GetInt32(),p[2].GetInt32()),o.BlockingPoint);
                Assert.Equal(before,Snapshot(s.State));count++;
            }
            Assert.Equal(file.Contains("live-stone") ? 13 : 14,count);
        }
    }

    [Fact]
    public void Live_target_box_is_a_successful_resolved_target_while_between_units_excludes_it()
    {
        var s=Make([new("a",new(1,1,0)),new("b",new(3,1,0),1),new("c",new(1,2,0))]);var h=new FlatRosterProjectileHeights([new(A,30),new(B,30),new(C,30)]);
        var request=new FlatRosterDirectTrajectoryRequest(A,B,new(1,1,0));var ai=FlatRosterDirectTrajectory.EvaluateBetweenUnits(s.State,request,s.Profile,h);
        var live=FlatRosterDirectTrajectory.EvaluateToTarget(s.State,request,s.Profile,h);Assert.True(ai.Outcome!.Clear);Assert.False(live.Outcome!.Clear);
        Assert.Equal(B,live.Outcome.BlockingUnitId);Assert.Equal(B,live.Outcome.ResolveTarget(B));
    }

    [Fact]
    public void Geometry_origin_grants_no_movement_permission_and_needs_exact_copied_height_membership()
    {
        var s=Make([new("a",new(1,1,0)),new("b",new(5,1,0),1),new("c",new(1,2,0))],move:0);var origin=new TilePosition(3,1,0);
        FlatRosterSpriteHeight[] values=[new(A,30),new(B,30),new(C,30)];var h=new FlatRosterProjectileHeights(values);values[2]=new(C,0);Assert.Equal(30,h.Units[C]);
        Assert.True(FlatRosterDirectTrajectory.EvaluateBetweenUnits(s.State,new(A,B,origin),s.Profile,h).Accepted);
        Assert.DoesNotContain(origin,FlatRosterMovement.Evaluate(s.State,A,s.Profile).Outcome!.AiOrigins);
        var missing=FlatRosterDirectTrajectory.EvaluateBetweenUnits(s.State,new(A,B,origin),s.Profile,new([new(A,30),new(B,30)]));
        Assert.False(missing.Accepted);Assert.Equal(FlatRosterDirectTrajectoryRejection.MissingSpriteHeights,missing.Rejection);
        Assert.Throws<ArgumentException>(()=>new FlatRosterProjectileHeights([new(A,30),new(A,0)]));
    }

    [Fact]
    public void Runtime_slot21_extent_is_distinct_from_AI_compact16_and_uncapped_metadata_storage()
    {
        foreach(int count in new[]{21,22})
        {
            var specs=Enumerable.Range(0,count).Select(i=>new Spec(i==0 ? "a" : i==1 ? "b" : $"u-{i}",new(i%8,i/8,0),i%4)).ToArray();
            var s=Make(specs,width:8,depth:4);var h=new FlatRosterProjectileHeights(specs.Select(u=>new FlatRosterSpriteHeight(new(u.Id),30)));
            Assert.Equal(count,h.Units.Count);var r=FlatRosterDirectTrajectory.EvaluateToTarget(s.State,new(A,B,new(0,0,0)),s.Profile,h);Assert.Equal(count==21,r.Accepted);
        }
    }

    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
}
