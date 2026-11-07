using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Compatibility adapter for direct-projectile delivery, defaulting to original Throw Stone formula0x37/Y2.</summary>
public sealed record BoundedThrowStoneAbility
{
    public PhysicalAbilityDefinition Definition { get; }
    public AssetId Id => Definition.Id;
    public int Range => Definition.Range;
    public BoundedThrowStoneAbility(AssetId id, PhysicalDamageFormula? formula = null)
        : this(new PhysicalAbilityDefinition(id, PhysicalAbilityDelivery.DirectProjectile,
            formula ?? new(PhysicalDamageFormulaId.RandomPa, 0, 2))) { }

    internal static BoundedThrowStoneAbility FromDefinition(PhysicalAbilityDefinition definition) => new(definition);

    private BoundedThrowStoneAbility(PhysicalAbilityDefinition definition)
    {
        Definition = definition;
    }
}
