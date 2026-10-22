using Fft.Content.ContentPackages;
using Fft.Core;

namespace Fft.Content.BattleSetup;

/// <summary>Validates explicit supported combat inputs and equipment before simulation startup.</summary>
public static class AttackBattleSetup
{
    public static AttackBattleSnapshot Create(TurnBattleState state, IEnumerable<AttackUnitState> combatants,
        SwordContentPackage swords, ContentPackage metadata)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(swords);
        swords.ValidateAgainst(metadata);
        var snapshot = new AttackBattleSnapshot(state, combatants);
        snapshot.ValidateInitialSetup();
        var weapons = swords.Swords.ToDictionary(sword => sword.Id.Value, StringComparer.Ordinal);
        foreach (AttackUnitState unit in snapshot.Combatants)
        {
            if (unit.RightHandWeaponId is { } id)
            {
                if (!weapons.TryGetValue(id.Value, out var weapon))
                    throw new ArgumentException($"Unit '{unit.UnitId}' references missing weapon '{id}'.");
                if (weapon.Evade != 0) throw new NotSupportedException("The bounded attack setup excludes weapon evasion.");
                // Largest possible critical XA is 2*PA-1 (or zero when PA is zero).
                if ((long)Math.Max(0, 2 * unit.PhysicalAttack - 1) * weapon.Power > short.MaxValue)
                    throw new NotSupportedException($"Unit '{unit.UnitId}' exceeds the traced nonnegative signed-16 damage envelope.");
            }
        }
        return snapshot;
    }
}
