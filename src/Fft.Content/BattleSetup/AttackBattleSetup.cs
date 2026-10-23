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
                if (swords.Version == 1 && weapon.Evade != 0)
                    throw new NotSupportedException("The legacy attack setup excludes nonzero raw weapon evade.");
                // This closed setup profile has no reactions, shields or accessories. Raw weapon evade
                // is retained in schema 2, but contributes zero without Weapon Guard. Capabilities do
                // not grant reaction/support abilities. AttackUnitState still excludes effective hand evade.
                if (PhysicalWeaponEvasion.GetContribution(weapon.Evade, hasWeaponGuard: false, actionUnrestricted: true) != unit.RightHandEvade)
                    throw new NotSupportedException("Effective weapon evade does not match the supported no-reaction profile.");
                // Largest possible critical XA is 2*PA-1 (or zero when PA is zero).
                if ((long)Math.Max(0, 2 * unit.PhysicalAttack - 1) * weapon.Power > short.MaxValue)
                    throw new NotSupportedException($"Unit '{unit.UnitId}' exceeds the traced nonnegative signed-16 damage envelope.");
            }
        }
        return snapshot;
    }
}
