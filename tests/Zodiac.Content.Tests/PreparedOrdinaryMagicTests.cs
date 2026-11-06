using System.Text.Json.Nodes;
using System.Text;
using System.Security.Cryptography;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedOrdinaryMagicTests
{
    [Fact]
    public void Round_trip_copies_mutable_wire_data_and_uses_immutable_job_metadata()
    {
        var document = AuthoredOrdinaryMagic.Document();
        document.Jobs[0] = document.Jobs[0] with { Jump = 5 };
        document.Units[1] = document.Units[1] with { Facing = BattleFacing.West };
        var package = PreparedOrdinaryMagicBattle.Create(document);
        string json = package.ToJson();
        document.Units[0].LearnedSpells[0] = "test:spell/missing";
        document.Units[0].Career.Clear(); document.Jobs[0] = document.Jobs[0] with { Move = 0 };
        document.JobThresholds[0] = 999;
        Assert.Equal(json, package.ToJson());
        Assert.Equal(json, PreparedOrdinaryMagicBattle.ReadJson(json).ToJson());
        Assert.Equal(3, package.JobDefinitions[AuthoredOrdinaryMagic.Wizard].JobStats.BaseMove);
        Assert.Equal(12, package.Definition.Units[0].Growth.Hp);
        Assert.Equal(5, package.Definition.Units[0].Jump);
        Assert.Equal(5, package.JobDefinitions[AuthoredOrdinaryMagic.Wizard].JobStats.BaseJump);
        Assert.Equal(BattleFacing.West, package.Definition.Start().Units[1].Facing);
        Assert.NotNull(package.Definition.MovementGround);
        Assert.Contains(new AssetId(AuthoredOrdinaryMagic.Fire), package.Definition.Units[0].LearnedSpells);
        Assert.Equal((ushort)100, package.Definition.JobThresholds[0]);
        Assert.Equal("authored-ordinary", package.Ruleset);
        Assert.Equal(new[] { new AssetId(AuthoredOrdinaryMagic.Fire) }, package.SpellOrder.ToArray());
    }

    [Fact]
    public void Loaded_content_drives_real_charge_damage_mp_growth_and_rewards()
    {
        var package = PreparedOrdinaryMagicBattle.ReadJson(PreparedOrdinaryMagicBattle.Create(AuthoredOrdinaryMagic.Document()).ToJson());
        var state = package.Definition.Start();
        void Apply(OrdinaryMagicCommand command)
        {
            var result = OrdinaryMagicBattle.Apply(state, command);
            Assert.True(result.Accepted, result.Rejection); state = result.State;
        }
        Apply(new OrdinaryMagicCommand.Advance());
        Apply(new OrdinaryMagicCommand.Cast(new("mage"), new(AuthoredOrdinaryMagic.Fire), new(4, 2, 0)));
        Assert.Equal((ushort)75, state.Units[0].Current.Mp);
        Assert.True(state.Units[0].Current.Statuses.Charging);
        Apply(new OrdinaryMagicCommand.Wait(new("mage"))); Apply(new OrdinaryMagicCommand.Advance());
        Apply(new OrdinaryMagicCommand.Wait(new("guard"))); Apply(new OrdinaryMagicCommand.Advance());
        Assert.Equal((ushort)146, state.Units[1].Vitals.Hp);
        Assert.Equal((ushort)69, state.Units[0].Current.Mp);
        Assert.Equal((byte)11, state.Units[0].Level); Assert.Equal((byte)0, state.Units[0].Experience);
        Assert.Equal((ushort)62, state.Units[0].JobPoints[new(AuthoredOrdinaryMagic.Wizard)].Current);
        Assert.Equal(12UL, state.Turns.RngState.DrawCount);
    }

    [Theory]
    [InlineData("version")] [InlineData("profile")] [InlineData("unknown")] [InlineData("missing")]
    [InlineData("null-entry")] [InlineData("null-growth")] [InlineData("job-ref")] [InlineData("spell-ref")]
    [InlineData("career")] [InlineData("duplicate")] [InlineData("maxima")] [InlineData("dimension")]
    [InlineData("enum")] [InlineData("numeric-enum")] [InlineData("negative-mp")]
    public void Malformed_or_unsupported_packages_fail_before_creating_a_battle(string change)
    {
        var root = JsonNode.Parse(PreparedOrdinaryMagicBattle.Create(AuthoredOrdinaryMagic.Document()).ToJson())!;
        switch (change)
        {
            case "version": root["schemaVersion"] = 2; break;
            case "profile": root["profile"] = "anything"; break;
            case "unknown": root["spells"]![0]!["extra"] = true; break;
            case "missing": root.AsObject().Remove("units"); break;
            case "null-entry": root["jobs"]![0] = null; break;
            case "null-growth": root["jobs"]![0]!["growth"] = null; break;
            case "job-ref": root["units"]![0]!["jobId"] = "test:job/missing"; break;
            case "spell-ref": root["units"]![0]!["learnedSpells"]![0] = "test:spell/missing"; break;
            case "career": root["units"]![0]!["career"]!.AsObject().Remove(AuthoredOrdinaryMagic.Wizard); break;
            case "duplicate": root["units"]![1]!["id"] = "mage"; break;
            case "maxima": root["units"]![0]!["mp"] = 1000; break;
            case "dimension": root["width"] = int.MaxValue; break;
            case "enum": root["units"]![0]!["team"] = "missing"; break;
            case "numeric-enum": root["units"]![0]!["team"] = 0; break;
            case "negative-mp": root["spells"]![0]!["mpCost"] = -1; break;
        }
        Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.ReadJson(root.ToJsonString()));
    }

    [Fact]
    public void Duplicate_fields_and_load_budget_fail_closed()
    {
        string json = PreparedOrdinaryMagicBattle.Create(AuthoredOrdinaryMagic.Document()).ToJson();
        Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.ReadJson(json.Replace("\"seed\": 57", "\"seed\": 57, \"seed\": 57")));
        Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.ReadJson(new string(' ', PreparedOrdinaryMagicBattle.MaxJsonBytes + 1)));
    }

    [Fact]
    public void Runtime_load_requires_manifest_integrity_and_preserves_integer_mp_cost()
    {
        var document = AuthoredOrdinaryMagic.Document();
        document.Spells[0] = document.Spells[0] with { MpCost = 300 };
        byte[] bytes = Encoding.UTF8.GetBytes(PreparedOrdinaryMagicBattle.Create(document).ToJson());
        string directory = Path.Combine(Path.GetTempPath(), "fft-fire-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "magic.json");
        try
        {
            File.WriteAllBytes(path, bytes);
            var store = new LooseAssetStore(directory, new PreparedAssetManifest([
                new(PreparedOrdinaryMagicBattle.AssetId, "magic.json", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))]));
            Assert.Equal(300, PreparedOrdinaryMagicBattle.Load(store).Definition.Spells[new(AuthoredOrdinaryMagic.Fire)].MpCost);
            bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
            Assert.Contains("SHA-256", Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.Load(store)).Message);
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }
}
