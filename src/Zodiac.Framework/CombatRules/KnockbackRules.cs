namespace Zodiac.Framework.CombatRules;

/// <summary>Shared knockback calculation for the currently admitted single-target, ordinary flat-ground profiles.</summary>
internal static class KnockbackRules
{
    /// <summary>Propose a knockback landing without moving the target or consuming RNG.</summary>
    /// <remarks>
    /// Knockback Calculation, 0x801843EC; knockback_calculate in src/battle/combat/formula_core.c,
    /// fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.
    /// Callers establish one ordinary target, no immortality/importance/mount restrictions and certified flat terrain.
    /// Trigger rolls and lethal aftermath suppression belong to callers. General layers, falls and movement flags remain gated.
    /// </remarks>
    internal static TilePosition? KnockbackCalculation(BattleState battle, BattleUnitId actorId, BattleUnitId targetId)
    {
        if (actorId == targetId)
        {
            return null;
        }

        var attacker = battle.Units.Single(unit => unit.Id == actorId).Position;
        var target = battle.Units.Single(unit => unit.Id == targetId).Position;
        long dx = (long)target.X - attacker.X;
        long dy = (long)target.Y - attacker.Y;
        if (dx == 0 && dy == 0)
        {
            return null;
        }

        TilePosition destination;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            destination = new(target.X + Math.Sign(dx), target.Y, target.Layer);
        }
        else
        {
            // The source chooses Y when the absolute deltas are equal.
            destination = new(target.X, target.Y + Math.Sign(dy), target.Layer);
        }

        return KnockbackDestinationCheck(battle, destination);
    }

    /// <summary>The admitted flat-ground portion of Knockback 2 (0x80184610, knockback_destination_check).</summary>
    private static TilePosition? KnockbackDestinationCheck(BattleState battle, TilePosition destination)
    {
        // Caller profiles prove the source terrain/height/headroom conditions. Occupancy includes every battle unit.
        if (!battle.Map.TryGetTile(destination, out _) || battle.Units.Any(unit => unit.Position == destination))
        {
            return null;
        }

        return destination;
    }
}
