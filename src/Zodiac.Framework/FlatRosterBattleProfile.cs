using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record FlatRosterUnit(BattleUnitId UnitId, int StableTeam, int SourceOrder);

/// <summary>Explicit stable teams/source order for ordinary neutral/effect-free units on open cost-one ground.</summary>
/// <remarks>ZOD-277/278, decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.
/// Certifies no mounts, formation/importance/immortality, charges, reactions, traps, pickups or unrepresented effects.
/// StableTeam represents both initial and current team. Catalog storage has no original slot capacity.</remarks>
public sealed class FlatRosterBattleProfile
{
    public TerrainSurfaceId Ground { get; }
    public ImmutableArray<FlatRosterUnit> Members { get; }
    public ImmutableDictionary<BattleUnitId, FlatRosterUnit> Units { get; }

    public FlatRosterBattleProfile(TerrainSurfaceId ground, IEnumerable<FlatRosterUnit> units)
    {
        if (string.IsNullOrWhiteSpace(ground.Value)) throw new ArgumentException("Explicit ordinary ground is required.", nameof(ground));
        ArgumentNullException.ThrowIfNull(units); var copy = units.ToImmutableArray();
        if (copy.IsEmpty || copy.Any(u => u is null || string.IsNullOrWhiteSpace(u.UnitId.Value) || u.StableTeam is < 0 or > 3 || u.SourceOrder < 0) ||
            copy.Select(u => u.UnitId).Distinct().Count() != copy.Length || copy.Select(u => u.SourceOrder).Distinct().Count() != copy.Length)
            throw new ArgumentException("Distinct explicit units, stable teams and source-order keys are required.", nameof(units));
        Ground = ground; Members = copy.OrderBy(u => u.SourceOrder).ToImmutableArray(); Units = Members.ToImmutableDictionary(u => u.UnitId);
    }

    /// <summary>Original-compatible sword/result admission, including represented corpses; no death scheduling or corpse movement.</summary>
    public void ValidateState(AttackBattleSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state); state.ValidateSupportedGeometry();
        if (state.Combatants.Length > 21 || state.Combatants.Length != Units.Count ||
            !state.Combatants.All(u => Units.ContainsKey(u.UnitId)) ||
            state.TurnBattle.Turns.Units.Any(u => Units[u.Id].SourceOrder != u.TieKey))
            throw new NotSupportedException("Match the complete roster/source order within the original 21-slot domain.");
        var map = state.TurnBattle.Battle.Map;
        if (map.Width > 16 || map.Depth > 16) throw new NotSupportedException("The ordinary roster profile requires a rectangle at most 16 by 16.");
        FlatSwordBattleProfile.ValidateGround(state, Ground);
        foreach (var body in state.TurnBattle.Battle.Units)
        {
            var movement = body.MovementProfile;
            if (movement.Move > 255 || movement.Jump > 255 || !movement.TryGetTerrainCost(Ground, out var cost) || cost.IsBlocked || cost.MovePoints != 1)
                throw new NotSupportedException("Byte Move/Jump and explicit cost-one ordinary ground are required for every roster unit.");
        }
    }

    public bool SupportsMovement(AttackBattleSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        // Source has sixteen OTHER movement records. Beyond this extent, truncation needs its own compatibility work.
        if (state.Combatants.Length > 17 || state.Combatants.Any(u => u.Hp == 0)) return false;
        try { ValidateState(state); return true; }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return false; }
    }
}
