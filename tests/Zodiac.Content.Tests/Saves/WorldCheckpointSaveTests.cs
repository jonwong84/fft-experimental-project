using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Zodiac.Framework.Campaign;
using Xunit;

namespace Zodiac.Content.Tests.Saves;

public sealed class WorldCheckpointSaveTests
{
    internal static byte[] Rewrite(byte[] bytes, Action<JsonObject> change, bool rehash = true)
    {
        var root = JsonNode.Parse(bytes)!.AsObject(); change(root);
        if (rehash) root["sha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(root["snapshot"]!.ToJsonString())));
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Fresh_definitions_restore_every_supported_field_and_identical_next_state_events(int checkpoint)
    {
        var source = SyntheticWorldCheckpoint.CreateProfile(); var state = SyntheticWorldCheckpoint.Start(source);
        if (checkpoint == 1) state = WorldCheckpointSimulator.Travel(state, SyntheticWorldCheckpoint.Hill).State;
        if (checkpoint == 2) state = source.Definition.Restore(source.Definition.RouteId, state.LocationId,
            state.Party.Select(p => p with { EquippedItemId = null }), [], WorldCheckpointDefinition.MaximumFunds,
            new(12,30), source.Definition.ProgressFlagId, false, WorldCheckpointDefinition.MaximumElapsedSeconds);
        byte[] bytes = WorldCheckpointSave.ToUtf8(source, state);
        var selected = SyntheticWorldCheckpoint.CreateProfile(); var loaded = WorldCheckpointSave.Read(selected, bytes);
        Assert.Same(selected.Definition, loaded.State.Definition); Assert.False(loaded.WasMigrated);
        Assert.Equal(bytes, WorldCheckpointSave.ToUtf8(selected, loaded.State));
        Assert.Equal(WorldCheckpointSave.Summarize(state), loaded.Summary);
        var left = WorldCheckpointSimulator.Travel(state, SyntheticWorldCheckpoint.Hill);
        var right = WorldCheckpointSimulator.Travel(loaded.State, SyntheticWorldCheckpoint.Hill);
        Assert.Equal(left.Rejection, right.Rejection); Assert.Equal(left.Events.ToArray(), right.Events.ToArray());
        Assert.Equal(WorldCheckpointSave.ToUtf8(source, left.State), WorldCheckpointSave.ToUtf8(selected, right.State));
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public void Independent_fixtures_migrate_without_rewriting_source_and_reproduce_authored_state(int version)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves", $"synthetic-world-idle-v{version}.json");
        byte[] bytes = File.ReadAllBytes(path); var profile = SyntheticWorldCheckpoint.CreateProfile();
        var loaded = WorldCheckpointSave.Read(profile, bytes);
        Assert.Equal(version, loaded.SourceSchemaVersion); Assert.Equal(version == 1, loaded.WasMigrated);
        Assert.Equal(WorldCheckpointSave.ToUtf8(profile, SyntheticWorldCheckpoint.Start(profile)), WorldCheckpointSave.ToUtf8(profile, loaded.State));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        var next = WorldCheckpointSimulator.Travel(loaded.State, SyntheticWorldCheckpoint.Hill);
        Assert.Equal(93, next.State.Funds); Assert.Equal(53, next.State.ElapsedSeconds); Assert.Single(next.Events);
    }
    [Theory]
    [InlineData("profileId","sample:profile/wrong")] [InlineData("routeId","sample:route/wrong")]
    [InlineData("locationId","sample:location/wrong")] [InlineData("progressFlagId","sample:flag/wrong")]
    [InlineData("checkpoint","battle")] [InlineData("rngPolicy","seed-on-load")]
    public void Rehashed_cross_field_and_capability_mismatches_reject(string field,string value)
    {
        var p=SyntheticWorldCheckpoint.CreateProfile(); byte[] bytes=Rewrite(WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(p)),
            root=>root["snapshot"]![field]=value);
        Assert.Throws<WorldSaveValidationException>(()=>WorldCheckpointSave.Read(p,bytes));
    }
    [Theory] [InlineData("profileVersion",2)] [InlineData("contentVersion",2)] [InlineData("funds",-1)] [InlineData("funds",1000001)]
    [InlineData("month",0)] [InlineData("month",13)] [InlineData("day",31)] [InlineData("elapsedSeconds",-1)] [InlineData("elapsedSeconds",315360001)]
    public void Rehashed_scalar_and_version_boundaries_reject(string field,int value)
    {
        var p=SyntheticWorldCheckpoint.CreateProfile(); var bytes=Rewrite(WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(p)),r=>r["snapshot"]![field]=value);
        Assert.Throws<WorldSaveValidationException>(()=>WorldCheckpointSave.Read(p,bytes));
    }
    [Theory] [InlineData("party-duplicate")] [InlineData("stack-duplicate")] [InlineData("unknown-job")] [InlineData("wrong-equipment")]
    [InlineData("owned-limit")] [InlineData("empty-party")] [InlineData("null-party")] [InlineData("summary")] [InlineData("rngState")]
    public void Rehashed_party_inventory_and_summary_mismatches_reject(string defect)
    {
        var p=SyntheticWorldCheckpoint.CreateProfile(); var bytes=Rewrite(WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(p)),root=>
        {
            var s=root["snapshot"]!;
            switch(defect)
            {
                case "party-duplicate": s["party"]![1]=s["party"]![0]!.DeepClone(); break;
                case "stack-duplicate": s["inventory"]!.AsArray().Add(s["inventory"]![0]!.DeepClone()); break;
                case "unknown-job": s["party"]![0]!["jobId"]="sample:job/wrong"; break;
                case "wrong-equipment": s["party"]![0]!["equippedItemId"]="sample:item/rations"; break;
                case "owned-limit": s["inventory"]![1]!["quantity"]=4; break;
                case "empty-party": s["party"]=new JsonArray(); break;
                case "null-party": s["party"]![0]=null; break;
                case "summary": s["summary"]!["funds"]=99; break;
                case "rngState": s["rngWord"]=1; break;
            }
        });
        Assert.Throws<WorldSaveValidationException>(()=>WorldCheckpointSave.Read(p,bytes));
    }
    public static IEnumerable<object[]> RequiredFields()
    {
        var p=SyntheticWorldCheckpoint.CreateProfile(); var root=JsonNode.Parse(WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(p)))!;
        foreach(var entry in root["snapshot"]!.AsObject()) yield return [entry.Key];
    }
    [Theory] [MemberData(nameof(RequiredFields))]
    public void Snapshot_fields_are_required_and_non_null(string field)
    {
        var p=SyntheticWorldCheckpoint.CreateProfile();byte[] original=WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(p));
        foreach(bool missing in new[]{true,false})
        {
            var bytes=Rewrite(original,r=>{if(missing)r["snapshot"]!.AsObject().Remove(field);else r["snapshot"]![field]=null;});
            Assert.Throws<WorldSaveValidationException>(()=>WorldCheckpointSave.Read(p,bytes));
        }
    }
    [Fact]
    public void Checksum_future_unknown_duplicate_malformed_and_oversized_inputs_reject()
    {
        var p=SyntheticWorldCheckpoint.CreateProfile();var original=WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(p));
        Assert.Throws<WorldSaveValidationException>(()=>WorldCheckpointSave.Read(p,Rewrite(original,r=>r["snapshot"]!["funds"]=99,false)));
        foreach(var bytes in new[]{Rewrite(original,r=>r["schemaVersion"]=3),Rewrite(original,r=>r["extra"]=1),
            Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"schemaVersion\":2}"),Encoding.UTF8.GetBytes("null"),Encoding.UTF8.GetBytes("[]"),
            Encoding.UTF8.GetBytes("{}"),new byte[]{0xff,0xfe},original[..^1],new byte[65537]})
            Assert.Throws<WorldSaveValidationException>(()=>WorldCheckpointSave.Read(p,bytes));
        var foreign=SyntheticWorldCheckpoint.CreateProfile();
        Assert.Throws<WorldSaveValidationException>(()=>WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(foreign)));
    }
    [Fact]
    public void Input_array_order_does_not_change_authored_party_order_or_inventory_canonicalization()
    {
        var p=SyntheticWorldCheckpoint.CreateProfile();var original=WorldCheckpointSave.ToUtf8(p,SyntheticWorldCheckpoint.Start(p));
        var bytes=Rewrite(original,r=>
        {
            foreach(string field in new[]{"party","inventory"})
            {var values=r["snapshot"]![field]!.AsArray().Select(n=>n!.DeepClone()).Reverse().ToArray();r["snapshot"]![field]=new JsonArray(values);}
        });
        Assert.Equal(original,WorldCheckpointSave.ToUtf8(p,WorldCheckpointSave.Read(p,bytes).State));
    }
}
