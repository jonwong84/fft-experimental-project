using System.Text.Json;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class LearningMagicImporterTests
{
    private static byte[] Image()
    {
        var image = ElementalMagicImporterTests.Image();
        int offset = checked((int)(0x8005ebf0u + 19 * 8 - 0x80010000u + 0x800));
        image[offset + 2] = 30; image[offset + 3] = 0x61;
        return image;
    }

    [Fact]
    public void Selected_fire4_metadata_and_slot_state_are_distinct_from_authored_encounter()
    {
        var import = OriginalOrdinaryMagicImporter.DecodeSelection(Image(), OriginalDefaultAbilitySelection.LearnOnHit);
        Assert.Null(import.VerifiedInputSha256);
        Assert.Equal(37, import.Records.Length);
        var d = import.Package.Definition;
        var fire4 = new AssetId("fft.ps1:ability/source-0019");
        Assert.Equal(2, d.Spells.Count);
        Assert.Equal((byte)30, d.Learning!.Abilities[fire4].Rate);
        Assert.True(d.Learning.Abilities[fire4].LearnOnHit);
        Assert.Equal(new[] { 0, 3 }, d.Learning.PrimarySlots(d.Units[1].RewardJobId).Select(s => s.Index));
        Assert.Equal((byte)80, d.Units[1].OriginalJobId);
        Assert.Empty(d.Units[1].LearnedSpells);
        Assert.Empty(d.Units[1].LearnedPrimarySlots!);
        Assert.Equal((ushort)75, d.Units[1].Current.Mp);
        using var json = JsonDocument.Parse(import.Package.ToJson());
        Assert.Equal(3, json.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Prepared_learning_recipient_can_accept_or_decline_the_actual_cast(bool accept)
    {
        var d = OriginalOrdinaryMagicImporter.DecodeSelection(Image(), OriginalDefaultAbilitySelection.LearnOnHit).Package.Definition;
        var state = d.Start();
        var fire4 = new AssetId("fft.ps1:ability/source-0019");
        void Apply(OrdinaryMagicCommand command)
        { var result = OrdinaryMagicBattle.Apply(state, command); Assert.True(result.Accepted, result.Rejection); state = result.State; }
        Apply(new OrdinaryMagicCommand.Advance());
        Apply(new OrdinaryMagicCommand.Cast(new("mage"), fire4, new(4, 2, 0)));
        Apply(new OrdinaryMagicCommand.Wait(new("mage")));
        for (int i = 0; i < 30 && !state.Pending.IsEmpty; i++)
            Apply(state.Turns.ActiveUnitId is { } active ? new OrdinaryMagicCommand.Wait(active) : new OrdinaryMagicCommand.Advance());
        Assert.Equal((ushort)75, state.Units[1].Vitals.Hp);
        Assert.Equal((ushort)27, state.Units[0].Current.Mp);
        var report = Assert.IsType<OrdinaryMagicLearningReport>(state.LearningReport);
        Assert.Equal(new BattleUnitId("guard"), report.LearnerId);
        var rng = state.Turns.RngState;
        Apply(new OrdinaryMagicCommand.AnswerLearning(report.Id, report.LearnerId, accept));
        Assert.Equal(accept, OrdinaryMagicBattle.KnownSpells(state, report.LearnerId).Contains(fire4));
        Assert.Equal(rng, state.Turns.RngState);
        Assert.Null(state.LearningReport);
    }
}
