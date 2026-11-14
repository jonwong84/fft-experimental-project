namespace Zodiac.Content.Definitions;

public enum ImmediateAbilityEffect { SelfPhysicalAttackIncrease = 1 }

/// <summary>Closed executable effect data, separate from learning metadata. Schema 1 admits self PA+1 only.</summary>
public sealed class ImmediateAbilityDefinition
{
    public AbilityId Id { get; }
    public ImmediateAbilityEffect Effect { get; }
    public int Amount { get; }
    public int ChargeTicks { get; }
    public int MpCost { get; }

    public ImmediateAbilityDefinition(AbilityId id, ImmediateAbilityEffect effect, int amount, int chargeTicks, int mpCost)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        if (effect != ImmediateAbilityEffect.SelfPhysicalAttackIncrease || amount != 1 || chargeTicks != 0 || mpCost != 0)
            throw new NotSupportedException("Only immediate, zero-MP self PA+1 is supported.");
        Id = id; Effect = effect; Amount = amount; ChargeTicks = chargeTicks; MpCost = mpCost;
    }
}
