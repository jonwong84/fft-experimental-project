using System.Collections.Immutable;
using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Compatibility adapter for adjacent delivery, defaulting to original Dash formula0x37/Y4.</summary>
public sealed record BoundedDashAbility
{
    public PhysicalAbilityDefinition Definition { get; }
    public AssetId Id => Definition.Id;
    public BoundedDashAbility(AssetId id, PhysicalDamageFormula? formula = null)
    {
        Definition = new(id, 1, false, false, formula ?? new(PhysicalDamageFormulaId.RandomPa, 0, 4));
    }
}
