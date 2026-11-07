using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Composes the existing movement snapshot with the bounded ordinary turn clock.</summary>
public sealed class TurnBattleState
{
    public BattleState Battle { get; }
    public TurnCycleState Turns { get; }

    public TurnBattleState(BattleState battle, TurnCycleState turns)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(turns);
        if (battle.RngState is null || battle.RngState != turns.RngState)
            throw new ArgumentException("Battle and turn snapshots must carry the same explicit RNG state.");
        if (battle.Units.Count != turns.Units.Length)
            throw new ArgumentException("Battle and turn snapshots must contain the same unit IDs.");
        foreach (TurnUnitState unit in turns.Units)
        {
            if (!battle.TryGetUnit(unit.Id, out BattleUnitState? movement) || movement!.HasMoved != unit.MovementTaken)
                throw new ArgumentException($"Turn unit '{unit.Id}' is missing or has inconsistent movement availability.");
        }
        Battle = battle;
        Turns = turns;
    }
}
