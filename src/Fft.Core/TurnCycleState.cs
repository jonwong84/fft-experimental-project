using System.Collections.Immutable;

namespace Fft.Core;

/// <summary>The bounded clock distinguishes an initial tick from rechecking already-ready peers.</summary>
public enum TurnCyclePhase
{
    AddCt,
    PickUnit,
    UnitTurn,
}

/// <summary>Explicit immutable scheduling state for positive-Speed, living, unrestricted ordinary units.</summary>
public sealed class TurnCycleState
{
    public ImmutableArray<TurnUnitState> Units { get; }
    public FftRandomState RngState { get; }
    public ulong Ticks { get; }
    public TurnCyclePhase Phase { get; }
    public BattleUnitId? ActiveUnitId { get; }

    public TurnCycleState(IEnumerable<TurnUnitState> units, FftRandomState rngState,
        ulong ticks = 0, TurnCyclePhase phase = TurnCyclePhase.AddCt)
    {
        ArgumentNullException.ThrowIfNull(units);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        var snapshot = ImmutableArray.CreateBuilder<TurnUnitState>();
        var ids = new HashSet<BattleUnitId>();
        var tieKeys = new HashSet<int>();
        foreach (TurnUnitState unit in units)
        {
            ArgumentNullException.ThrowIfNull(unit);
            if (!ids.Add(unit.Id)) throw new ArgumentException($"Duplicate turn unit ID '{unit.Id}'.", nameof(units));
            if (!tieKeys.Add(unit.TieKey)) throw new ArgumentException($"Duplicate turn tie key {unit.TieKey}.", nameof(units));
            if (unit.HasTurn)
            {
                if (ActiveUnitId is not null) throw new ArgumentException("Only one unit may have a turn.", nameof(units));
                ActiveUnitId = unit.Id;
            }
            snapshot.Add(unit);
        }
        if (snapshot.Count == 0) throw new ArgumentException("The ordinary turn cycle requires at least one positive-Speed unit.", nameof(units));
        if ((phase == TurnCyclePhase.UnitTurn) != (ActiveUnitId is not null))
            throw new ArgumentException("UnitTurn requires exactly one active unit; other phases require none.", nameof(phase));
        Units = snapshot.ToImmutable();
        RngState = rngState;
        Ticks = ticks;
        Phase = phase;
    }
}
