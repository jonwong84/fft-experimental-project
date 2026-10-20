namespace Fft.Core;

/// <summary>Immutable runtime state for a unit in the initial battle simulation slice.</summary>
/// <remarks>This model is intentionally smaller than a complete FFT battle unit.</remarks>
public sealed record BattleUnitState
{
    public BattleUnitState(BattleUnitId id, TilePosition position, int move, bool hasMoved = false)
        : this(id, position, UnitMovementProfile.ForFlatGridPrototype(move), hasMoved)
    {
    }

    public BattleUnitState(
        BattleUnitId id,
        TilePosition position,
        UnitMovementProfile movementProfile,
        bool hasMoved = false)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("A battle unit must have a non-empty ID.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(movementProfile);
        Id = id;
        Position = position;
        MovementProfile = movementProfile;
        HasMoved = hasMoved;
    }

    public BattleUnitId Id { get; }

    public TilePosition Position { get; }

    public UnitMovementProfile MovementProfile { get; }

    public int Move => MovementProfile.Move;

    public bool HasMoved { get; }

    internal BattleUnitState MoveTo(TilePosition destination) => new(Id, destination, MovementProfile, hasMoved: true);
}
