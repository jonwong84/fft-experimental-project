using Fft.Client.Presentation;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;
using Zodiac.Framework.Ai;
using Xunit;

namespace Fft.Client.Tests;

public sealed class OrdinaryMagicLearningTests
{
    private static readonly AssetId Fire = new(AuthoredOrdinaryMagic.Fire);
    private static readonly BattleUnitId Guard = new("guard");
    private static OrdinaryMagicInteraction Interaction()
    {
        var document = AuthoredOrdinaryMagic.Document() with { SchemaVersion = 3, MetadataPackageId = "test:package/learning" };
        document.Units[0] = document.Units[0] with { LearnedPrimarySlots = [0] };
        document.Units[1] = document.Units[1] with { LearnedPrimarySlots = [] };
        var metadata = new ContentPackage("test:package/learning", "test:profile/learning", ContentPackageKind.Baseline, null, [],
            [new(new("test:skillset/magic"), [new(0, new(Fire.Value))])],
            [new(new(Fire.Value), 50, 100, 1, new AiAbilityBehavior(), true, learnOnHit: true)]);
        return new(PreparedOrdinaryMagicBattle.Create(document, metadata));
    }

    private static void Offer(OrdinaryMagicInteraction ui)
    {
        ui.Advance(); ui.BeginCast(); ui.Select(new(4, 2, 0)); ui.Confirm(); ui.Wait(); ui.Advance(); ui.Wait(); ui.Advance();
        Assert.NotNull(ui.State.LearningReport);
        Assert.False(ui.IsError, ui.Message);
    }

    [Fact]
    public void Accepted_spell_enters_normal_player_selection_and_executes_with_exact_replay()
    {
        var ui = Interaction(); Offer(ui);
        Assert.Contains("Enter accepts", ui.Message);
        ui.Confirm(); Assert.Null(ui.State.LearningReport);
        Assert.Contains("learned", ui.Message);
        for (int i = 0; i < 10 && ui.State.Turns.ActiveUnitId != Guard; i++)
        { if (ui.State.Turns.ActiveUnitId is null) ui.Advance(); else ui.Wait(); }
        ui.BeginCast(); Assert.Equal(Fire, ui.SelectedSpell); Assert.Single(ui.AvailableSpells);
        ui.Select(new(0, 2, 0)); ui.Confirm(); ui.Wait();
        for (int i = 0; i < 20 && !ui.State.Pending.IsEmpty; i++)
        { if (ui.State.Turns.ActiveUnitId is null) ui.Advance(); else ui.Wait(); }
        Assert.Equal((ushort)66, ui.State.Units[0].Vitals.Hp);
        Assert.False(ui.IsError, ui.Message);
        Assert.Contains(ui.Journal, c => c is OrdinaryMagicCommand.AnswerLearning { Accept: true });
        AssertReplay(ui);
    }

    [Fact]
    public void Cancel_declines_without_learning_or_rng_and_is_journaled()
    {
        var ui = Interaction(); Offer(ui); var rng = ui.State.Turns.RngState;
        ui.Cancel(); Assert.Null(ui.State.LearningReport);
        Assert.DoesNotContain(Fire, OrdinaryMagicBattle.KnownSpells(ui.State, Guard));
        Assert.Equal(rng, ui.State.Turns.RngState);
        Assert.Contains(ui.Journal, c => c is OrdinaryMagicCommand.AnswerLearning { Accept: false });
        AssertReplay(ui);
    }

    [Fact]
    public void Prompt_blocks_unrelated_intent_and_snapshot_contains_owned_report_and_rows()
    {
        var ui = Interaction(); Offer(ui); string before = OrdinaryMagicSnapshot.Capture(ui.State);
        Assert.Contains("LearningReport", before); Assert.Contains("learned", before);
        ui.Advance(); ui.BeginCast(); ui.BeginStep(); ui.BeginMove(); ui.Wait();
        Assert.True(ui.IsError); Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State));
        Assert.Equal(OrdinaryMagicInteractionMode.Browse, ui.Mode);
        AssertReplay(ui);
    }

    [Fact]
    public void Restart_clears_report_and_learned_slots_and_reproduces_same_offer()
    {
        var ui = Interaction(); string initial = OrdinaryMagicSnapshot.Capture(ui.State);
        Offer(ui); string report = OrdinaryMagicSnapshot.Capture(ui.State);
        ui.Confirm(); ui.Restart();
        Assert.Equal(initial, OrdinaryMagicSnapshot.Capture(ui.State)); Assert.Empty(ui.Journal);
        Offer(ui); Assert.Equal(report, OrdinaryMagicSnapshot.Capture(ui.State)); AssertReplay(ui);
    }

    private static void AssertReplay(OrdinaryMagicInteraction ui) => Assert.Equal(OrdinaryMagicSnapshot.Capture(ui.State),
        OrdinaryMagicSnapshot.Capture(OrdinaryMagicSnapshot.Replay(ui.State.Definition, ui.Journal)));
}
