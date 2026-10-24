using System.Collections.ObjectModel;

namespace Fft.Core;

/// <summary>Immutable snapshot of the map and units needed by the movement proof of concept.</summary>
public sealed class BattleState
{
    private readonly ReadOnlyCollection<BattleUnitState> _units;
    private readonly Dictionary<BattleUnitId, int> _unitIndexes;

    public BattleState(BattleMapDefinition map, IEnumerable<BattleUnitState> units, FftRandomState? rngState = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(units);

        BattleUnitState[] unitArray = units.ToArray();
        _unitIndexes = new Dictionary<BattleUnitId, int>();
        for (int index = 0; index < unitArray.Length; index++)
        {
            BattleUnitState unit = unitArray[index]
                ?? throw new ArgumentException("The unit collection cannot contain null entries.", nameof(units));
            if (!map.TryGetTile(unit.Position, out _))
            {
                throw new ArgumentException($"Unit '{unit.Id}' is positioned outside the map.", nameof(units));
            }

            if (!_unitIndexes.TryAdd(unit.Id, index))
            {
                throw new ArgumentException($"Duplicate battle unit ID '{unit.Id}'.", nameof(units));
            }
        }

        Map = map;
        RngState = rngState;
        _units = Array.AsReadOnly(unitArray);
    }

    public BattleMapDefinition Map { get; }

    /// <summary>Explicit RNG snapshot; null means this scenario has not selected a random stream.</summary>
    public FftRandomState? RngState { get; }

    public IReadOnlyList<BattleUnitState> Units => _units;

    public bool TryGetUnit(BattleUnitId id, out BattleUnitState? unit) => TryGetUnit(id, out unit, out _);

    internal bool TryGetUnit(BattleUnitId id, out BattleUnitState? unit, out int index)
    {
        if (_unitIndexes.TryGetValue(id, out index))
        {
            unit = _units[index];
            return true;
        }

        unit = null;
        index = -1;
        return false;
    }

    internal BattleState ReplaceUnit(int index, BattleUnitState replacement)
    {
        BattleUnitState[] updated = [.. _units];
        updated[index] = replacement;
        return new BattleState(Map, updated, RngState);
    }
}
