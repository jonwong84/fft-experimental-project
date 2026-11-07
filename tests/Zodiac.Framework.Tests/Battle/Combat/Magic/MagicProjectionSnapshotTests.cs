using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class MagicProjectionSnapshotTests
{
    [Fact]
    public void Snapshot_copies_roster_and_retains_inactive_anchor_metadata_without_defaulting_current_facts()
    {
        var s = MagicProjectionTestData.Snapshot(); var array = s.Units.ToArray(); var first = array[0];
        var captured = new MagicProjectionSnapshot(s.Map, array, s.RngState); array[0] = null!;
        Assert.Equal(first, captured.Units[0]); Assert.Equal(new Zodiac.Framework.Random.RandomState(57, 9), captured.RngState);
        var inactive = first with { Occupant = first.Occupant with { Active = false }, Current = null, Vitals = null };
        var retained = new MagicProjectionSnapshot(s.Map, s.Units.Replace(first, inactive), s.RngState);
        Assert.True(retained.TryGetUnit(first.Occupant.UnitId, out var unit)); Assert.Equal(first.Occupant.Position, unit!.Occupant.Position);
        Assert.Null(unit.Current); Assert.Null(unit.Vitals); Assert.False(retained.TryGetUnit(new("unknown"), out _));
    }

    [Fact]
    public void Incoherent_ids_order_positions_or_living_fields_reject_before_capture()
    {
        var s = MagicProjectionTestData.Snapshot(); var u = s.Units[0];
        Assert.Throws<ArgumentException>(() => new MagicProjectionSnapshot(s.Map, s.Units.Add(u), s.RngState));
        foreach (var bad in new[] { u with { Occupant = u.Occupant with { SourceOrder = s.Units[1].Occupant.SourceOrder } },
                     u with { Occupant = u.Occupant with { Position = new(99, 0, 0) } }, u with { Current = null },
                     u with { Current = u.Current! with { UnitId = new("wrong") } }, u with { Current = u.Current! with { Mp = 51 } } })
            Assert.Throws<ArgumentException>(() => new MagicProjectionSnapshot(s.Map, s.Units.Replace(u, bad), s.RngState));
        Assert.Throws<NotSupportedException>(() => new MagicProjectionSnapshot(s.Map, s.Units.Replace(u, u with { Vitals = u.Vitals! with { Hp = 0 } }), s.RngState));
    }

    [Fact]
    public void Projection_has_no_original_fixed_roster_size_or_neutral_sign_requirement()
    {
        var q = MagicCastTestData.Ready(); var map = new BattleMapDefinition(26, 1, 1, Enumerable.Repeat(new BattleTile(0), 26).ToArray());
        var units = Enumerable.Range(0, 26).Select(i => new MagicProjectionUnit(
            new(new("u" + i), i, new(i, 0, 0), SpellTargetTeam.Blue, SpellTargetTeam.Blue),
            q.Caster with { UnitId = new("u" + i), Identity = new((MagicZodiacSign)(i % 13)) }, new(100, 100, 50, 70)));
        var s = new MagicProjectionSnapshot(map, units, default); Assert.Equal(26, s.Units.Length);
        Assert.Equal(13, s.Units.Select(u => u.Current!.Identity.Sign).Distinct().Count());
    }
}
