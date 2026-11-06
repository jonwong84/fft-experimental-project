using Zodiac.Framework;
using Zodiac.Framework.Formulas;

namespace Zodiac.Content.Definitions;

public enum TargetedAbilityEffect { ThrowStoneFlat = 1, DashFlat = 2 }

/// <summary>Closed immediate occupied-unit damage/flat-knockback definition, separate from learning metadata.</summary>
public sealed class TargetedAbilityDefinition
{
    public AbilityId Id { get; }
    public TargetedAbilityEffect Effect { get; }
    public int Range { get; }
    /// <summary>Legacy schema1 binding value. Configurable calculations use Formula.X/Y.</summary>
    public int MaximumDamageMultiplier { get; }
    public int ChargeTicks { get; }
    public int MpCost { get; }
    public PhysicalDamageFormula Formula { get; }
    public bool IsCustom { get; }
    public PhysicalAbilityDefinition Definition => new(new(Id.Value), Range,
        Effect == TargetedAbilityEffect.ThrowStoneFlat, Effect == TargetedAbilityEffect.ThrowStoneFlat, Formula);

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
        Id = id; Effect = effect; Range = range; MaximumDamageMultiplier = maximumDamageMultiplier; ChargeTicks = chargeTicks; MpCost = mpCost;
        Formula = new(PhysicalDamageFormulaId.RandomPa, 0, maximumDamageMultiplier);
    }

    /// <summary>Explicit custom formula assignment; delivery retains the admitted original targeting and execution attributes.</summary>
    public TargetedAbilityDefinition(AbilityId id, TargetedAbilityEffect delivery, PhysicalDamageFormula formula)
        : this(id, delivery, delivery == TargetedAbilityEffect.DashFlat ? 1 : 4,
            delivery == TargetedAbilityEffect.DashFlat ? 4 : 2, 0, 0)
    {
        ArgumentNullException.ThrowIfNull(formula);
        Formula = formula; IsCustom = true;
    }
}
