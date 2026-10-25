using Zodiac.Content.ContentPackages;
using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

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
                // Same-sign zodiac floors XA to 1 even when PA is zero, before the critical step.
                if ((long)(2 * Math.Max(1, unit.PhysicalAttack) - 1) * weapon.Power > short.MaxValue)
                    throw new NotSupportedException($"Unit '{unit.UnitId}' exceeds the traced nonnegative signed-16 damage envelope.");
            }
        }
        return snapshot;
    }

    /// <summary>Projects a selected supported content sword without coupling simulation to package loading.</summary>
    public static BoundedSwordWeapon ResolveWeapon(SwordContentPackage swords, ContentPackage metadata, AssetId id)
    {
        ArgumentNullException.ThrowIfNull(swords);
        swords.ValidateAgainst(metadata);
        var weapon = swords.Swords.SingleOrDefault(sword => sword.Id.Value == id.Value)
            ?? throw new ArgumentException($"Missing sword '{id}'.", nameof(id));
        if (swords.Version == 1 && weapon.Evade != 0)
            throw new NotSupportedException("Legacy sword setup excludes nonzero raw weapon evade.");
        return new(id, weapon.Power);
    }
}
