using System.Security.Cryptography;
using System.Text;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Content.Definitions.Job;
using Zodiac.Framework;
using Zodiac.Framework.Ai;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class OrdinaryMagicMetadataTests
{
    private const string PackageId = "test:package/magic-metadata";
    private static OrdinaryMagicPackageDocument Document() => AuthoredOrdinaryMagic.Document() with
        { SchemaVersion = 2, MetadataPackageId = PackageId };
    private static AbilityMetadataDefinition Ability(string id = AuthoredOrdinaryMagic.Fire, bool learnOnHit = false) =>
        new(new(id), 50, 90, 1, new AiAbilityBehavior(TargetsEnemies: true, AffectsHp: true, AffectedBySilence: true,
            AffectedByFaith: true, FollowTarget: true, Reflectable: true, MagicalAttack: true), true, learnOnHit: learnOnHit, showName: true);
    private static ContentPackage Metadata(string packageId = PackageId, string skillset = "test:skillset/magic",
        string ability = AuthoredOrdinaryMagic.Fire, bool bind = true, bool extra = false, bool addon = false, bool includeJob = false) =>
        new(packageId, "test:profile/ordinary", addon ? ContentPackageKind.Addon : ContentPackageKind.Baseline,
            addon ? "test:package/base" : null,
            includeJob ? [new JobDefinition(new(AuthoredOrdinaryMagic.Wizard), new(skillset), new JobStats(3,3), false, [])] : [],
            [new SkillsetDefinition(new(skillset), bind ? [new SkillsetSlot(0, new(ability))] : [])],
            extra ? [Ability(ability), Ability("test:spell/extra")] : [Ability(ability)]);

    [Fact]
    public void Schema2_binds_one_metadata_instance_without_conflating_ai_and_execution_flags()
    {
        var package = PreparedOrdinaryMagicBattle.Create(Document(), Metadata());
        var id = new AssetId(AuthoredOrdinaryMagic.Fire); var ability = package.AbilityMetadata[id];
        Assert.Same(Assert.Single(package.MetadataPackage!.Abilities), ability);
        Assert.True(ability.AiBehavior.MagicalAttack); Assert.True(ability.AiBehavior.TargetsEnemies);
        Assert.False(ability.AiBehavior.Evadeable); Assert.True(package.Definition.Spells[id].Spell.SecondaryEvadeable);
        Assert.Equal(50, ability.JpCost); Assert.True(ability.CanLearnWithJp);
        Assert.Equal(package.ToJson(), PreparedOrdinaryMagicBattle.ReadJson(package.ToJson(), package.MetadataPackage).ToJson());
        var legacy = PreparedOrdinaryMagicBattle.Create(AuthoredOrdinaryMagic.Document());
        Assert.Null(legacy.MetadataPackage); Assert.Empty(legacy.AbilityMetadata);
        Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.ReadJson(package.ToJson()));
    }

    [Theory]
    [InlineData("id")] [InlineData("extra")] [InlineData("spell")] [InlineData("primary")]
    [InlineData("unbound")] [InlineData("addon")] [InlineData("profile")] [InlineData("jobs")]
    [InlineData("legacy")]
    public void Mismatched_companions_cannot_authorize_a_runtime_package(string change)
    {
        var document = Document(); var metadata = Metadata();
        switch (change)
        {
            case "id": metadata = Metadata(packageId: "test:package/other"); break;
            case "extra": metadata = Metadata(extra: true); break;
            case "spell": metadata = Metadata(ability: "test:spell/other"); break;
            case "primary": metadata = Metadata(skillset: "test:skillset/other"); break;
            case "unbound": metadata = Metadata(bind: false); break;
            case "addon": metadata = Metadata(addon: true); break;
            case "profile": document = document with { Ruleset = "original-fft" }; break;
            case "jobs": metadata = Metadata(includeJob: true); break;
            case "legacy": document = document with { SchemaVersion = 1 }; break;
        }
        Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.Create(document, metadata));
    }

    [Fact]
    public void Runtime_load_hashes_the_required_companion_and_never_falls_back_when_it_is_missing()
    {
        var package = PreparedOrdinaryMagicBattle.Create(Document(), Metadata());
        byte[] main = Encoding.UTF8.GetBytes(package.ToJson()), metadata = Encoding.UTF8.GetBytes(package.MetadataPackage!.ToJson());
        string directory = Path.Combine(Path.GetTempPath(), "fft-magic-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string mainPath = Path.Combine(directory, "magic.json"), metadataPath = Path.Combine(directory, "metadata.json");
        PreparedAssetEntry Entry(AssetId id, string name, byte[] bytes) => new(id, name, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var mainEntry = Entry(PreparedOrdinaryMagicBattle.AssetId, "magic.json", main);
        try
        {
            File.WriteAllBytes(mainPath, main); File.WriteAllBytes(metadataPath, metadata);
            var store = new LooseAssetStore(directory, new([mainEntry, Entry(new(PackageId), "metadata.json", metadata)]));
            Assert.Single(PreparedOrdinaryMagicBattle.Load(store).AbilityMetadata);
            metadata[^1] ^= 1; File.WriteAllBytes(metadataPath, metadata);
            Assert.Contains("SHA-256", Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.Load(store)).Message);
            Assert.Throws<KeyNotFoundException>(() => PreparedOrdinaryMagicBattle.Load(new(directory, new([mainEntry]))));
            var overBudget = new LooseAssetStore(directory, new([mainEntry,
                new(new(PackageId), "missing.json", ContentPackage.MaxJsonBytes + 1L, new string('0',64))]));
            Assert.Contains("budget", Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.Load(overBudget)).Message);
        }
        finally { File.Delete(mainPath); File.Delete(metadataPath); Directory.Delete(directory); }
    }

    [Fact]
    public void Learning_metadata_binds_eligible_recipients_and_preserves_repeated_slots()
    {
        var document = Document();
        var ability = Ability(learnOnHit: true);
        ContentPackage MetadataFor(IEnumerable<SkillsetSlot> slots) => new(PackageId, "test:profile/ordinary",
            ContentPackageKind.Baseline, null, [], [new(new("test:skillset/magic"), slots)], [ability]);
        var metadata = MetadataFor([new(0, ability.Id)]);
        var initial = PreparedOrdinaryMagicBattle.Create(document, metadata).Definition.Start();
        Assert.Empty(initial.Units[1].LearnedAbilitySlots[new(AuthoredOrdinaryMagic.Wizard)]);
        document.Units[1] = document.Units[1] with { LearnedSpells = [AuthoredOrdinaryMagic.Fire] };
        Assert.Single(PreparedOrdinaryMagicBattle.Create(document, metadata).AbilityMetadata);
        metadata = MetadataFor([new(0, ability.Id), new(1, ability.Id)]);
        var repeated = PreparedOrdinaryMagicBattle.Create(document, metadata).Definition.Start();
        Assert.True(repeated.Units[1].LearnedAbilitySlots[new(AuthoredOrdinaryMagic.Wizard)].SetEquals([0, 1]));
        Assert.Equal(2, repeated.Definition.Learning!.PrimarySlots(new(AuthoredOrdinaryMagic.Wizard)).Length);
    }

    [Fact]
    public void Version3_slot_rows_are_explicit_snapshot_data_and_can_grant_primary_spells()
    {
        var document = Document() with { SchemaVersion = 3 };
        document.Units[0] = document.Units[0] with { LearnedPrimarySlots = [0] };
        document.Units[1] = document.Units[1] with { LearnedPrimarySlots = [0] };
        var package = PreparedOrdinaryMagicBattle.Create(document, Metadata());
        document.Units[1].LearnedPrimarySlots![0] = 20;
        var state = package.Definition.Start();
        Assert.Contains(new AssetId(AuthoredOrdinaryMagic.Fire), OrdinaryMagicBattle.KnownSpells(state, new("guard")));
        Assert.True(state.Units[1].LearnedAbilitySlots[new(AuthoredOrdinaryMagic.Wizard)].SetEquals([0]));
        Assert.Equal(package.ToJson(), PreparedOrdinaryMagicBattle.ReadJson(package.ToJson(), package.MetadataPackage).ToJson());
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("unknown")]
    [InlineData("negative")] [InlineData("out-of-range")] [InlineData("legacy")]
    public void Invalid_or_implicit_version3_rows_cannot_authorize_learning(string change)
    {
        var document = Document() with { SchemaVersion = 3 };
        document.Units[0] = document.Units[0] with { LearnedPrimarySlots = [] };
        document.Units[1] = document.Units[1] with { LearnedPrimarySlots = change switch
        { "missing" => null, "duplicate" => [0, 0], "unknown" => [1], "negative" => [-1], "out-of-range" => [22], _ => [] } };
        if (change == "legacy") document = document with { SchemaVersion = 2 };
        Assert.Throws<InvalidDataException>(() => PreparedOrdinaryMagicBattle.Create(document, Metadata()));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Player_control_is_explicit_data_independent_of_team_and_survives_round_trip(bool controlled)
    {
        var document = Document();
        document.Units[1] = document.Units[1] with { PlayerControlled = controlled };
        var package = PreparedOrdinaryMagicBattle.Create(document, Metadata());
        var restored = PreparedOrdinaryMagicBattle.ReadJson(package.ToJson(), package.MetadataPackage);
        Assert.Equal(controlled, restored.Definition.Units[1].PlayerControlled);
        Assert.Equal(SpellTargetTeam.Red, restored.Definition.Units[1].Occupant.InitialTeam);
        Assert.True(restored.Definition.Units[0].PlayerControlled);
    }
}
