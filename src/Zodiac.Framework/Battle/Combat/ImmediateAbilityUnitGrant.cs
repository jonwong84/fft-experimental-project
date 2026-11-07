using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Explicit learned-action grants and zero equipment PA bonus for this closed profile.</summary>
public sealed class ImmediateAbilityUnitGrant
{
    public BattleUnitId UnitId { get; }
    public ImmutableHashSet<AssetId> Abilities { get; }

    public ImmediateAbilityUnitGrant(BattleUnitId unitId, IEnumerable<AssetId> abilities, int equipmentPhysicalAttackBonus)
    {
        if (string.IsNullOrWhiteSpace(unitId.Value)) throw new ArgumentException("A unit ID is required.", nameof(unitId));
        if (equipmentPhysicalAttackBonus != 0) throw new NotSupportedException("This profile requires zero equipment PA bonus.");
        ArgumentNullException.ThrowIfNull(abilities);
        var selected = ImmutableHashSet.CreateBuilder<AssetId>();
        foreach (var id in abilities)
            if (string.IsNullOrWhiteSpace(id.Value) || !selected.Add(id))
                throw new ArgumentException("Ability grants require distinct explicit IDs.", nameof(abilities));
        UnitId = unitId;
        Abilities = selected.ToImmutable();
    }
}
