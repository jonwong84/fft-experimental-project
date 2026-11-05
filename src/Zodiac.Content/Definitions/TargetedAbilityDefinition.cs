namespace Zodiac.Content.Definitions;

public enum TargetedAbilityEffect { ThrowStoneFlat = 1, DashFlat = 2 }

/// <summary>Closed immediate occupied-unit damage/flat-knockback definition, separate from learning metadata.</summary>
public sealed class TargetedAbilityDefinition
{
    public AbilityId Id { get; }
    public TargetedAbilityEffect Effect { get; }
    public int Range { get; }
    public int MaximumDamageMultiplier { get; }
    public int ChargeTicks { get; }
    public int MpCost { get; }

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
    }
}
