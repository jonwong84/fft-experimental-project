using Zodiac.Framework.Random;
using System.Collections.Immutable;
using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Compatibility adapter for direct-projectile delivery, defaulting to original Throw Stone formula0x37/Y2.</summary>
public sealed record BoundedThrowStoneAbility
{
    public PhysicalAbilityDefinition Definition { get; }
    public AssetId Id => Definition.Id;
    public int Range => Definition.Range;
    public BoundedThrowStoneAbility(AssetId id, PhysicalDamageFormula? formula = null)
    {
        Definition = new(id, 4, true, true, formula ?? new(PhysicalDamageFormulaId.RandomPa, 0, 2));
    }
}
