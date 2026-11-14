using System.Text.Json;
using Xunit;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterAiDirectTrajectoryTests
{
    private static readonly TilePosition Origin = new(1,1,0), Target = new(5,1,0), Blocker = new(3,1,0);
    private static Setup Sample(int team=1,bool reverse=false) => Make([new("a",2,1,15,Move:4),new("b",1,0,95),new("c",0,team)],
        positions:[Origin,Target,Blocker],reverse:reverse);
    private static RosterAiProjectileHeights Heights(byte third=30) => new([new(A,30),new(B,30),new(C,third)]);
    private static RosterAiDirectTrajectoryResult Query(Setup s,RosterAiProjectileHeights? heights=null,RosterAiDirectTrajectoryRequest? request=null) =>
        RosterAiDirectTrajectory.Evaluate(s.State,request ?? new(A,B,Origin),s.Simulation,heights ?? Heights());

    [Fact]
    public void Fourteen_source_tapes_cover_exact_sdk_samples_all_occupants_and_strict_collision_bounds()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-trajectory-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var raw=d.GetProperty("units").EnumerateArray().ToArray();
            string actor=d.GetProperty("actor").GetString()!,target=d.GetProperty("target").GetString()!;
            var units=raw.Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),
                u.GetProperty("id").GetString()==actor ? 15 : 95,Move:4)).ToArray();
            var s=Make(units,actor,positions:raw.Select(u=>Tile(u.GetProperty("tile"))).ToArray());
            var heights=new RosterAiProjectileHeights(raw.Select(u=>new RosterAiSpriteHeight(new(u.GetProperty("id").GetString()!),u.GetProperty("height").GetByte())));
            var before=Snapshot(s.State);var r=Query(s,heights,new(new(actor),new(target),Tile(d.GetProperty("origin"))));
            Assert.True(r.Accepted);Assert.Equal(RosterAiDirectTrajectoryRejection.None,r.Rejection);var o=r.Outcome!;
            Assert.Equal(e.GetProperty("clear").GetBoolean(),o.Clear);Assert.Equal(e.GetProperty("blocker").GetString(),o.BlockingUnitId?.Value);
            Assert.Equal(e.GetProperty("sample").ValueKind==JsonValueKind.Null ? (int?)null : e.GetProperty("sample").GetInt32(),o.BlockingSample);
            Assert.Equal(e.GetProperty("samples").GetInt32(),o.TotalSamples);Assert.Equal(e.GetProperty("distance").GetInt32(),o.DistanceQ12);
            var point=e.GetProperty("point");Assert.Equal(point.ValueKind==JsonValueKind.Null ? (AiProjectilePoint?)null : new(point[0].GetInt32(),point[1].GetInt32(),point[2].GetInt32()),o.BlockingPoint);
            Assert.Equal(before,Snapshot(s.State));count++;
        }
        Assert.Equal(14,count);
    }

    [Fact]
    public void Every_other_team_blocks_while_equal_height_box_top_and_zero_height_do_not()
    {
        foreach(int team in new[]{0,1,2,3})
        {
            var s=Sample(team);var r=Query(s);Assert.True(r.Accepted);Assert.False(r.Outcome!.Clear);Assert.Equal(C,r.Outcome.BlockingUnitId);
            Assert.True(Query(s,Heights(0)).Outcome!.Clear);Assert.True(Query(s,Heights(20)).Outcome!.Clear);Assert.False(Query(s,Heights(21)).Outcome!.Clear);
        }
    }

    [Fact]
    public void Height_bindings_copy_storage_and_require_exact_complete_known_membership()
    {
        var s=Sample();RosterAiSpriteHeight[] raw=[new(A,30),new(B,30),new(C,30)];var heights=new RosterAiProjectileHeights(raw);raw[2]=new(C,0);
        Assert.False(Query(s,heights).Outcome!.Clear);
        foreach(var wrong in new[]{new RosterAiProjectileHeights([new(A,30),new(B,30)]),new([new(A,30),new(B,30),new(new("absent"),30)]),
            new([new(A,30),new(B,30),new(C,30),new(new("extra"),30)])})Rejected(Query(s,wrong),RosterAiDirectTrajectoryRejection.MissingSpriteHeights);
        Assert.Throws<ArgumentException>(()=>new RosterAiProjectileHeights([new(A,30),new(A,0)]));
        Assert.Throws<ArgumentException>(()=>new RosterAiProjectileHeights([new(new BattleUnitId(""),30)]));
    }

    [Fact]
    public void Actor_target_initial_legal_origin_and_range_are_admitted_before_sampling()
    {
        var s=Sample();
        foreach(var row in new[]{
            (new RosterAiDirectTrajectoryRequest(new("absent"),B,Origin),RosterAiDirectTrajectoryRejection.UnknownActor),
            (new RosterAiDirectTrajectoryRequest(A,new("absent"),Origin),RosterAiDirectTrajectoryRejection.UnknownTarget),
            (new RosterAiDirectTrajectoryRequest(A,A,Origin),RosterAiDirectTrajectoryRejection.InvalidTarget),
            (new RosterAiDirectTrajectoryRequest(A,B,Target),RosterAiDirectTrajectoryRejection.InvalidOrigin),
            (new RosterAiDirectTrajectoryRequest(A,B,Blocker),RosterAiDirectTrajectoryRejection.InvalidOrigin),
            (new RosterAiDirectTrajectoryRequest(A,B,new(0,1,0)),RosterAiDirectTrajectoryRejection.OutOfRange),
            (new RosterAiDirectTrajectoryRequest(A,B,new(1,1,1)),RosterAiDirectTrajectoryRejection.InvalidOrigin)})Rejected(Query(s,request:row.Item1),row.Item2);
        var missingContext=new RosterAiSimulationProfile(s.Simulation.Battle,s.Simulation.Units);
        Rejected(RosterAiDirectTrajectory.Evaluate(s.State,new(A,B,Origin),missingContext,Heights()),RosterAiDirectTrajectoryRejection.UnsupportedProfile);
    }

    [Fact]
    public void Reordered_source_storage_and_repeated_queries_leave_state_and_rng_untouched()
    {
        var a=Sample();var b=Sample(reverse:true);var before=Snapshot(a.State);
        Assert.Equal(Query(a),Query(b));Assert.Equal(Query(a),Query(a));Assert.Equal(before,Snapshot(a.State));Assert.Equal(before,Snapshot(b.State));
        Assert.Equal(Rng,a.State.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Profile_storage_is_uncapped_while_computed_ai_query_keeps_compact_extent()
    {
        foreach(int n in new[]{16,17})
        {
            var units=Enumerable.Range(0,n).Select(i=>new Unit(i==0 ? "a" : i==1 ? "b" : $"u-{i}",i,i%4,i==0 ? 15 : 95)).ToArray();
            var positions=Enumerable.Range(0,n).Select(i=>new TilePosition(i%8,i/8,0)).ToArray();var s=Make(units,positions:positions);
            var heights=new RosterAiProjectileHeights(units.Select(u=>new RosterAiSpriteHeight(new(u.Id),255)));Assert.Equal(n,heights.Units.Count);
            var r=Query(s,heights,new(A,B,positions[0]));Assert.Equal(n==16,r.Accepted);
        }
    }

    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static void Rejected(RosterAiDirectTrajectoryResult result,RosterAiDirectTrajectoryRejection reason)
    {Assert.False(result.Accepted);Assert.Equal(reason,result.Rejection);Assert.Null(result.Outcome);}
}
