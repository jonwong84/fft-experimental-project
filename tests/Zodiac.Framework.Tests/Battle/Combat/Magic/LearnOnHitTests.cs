using System.Collections.Immutable;
using Zodiac.Framework.Random;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class LearnOnHitTests
{
    private static readonly AssetId Spell = new("test:spell/learn");
    private static LearnOnHitCandidate Unit(string id, int order, params int[] slots) => new(new(id), order,
        AbilityOutcomeMarker.Affected, slots.Select(i => new AbilityLearningSlot(i, Spell)).ToImmutableArray(), []);

    [Fact]
    public void SourceOrderReplacesMarkersAndStopsAtFirstSuccess()
    {
        var units = ImmutableArray.Create(Unit("later", 4, 3), Unit("actor", 0, 0),
            Unit("ineligible", 1), Unit("first", 2, 5));
        var result = LearnOnHit.Scan(new("actor"), Spell, true, 100, units, new(57, 0));
        Assert.Equal(new("first"), result.LearnerId);
        Assert.Equal(new RandomState(2770839166, 1), result.RngState); // BIOS output 9512 -> roll29.
        Assert.Equal(AbilityOutcomeMarker.Affected, result.Units[0].Marker);
        Assert.Equal(AbilityOutcomeMarker.Affected, result.Units[1].Marker);
        Assert.All(result.Units.Skip(2), u => Assert.Equal(AbilityOutcomeMarker.LearnChecked, u.Marker));
        Assert.Empty(result.Units[3].LearnedSlots); // Offer is not acceptance.
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(100, true)]
    [InlineData(255, true)]
    public void RawRateBoundaryAlwaysConsumesOneDraw(byte rate, bool success)
    {
        var result = LearnOnHit.Scan(new("actor"), Spell, true, rate, [Unit("target", 1, 0)], new(57, 8));
        Assert.Equal(success, result.LearnerId is not null);
        Assert.Equal(9UL, result.RngState.DrawCount);
    }

    [Fact]
    public void DuplicateSlotsRollIndependentlyAndAcceptLearnsAllMatches()
    {
        var unit = Unit("target", 1, 0, 8, 21) with { LearnedSlots = [0] };
        // Seed1: rolls51 then17. First unlearned duplicate fails, next succeeds.
        var result = LearnOnHit.Scan(new("actor"), Spell, true, 30, [unit], new(1, 0));
        Assert.Equal(unit.UnitId, result.LearnerId);
        Assert.Equal(2UL, result.RngState.DrawCount);
        Assert.True(LearnOnHit.Accept(Spell, unit.PrimarySlots, unit.LearnedSlots).SetEquals([0, 8, 21]));
    }

    [Fact]
    public void KnownMonsterBlockedAndAlreadyCheckedTargetsDoNotDraw()
    {
        var units = ImmutableArray.Create(Unit("known", 1, 0) with { LearnedSlots = [0] },
            Unit("monster", 2, 0) with { IsMonster = true }, Unit("blocked", 3, 0) with { PreventReaction = true },
            Unit("checked", 4, 0) with { Marker = AbilityOutcomeMarker.LearnChecked },
            Unit("targeted", 5, 0) with { Marker = AbilityOutcomeMarker.Targeted });
        var result = LearnOnHit.Scan(new("actor"), Spell, true, 255, units, new(57, 0));
        Assert.Null(result.LearnerId);
        Assert.Equal(new RandomState(57, 0), result.RngState);
        Assert.All(result.Units.Take(4), u => Assert.Equal(AbilityOutcomeMarker.LearnChecked, u.Marker));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void DisabledOrHamedoLeavesEverythingUntouched(bool enabled, bool hamedo)
    {
        ImmutableArray<LearnOnHitCandidate> units = [Unit("target", 1, 0)];
        var result = LearnOnHit.Scan(new("actor"), Spell, enabled, 255, units, new(57, 0), hamedo);
        Assert.Equal(units, result.Units);
        Assert.Equal(0UL, result.RngState.DrawCount);
    }

    [Fact]
    public void ExhaustionCannotPublishPartialScan()
    {
        ImmutableArray<LearnOnHitCandidate> units = [Unit("target", 1, 0)];
        Assert.Throws<OverflowException>(() => LearnOnHit.Scan(new("actor"), Spell, true, 255, units, new(57, ulong.MaxValue)));
        Assert.Equal(AbilityOutcomeMarker.Affected, units[0].Marker);
    }
}
