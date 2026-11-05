using Zodiac.Framework.Campaign;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class WorldCheckpointTests
{
    private static AssetId Id(string id) => new("sample:" + id);
    private static WorldCheckpointDefinition Definition() => new(Id("route/idle"), Id("flag/done"), [Id("unit/a")],
        [new(Id("job/courier"), [Id("item/blade")])], [new(Id("item/blade"), true, 2)], [Id("location/a"), Id("location/b")]);
    private static WorldCheckpointState State(WorldCheckpointDefinition d, int funds = 100, long seconds = 42) => d.Restore(d.RouteId, Id("location/a"),
        [new(Id("unit/a"), Id("job/courier"), Id("item/blade"))], [new(Id("item/blade"), 1)], funds, new(3, 14), d.ProgressFlagId, true, seconds);
    [Fact]
    public void Authored_travel_has_exact_cost_duration_and_one_event_without_mutating_input()
    {
        var before = State(Definition()); var result = WorldCheckpointSimulator.Travel(before, Id("location/b"));
        Assert.True(result.Accepted); Assert.Equal(93, result.State.Funds); Assert.Equal(53, result.State.ElapsedSeconds);
        Assert.Equal(new WorldTravelEvent(Id("location/a"), Id("location/b"), 7, 11), Assert.Single(result.Events));
        Assert.Equal(before.Party.ToArray(), result.State.Party.ToArray()); Assert.Equal(before.Inventory.ToArray(), result.State.Inventory.ToArray());
        Assert.Equal(before.Date, result.State.Date); Assert.True(result.State.Progress); Assert.Equal(100, before.Funds); Assert.Equal(42, before.ElapsedSeconds);
    }
    [Theory] [InlineData("location/missing", 100, 42, WorldTravelRejection.UnknownLocation)]
    [InlineData("location/a", 0, 315360000, WorldTravelRejection.AlreadyThere)]
    [InlineData("location/b", 6, 315360000, WorldTravelRejection.InsufficientFunds)]
    [InlineData("location/b", 7, 315359990, WorldTravelRejection.ElapsedLimit)]
    public void Travel_rejection_precedence_preserves_exact_state_and_no_events(string destination, int funds, long seconds, WorldTravelRejection expected)
    {
        var state = State(Definition(), funds, seconds); var result = WorldCheckpointSimulator.Travel(state, Id(destination));
        Assert.Equal(expected, result.Rejection); Assert.Same(state, result.State); Assert.Empty(result.Events);
    }
    [Fact]
    public void Exact_funds_and_elapsed_limits_are_accepted()
    {
        var state = State(Definition(), 7, WorldCheckpointDefinition.MaximumElapsedSeconds - 11);
        var result = WorldCheckpointSimulator.Travel(state, Id("location/b")); Assert.True(result.Accepted);
        Assert.Equal(0, result.State.Funds); Assert.Equal(WorldCheckpointDefinition.MaximumElapsedSeconds, result.State.ElapsedSeconds);
    }
    [Theory] [InlineData(-1,1,1,0)] [InlineData(1000001,1,1,0)] [InlineData(0,0,1,0)] [InlineData(0,13,1,0)]
    [InlineData(0,1,0,0)] [InlineData(0,1,31,0)] [InlineData(0,1,1,-1)] [InlineData(0,1,1,315360001)]
    public void Invalid_scalars_reject_before_state_publication(int funds,int month,int day,long seconds)
    {
        var d=Definition(); var s=State(d);
        Assert.Throws<ArgumentException>(() => d.Restore(d.RouteId,s.LocationId,s.Party,s.Inventory,funds,new(month,day),d.ProgressFlagId,true,seconds));
    }
    [Theory] [InlineData("route")] [InlineData("location")] [InlineData("flag")] [InlineData("unit")] [InlineData("job")] [InlineData("equipment")] [InlineData("inventory")] [InlineData("duplicate-party")] [InlineData("duplicate-stack")] [InlineData("owned-limit")]
    public void Cross_field_bindings_and_combined_ownership_are_validated(string defect)
    {
        var d=Definition();var s=State(d);
        var members=s.Party.ToArray(); var stacks=s.Inventory.ToArray();
        if(defect=="unit")members[0]=members[0] with{Id=Id("unit/unknown")};
        if(defect=="job")members[0]=members[0] with{JobId=Id("job/unknown")};
        if(defect=="equipment")members[0]=members[0] with{EquippedItemId=Id("item/unknown")};
        if(defect=="inventory")stacks=[new(Id("item/unknown"),1)];
        if(defect=="duplicate-party")members=[members[0],members[0]];
        if(defect=="duplicate-stack")stacks=[stacks[0],stacks[0]];
        if(defect=="owned-limit")stacks=[new(Id("item/blade"),2)];
        Assert.Throws<ArgumentException>(()=>d.Restore(defect=="route"?Id("route/wrong"):d.RouteId,
            defect=="location"?Id("location/wrong"):s.LocationId,members,stacks,s.Funds,s.Date,
            defect=="flag"?Id("flag/wrong"):d.ProgressFlagId,s.Progress,s.ElapsedSeconds));
    }
    [Fact]
    public void Definition_collections_are_bounded_distinct_and_resolve_equipment()
    {
        Assert.Throws<ArgumentException>(()=>new WorldCheckpointDefinition(Id("route/idle"),Id("flag/done"),Enumerable.Repeat(Id("unit/a"),65),
            [new(Id("job/courier"),[])],[new(Id("item/blade"),true,2)],[Id("location/a")]));
        Assert.Throws<ArgumentException>(()=>new WorldCheckpointDefinition(Id("route/idle"),Id("flag/done"),[Id("unit/a")],
            [new(Id("job/courier"),[Id("item/blade")])],[new(Id("item/blade"),false,2)],[Id("location/a")]));
        Assert.Throws<ArgumentException>(()=>new WorldCheckpointDefinition(Id("route/idle"),Id("flag/done"),[Id("unit/a")],
            [new(Id("job/courier"),[])],[new(Id("item/blade"),true,2)],[Id("unit/a")]));
    }
}
