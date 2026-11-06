using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class OrdinaryMagicPrimaryMetadataTests
{
    private static int Offset(uint address) => checked((int)(address - 0x80010000 + 0x800));

    [Fact]
    public void Import_preserves_primary_unknown_learning_and_static_ai_bits_without_changing_execution_flags()
    {
        byte[] image = OriginalOrdinaryMagicImporterTests.Image(); int row = Offset(0x8005ebf0 + 16 * 8);
        image[row + 2] = 255; image[row + 3] = 0xf1;
        new byte[] { 0x82, 0x55, 2, 255 }.CopyTo(image, row + 4);
        var imported = OriginalOrdinaryMagicImporter.DecodeSelection(image); var id = new AssetId("fft.ps1:ability/source-0016");
        var ability = imported.Package.AbilityMetadata[id];
        Assert.True(ability.UnknownTypeBit4); Assert.True(ability.LearnOnHit); Assert.True(ability.ShowName);
        Assert.False(ability.CanLearnWithJp); Assert.Equal(255, ability.LearnOnHitRateRaw); Assert.Equal(1, ability.SerializedClassRaw);
        Assert.True(ability.UsableByAi); Assert.True(ability.AiEvadeWithMotion); Assert.Equal((byte)126, ability.UnresolvedAiBitsRaw);
        Assert.False(ability.AiBehavior.Evadeable); Assert.True(imported.Package.Definition.Spells[id].Spell.SecondaryEvadeable);
        Assert.Equal(new byte[] { 0x82, 0x55, 2, 255 }, ability.AiFlagsRaw.ToArray());
        var roundTrip = ContentPackage.ReadJson(imported.Package.MetadataPackage!.ToJson());
        Assert.Equal(ability.AiFlagsRaw.ToArray(), Assert.Single(roundTrip.Abilities).AiFlagsRaw.ToArray());
        Assert.Equal(imported.Package.ToJson(), PreparedOrdinaryMagicBattle.ReadJson(imported.Package.ToJson(), roundTrip).ToJson());
    }

    [Fact]
    public void Selected_skillsets_keep_source_slots_and_basic_skill_has_an_explicit_empty_intersection()
    {
        var import = OriginalOrdinaryMagicImporter.DecodeSelection(OriginalOrdinaryMagicImporterTests.Image());
        var metadata = import.Package.MetadataPackage!;
        Assert.Empty(metadata.Jobs); Assert.Equal(2, metadata.Skillsets.Length);
        var slot = Assert.Single(metadata.Skillsets.Single(s => s.Id.Value.EndsWith("black-magic", StringComparison.Ordinal)).Slots);
        Assert.Equal(0, slot.Slot); Assert.Equal("fft.ps1:ability/source-0016", slot.AbilityId!.Value.Value);
        Assert.Empty(metadata.Skillsets.Single(s => s.Id.Value.EndsWith("basic-skill", StringComparison.Ordinal)).Slots);
        byte[] changed = OriginalOrdinaryMagicImporterTests.Image(); changed[Offset(0x80064a94 + 5 * 25) + 3] = 16;
        Assert.Throws<InvalidDataException>(() => OriginalOrdinaryMagicImporter.DecodeSelection(changed));
    }
}
