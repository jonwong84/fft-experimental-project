using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Compatibility adapter for adjacent delivery, defaulting to original Dash formula0x37/Y4.</summary>
public sealed record BoundedDashAbility
{
    public PhysicalAbilityDefinition Definition { get; }
    public AssetId Id => Definition.Id;
    public BoundedDashAbility(AssetId id, AbilityFormula? formula = null)
        : this(new PhysicalAbilityDefinition(id, PhysicalAbilityDelivery.AdjacentUnit,
            formula ?? new(AbilityFormulaId.RandomPowerTimesPa, new(0, 4)))) { }

    internal static BoundedDashAbility FromDefinition(PhysicalAbilityDefinition definition) => new(definition);

    private BoundedDashAbility(PhysicalAbilityDefinition definition)
    {
        Definition = definition;
    }
}
