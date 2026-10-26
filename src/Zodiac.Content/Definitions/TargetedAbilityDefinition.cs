namespace Zodiac.Content.Definitions;

public enum TargetedAbilityEffect { ThrowStoneFlat = 1 }

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
        if (effect != TargetedAbilityEffect.ThrowStoneFlat || range != 4 || maximumDamageMultiplier != 2 || chargeTicks != 0 || mpCost != 0)
            throw new NotSupportedException("Only immediate zero-MP range-4 Throw Stone with Y=2 is supported.");
        Id = id; Effect = effect; Range = range; MaximumDamageMultiplier = maximumDamageMultiplier; ChargeTicks = chargeTicks; MpCost = mpCost;
    }
}
