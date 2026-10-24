namespace Fft.Core;

/// <summary>One ordinary, status-free unit's immutable CT scheduling state.</summary>
public sealed record TurnUnitState
{
    public BattleUnitId Id { get; }
    public int Speed { get; }
    public int Ct { get; }
    public int TieKey { get; }
    public bool HasTurn { get; }
    public bool MovementTaken { get; }
    public bool ActionTaken { get; }

    public TurnUnitState(BattleUnitId id, int speed, int ct, int tieKey,
        bool hasTurn = false, bool movementTaken = false, bool actionTaken = false)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("A turn unit requires a non-default ID.", nameof(id));
        if (speed is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(speed), "Ordinary turn Speed must be 1..255.");
        if (ct is < 0 or > 254) throw new ArgumentOutOfRangeException(nameof(ct), "Ordinary CT must be 0..254; Quick is unsupported.");
        ArgumentOutOfRangeException.ThrowIfNegative(tieKey);
        if (hasTurn && ct >= 100) throw new ArgumentException("An active unit must already have its CT reduced modulo 100.", nameof(ct));
        if (hasTurn && movementTaken && actionTaken)
            throw new ArgumentException("A unit with both choices consumed must have completed its turn.", nameof(hasTurn));
        Id = id;
        Speed = speed;
        Ct = ct;
        TieKey = tieKey;
        HasTurn = hasTurn;
        MovementTaken = movementTaken;
        ActionTaken = actionTaken;
    }

    internal TurnUnitState Update(int ct, bool hasTurn, bool movementTaken, bool actionTaken) =>
        new(Id, Speed, ct, TieKey, hasTurn, movementTaken, actionTaken);
}
