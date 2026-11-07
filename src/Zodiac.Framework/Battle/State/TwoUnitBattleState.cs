using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed class TwoUnitBattleState
{
    public TwoUnitBattleDefinition Definition { get; }
    public AttackBattleSnapshot Battle { get; }
    public TwoUnitBattleOutcome Outcome { get; }
    public BattleUnitId? WinnerId => Outcome switch
    {
        TwoUnitBattleOutcome.FirstUnitWon => Definition.FirstUnitId,
        TwoUnitBattleOutcome.SecondUnitWon => Definition.SecondUnitId,
        _ => null,
    };

    internal TwoUnitBattleState(TwoUnitBattleDefinition definition, AttackBattleSnapshot battle, TwoUnitBattleOutcome outcome)
    { Definition = definition; Battle = battle; Outcome = outcome; }
}
