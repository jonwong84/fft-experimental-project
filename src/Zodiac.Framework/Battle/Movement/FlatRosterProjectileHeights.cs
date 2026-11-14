using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.Framework;

/// <summary>Copied actual sprite-sheet graphic heights. Missing metadata is not a zero-height sprite.</summary>
public sealed class FlatRosterProjectileHeights
{
    public ImmutableDictionary<BattleUnitId, byte> Units { get; }
    public FlatRosterProjectileHeights(IEnumerable<FlatRosterSpriteHeight> units)
    {
        ArgumentNullException.ThrowIfNull(units); var copy = ImmutableDictionary.CreateBuilder<BattleUnitId, byte>();
        foreach (var unit in units)
            if (unit is null || string.IsNullOrWhiteSpace(unit.UnitId.Value) || !copy.TryAdd(unit.UnitId, unit.Height))
                throw new ArgumentException("Specify distinct units with actual byte sprite heights.", nameof(units));
        Units = copy.ToImmutable();
    }
}
