using Zodiac.Framework;
using Zodiac.Framework.Formulas;

namespace Zodiac.Content.Definitions;

public enum TargetedAbilityEffect { ThrowStoneFlat = 1, DashFlat = 2 }

/// <summary>Closed immediate occupied-unit damage/flat-knockback definition, separate from learning metadata.</summary>
public sealed class TargetedAbilityDefinition
{
    public AbilityId Id => new(Definition.Id.Value);
    public PhysicalAbilityDelivery Delivery { get; }
    public TargetedAbilityEffect Effect => Delivery == PhysicalAbilityDelivery.AdjacentUnit
        ? TargetedAbilityEffect.DashFlat : TargetedAbilityEffect.ThrowStoneFlat;
    public int Range => Definition.Range;
    /// <summary>Legacy schema1 binding value. Configurable calculations use Formula.Parameters.</summary>
    public int MaximumDamageMultiplier => Delivery == PhysicalAbilityDelivery.AdjacentUnit ? 4 : 2;
    public int ChargeTicks => 0;
    public int MpCost => 0;
    public AbilityFormula Formula => Definition.Formula;
    public bool IsCustom { get; }
    public PhysicalAbilityDefinition Definition { get; }

    public TargetedAbilityDefinition(AbilityId id, TargetedAbilityEffect effect, int range,
        int maximumDamageMultiplier, int chargeTicks, int mpCost)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        bool supported = effect switch
        {
            TargetedAbilityEffect.ThrowStoneFlat => range == 4 && maximumDamageMultiplier == 2,
            TargetedAbilityEffect.DashFlat => range == 1 && maximumDamageMultiplier == 4,
            _ => false,
        };
        if (!supported || chargeTicks != 0 || mpCost != 0)
            throw new NotSupportedException("Select immediate zero-MP flat Throw Stone (range4/Y2) or Dash (range1/Y4).");
        Delivery = ToDelivery(effect);
        Definition = new(new(id.Value), Delivery, new(AbilityFormulaId.RandomPa, new(0, maximumDamageMultiplier)));
    }

    /// <summary>Explicit custom formula assignment; delivery retains the admitted original targeting and execution attributes.</summary>
    public TargetedAbilityDefinition(AbilityId id, TargetedAbilityEffect delivery, AbilityFormula formula)
        : this(id, ToDelivery(delivery), formula) { }

    /// <summary>Custom data with explicit delivery and formula; no ability-specific class or defaults are required.</summary>
    public TargetedAbilityDefinition(AbilityId id, PhysicalAbilityDelivery delivery, AbilityFormula formula)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        ArgumentNullException.ThrowIfNull(formula);
        Definition = new(new(id.Value), delivery, formula);
        Delivery = delivery;
        IsCustom = true;
    }

    private static PhysicalAbilityDelivery ToDelivery(TargetedAbilityEffect effect) => effect switch
    {
        TargetedAbilityEffect.DashFlat => PhysicalAbilityDelivery.AdjacentUnit,
        TargetedAbilityEffect.ThrowStoneFlat => PhysicalAbilityDelivery.DirectProjectile,
        _ => throw new NotSupportedException("Select an admitted physical delivery profile.")
    };
}
