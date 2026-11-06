using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Zodiac.AssetTool.Cli;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;
using Zodiac.Framework.Ai;
using Zodiac.Framework.Random;
using Zodiac.Framework.Formulas;
using Zodiac.Framework.CombatRules;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class TargetedAbilityImportTests
{
    private static int Offset(uint address) => checked((int)(address - 0x80010000 + 0x800));
    private static byte[] Image()
    {
        var image = new byte[OriginalMetadataImporter.ExecutableLength]; "PS-X EXE"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18), 0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), (uint)image.Length - 0x800);
        int job = Offset(0x800610b8 + 74 * 48); image[job] = 5; image[job + 0x17] = 1;
        int slots = Offset(0x80064a94 + 5 * 25); image[slots + 3] = 146; image[slots + 4] = 147; image[slots + 5] = 148;
        new byte[] { 0,0,0,1,0,0,0,0 }.CopyTo(image, Offset(0x8005ebf0 + 146 * 8));
        new byte[] { 80,0,90,65,130,2,1,129 }.CopyTo(image, Offset(0x8005ebf0 + 147 * 8));
        new byte[] { 90,0,80,65,130,2,65,129 }.CopyTo(image, Offset(0x8005ebf0 + 148 * 8));
        new byte[] { 1,0,1,9,17,0,0,0,55,0,4,0,0,0 }.CopyTo(image, Offset(0x8005fbf0 + 147 * 14));
        new byte[] { 4,0,0,1,17,0,34,0,55,0,2,0,0,0 }.CopyTo(image, Offset(0x8005fbf0 + 148 * 14));
        return image;
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public void Dash_import_validates_every_secondary_field(int field)
    {
        var image = Image(); var metadata = OriginalMetadataImporter.DecodeSelection(image).Package;
        image[Offset(0x8005fbf0 + 147 * 14) + field] ^= 0xff;
        Assert.Throws<InvalidDataException>(() => OriginalTargetedAbilityImporter.DecodeSelection(image, metadata, TargetedAbilityEffect.DashFlat));
    }

    [Fact]
    public void Shared_import_preserves_Stone_output_and_Dash_provenance_without_certifying_synthetic_input()
    {
        var image = Image(); var metadata = OriginalMetadataImporter.DecodeSelection(image).Package; string before = metadata.ToJson();
        var dash = OriginalTargetedAbilityImporter.DecodeSelection(image, metadata, TargetedAbilityEffect.DashFlat);
        var stone = OriginalTargetedAbilityImporter.DecodeSelection(image, metadata, TargetedAbilityEffect.ThrowStoneFlat);
        Assert.Equal(OriginalThrowStoneImporter.ToJson(OriginalThrowStoneImporter.DecodeSelection(image, metadata)), OriginalTargetedAbilityImporter.ToJson(stone));
        Assert.Equal(before, metadata.ToJson()); Assert.Equal(6, dash.Records.Length);
        foreach (var row in dash.Records) Assert.Equal(Convert.ToHexStringLower(image.AsSpan(row.FileOffset, row.ByteLength)), row.RawHex);
        Assert.Null(dash.VerifiedInputSha256); Assert.Throws<InvalidDataException>(() => OriginalTargetedAbilityImporter.ImportVerified(image, metadata, TargetedAbilityEffect.DashFlat));
        Assert.Equal(dash.Package.ToJson(), TargetedAbilityContentPackage.ReadJson(dash.Package.ToJson()).ToJson());
        Assert.Equal(TargetedAbilityEffect.DashFlat, Assert.Single(dash.Package.Abilities).Effect);
        Assert.Equal(80, metadata.Abilities.Single(a => a.Id == dash.Package.Abilities[0].Id).JpCost);
        string provenance = OriginalTargetedAbilityImporter.ToJson(dash); Array.Clear(image);
        Assert.Equal(provenance, OriginalTargetedAbilityImporter.ToJson(dash));
    }

    [Theory] [InlineData("slot")] [InlineData("slot-high")] [InlineData("menu")] [InlineData("status")]
    [InlineData("metadata")] [InlineData("primary")]
    public void Dash_import_rejects_inconsistent_source_or_metadata(string kind)
    {
        var image = Image(); var metadata = OriginalMetadataImporter.DecodeSelection(image).Package;
        if (kind == "slot") image[Offset(0x80064a94 + 5 * 25) + 4] = 148;
        if (kind == "slot-high") image[Offset(0x80064a94 + 5 * 25)] |= 0x40;
        if (kind == "menu") image[Offset(0x80065cb4 + 5)] = 1;
        if (kind == "status") image[Offset(0x80063fc4)] = 1;
        if (kind == "primary") image[Offset(0x8005ebf0 + 147 * 8)]++;
        if (kind == "metadata")
        {
            var json = JsonNode.Parse(metadata.ToJson())!; json["jobs"]![0]!["baseMove"] = 2;
            metadata = ContentPackage.ReadJson(json.ToJsonString());
        }
        Assert.Throws<InvalidDataException>(() => OriginalTargetedAbilityImporter.DecodeSelection(image, metadata, TargetedAbilityEffect.DashFlat));
    }

    [Theory] [InlineData("missing")] [InlineData("hash")] [InlineData("existing")]
    public void Dash_command_rejects_missing_metadata_and_invalid_input_without_publishing(string kind)
    {
        string workspace = Path.Combine(Path.GetTempPath(),"fft-dash-import-"+Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace,"scratch"); Directory.CreateDirectory(scratch);
        string exe = Path.Combine(scratch,"synthetic.exe"), metadata = Path.Combine(scratch,"metadata.json"), destination = Path.Combine(scratch,"output");
        File.WriteAllBytes(exe,Image()); File.WriteAllText(metadata,OriginalMetadataImporter.DecodeSelection(Image()).Package.ToJson());
        if (kind == "existing") Directory.CreateDirectory(destination);
        try
        {
            var args = new List<string> { "import-dash","--exe",exe,"--output",destination };
            if (kind != "missing") args.AddRange(["--metadata",metadata]);
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.NotEqual(0,new ImportMetadataCommand(workspace).Run(args.ToArray(),output,error));
            Assert.Empty(output.ToString()); Assert.NotEmpty(error.ToString());
            Assert.Equal(kind == "existing",Directory.Exists(destination)); Assert.Equal(2,Directory.GetFiles(scratch).Length);
        }
        finally
        {
            File.Delete(exe); File.Delete(metadata); if (Directory.Exists(destination)) Directory.Delete(destination);
            Directory.Delete(scratch); Directory.Delete(workspace);
        }
    }

    [Theory] [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void Imported_Dash_lists_and_explicit_grants_reach_both_actual_turn_orders(bool moveFirst,bool customFormula)
    {
        var image = Image(); var metadata = OriginalMetadataImporter.DecodeSelection(image).Package;
        var dash = OriginalTargetedAbilityImporter.DecodeSelection(image, metadata, TargetedAbilityEffect.DashFlat).Package;
        if(customFormula)
        {
            var edited=new TargetedAbilityContentPackage(dash.PackageId,dash.ProfileId,dash.RequiredMetadataPackageId,
                [new(dash.Abilities[0].Id,TargetedAbilityEffect.DashFlat,new PhysicalDamageFormula(PhysicalDamageFormulaId.RandomPaPlusHalfPa,9,0))]);
            dash=TargetedAbilityContentPackage.ReadJson(edited.ToJson());
            Assert.Equal(2,dash.Version);Assert.Contains("\"ruleset\": \"custom\"",edited.ToJson());
        }
        var stone = OriginalTargetedAbilityImporter.DecodeSelection(image, metadata, TargetedAbilityEffect.ThrowStoneFlat).Package;
        var self = new ImmediateAbilityContentPackage("fft.ps1:package/self", metadata.ProfileId, metadata.PackageId,
            [new(new("fft.ps1:ability/accumulate"), ImmediateAbilityEffect.SelfPhysicalAttackIncrease, 1,0,0)]);
        var actor = new BattleUnitId("test:actor"); var enemy = new BattleUnitId("test:enemy"); var ally = new BattleUnitId("test:ally");
        var ids = new[] { actor, enemy, ally }; var ground = new TerrainSurfaceId("test:ground"); var skill = metadata.Skillsets[0].Id;
        var map = new BattleMapDefinition(8,4,1,Enumerable.Repeat(new BattleTile(ground,10),32).ToArray());
        var rng = new RandomState(unchecked((1u - 12345u) * 4005161829u),6);
        var positions = new TilePosition[] { new(1,1,0), new(moveFirst ? 3 : 2,1,0), new(1,2,0) };
        var setup = BaseJobBattleSetup.Create(new(metadata.Jobs,metadata.Skillsets,metadata.Abilities),map,
            ids.Select((id,i) => new BaseJobUnitPlacement(id,metadata.Jobs[0].Id,positions[i],6,[new(ground,new(1))])),rng);
        var selections = ids.Select(id => new OrdinaryHumanAiUnitSelection(id,id == actor ? [new(skill,1)] : []));
        var lists = OrdinaryHumanAiAbilityListSetup.Create(metadata,self,stone,dash,setup,selections,new(true,true));
        Assert.Equal(2,lists.Units[actor].EvadeQuarterShare);
        var binding = TargetedAbilityBattleSetup.CreateDashProfile(metadata,dash,setup,[new(actor,1,dash.Abilities[0].Id)],ground,0);
        Assert.Throws<NotSupportedException>(() => TargetedAbilityBattleSetup.CreateProfile(metadata,dash,setup,[],ground,0));
        Assert.Throws<NotSupportedException>(() => TargetedAbilityBattleSetup.CreateDashProfile(metadata,stone,setup,[],ground,0));
        var roster = new FlatRosterBattleProfile(ground,ids.Select((id,i) => new FlatRosterUnit(id,i == 1 ? 0 : 1,i)));
        var weapon = new BoundedSwordWeapon(new("test:sword"),1);
        var turns = ids.Select((id,i) => new TurnUnitState(id,5,i == 0 ? 110 : i == 1 ? 90 : 0,i));
        var combat = ids.Select(id => new AttackUnitState(id,50,50,0,0,5,id == enemy ? 100 : 0,0,0,0,70,0,
            id == enemy ? BattleFacing.West : BattleFacing.South,AttackUnitStatus.None,0,id == actor ? weapon.Id : null));
        var ready = RosterFacingTurnBattle.Advance(RosterFacingTurnBattle.Begin(new(new(setup.InitialState,new(turns,rng)),combat),roster));
        Assert.True(ready.Accepted);
        var configuration = new AiDecisionConfiguration(new(roster,ids.Select(id => new AiUnitBaseline(id,255)),true),
            new(true,true,true,true),lists,new(true,true),new(true,true,true,true,true),
            new(Weapon:weapon,OrdinaryAttackTilePolicy:true,NoDefendSupport:true,FreshOrdinarySelectionDefaults:true,Dash:binding,OriginalInstantEnemyDashPolicy:true));
        var request = new RosterAiRefreshRequest(actor,ids.Select(id => new AiUnitPolicy(id,false,false)).ToImmutableArray());
        var decision = AiDecisionMaker.Decide(ready.State.Battle,request,configuration,ready.State.Battle.TurnBattle.Turns.RngState);
        Assert.True(decision.Accepted,decision.Failure?.ToString()); Assert.Equal(AiHumanAbilityKind.Dash,decision.Outcome!.Plan.Action!.Kind);
        Assert.Equal(!moveFirst,decision.Outcome.Plan.ActBeforeMove);
        var first = AiCommandExecutor.Execute(ready.State,request,configuration,new(true,true,true,true));
        Assert.True(first.Accepted);
        var expected=PhysicalDamageRules.Calculate(binding.Abilities[new(dash.Abilities[0].Id.Value)].Definition,5,70,70,100,
            1,0,BattleFacing.West,PhysicalDamageMode.Execution,first.Outcome!.DecisionRng);
        if (moveFirst)
        {
            Assert.Equal(AiCommandKind.Move,first.Outcome!.Kind);
            var retained = RosterMoveDecisionBattle.Continue(first.State,actor,first.Continuation);
            Assert.True(retained.Accepted);
            Assert.Equal(expected.RngState,retained.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(expected.Outcome!.Damage,Assert.Single(retained.Events.OfType<DashResolvedEvent>()).Damage);
        }
        else
        {
            Assert.Equal(expected.RngState,first.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(expected.Outcome!.Damage,Assert.Single(first.Events.OfType<DashResolvedEvent>()).Damage);
        }
        if(!customFormula)Assert.Equal(first.Outcome!.DecisionRng.DrawCount+3,expected.RngState.DrawCount);
        var turn = AiTurnController.Execute(ready.State,request,new(configuration,new(true,true,true,true),BattleFacing.North));
        Assert.True(turn.Completed,turn.Failure?.ToString());
        Assert.Contains(turn.Events,e => e is DashResolvedEvent); Assert.Equal(50,ready.State.Battle.Combatants.Single(u => u.UnitId == enemy).Hp);
        Assert.True(turn.State.Battle.Combatants.Single(u => u.UnitId == enemy).Hp < 50);
        var entry = decision.Outcome.Entries.Single(e => e.Entry.Source.Kind == AiHumanAbilityKind.Dash);
        Assert.All(entry.Dash!.Candidates,c => Assert.Equal(customFormula?35:15,c.Evaluation.Outcome!.NormalDamage));
        var ungranted = TargetedAbilityBattleSetup.CreateDashProfile(metadata,dash,setup,[],ground,0);
        var rejected = AiCommandExecutor.Execute(ready.State,request,configuration with { Abilities = configuration.Abilities with { Dash = ungranted } },new(true,true,true,true));
        Assert.False(rejected.Accepted); Assert.Same(ready.State,rejected.State);
        Assert.Equal(ready.State.Battle.TurnBattle.Turns.RngState,rejected.State.Battle.TurnBattle.Turns.RngState);
    }
}
