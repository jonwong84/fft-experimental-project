using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Immutable living vital projection, not game battle/turn state or authority to adopt results.</summary>
public sealed class MagicProjectionSnapshot
{
    public BattleMapDefinition Map { get; }
    public ImmutableArray<MagicProjectionUnit> Units { get; }
    public RandomState RngState { get; }

    public MagicProjectionSnapshot(BattleMapDefinition map, IEnumerable<MagicProjectionUnit> units, RandomState rngState)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(units);
        var captured = units.ToImmutableArray();
        if (captured.IsEmpty) throw new ArgumentException("A projection requires an explicit roster.", nameof(units));
        var ids = new HashSet<BattleUnitId>(); var orders = new HashSet<int>();
        foreach (var unit in captured)
        {
            if (unit is null || unit.Occupant is not { } u || string.IsNullOrWhiteSpace(u.UnitId.Value) || !ids.Add(u.UnitId) ||
                u.SourceOrder < 0 || !orders.Add(u.SourceOrder) || !Enum.IsDefined(u.InitialTeam) || !Enum.IsDefined(u.CurrentTeam))
                throw new ArgumentException("Distinct explicit identities, source order and known teams are required.", nameof(units));
            if (u.Active && (!map.TryGetTile(u.Position, out var tile) || tile.IsBlocked))
                throw new ArgumentException("Active units require valid unblocked positions.", nameof(units));
            if ((unit.Current is null) != (unit.Vitals is null) || u.Active && unit.Current is null)
                throw new ArgumentException("Active units require paired current/vital captures.", nameof(units));
            if (unit.Current is not { } current) continue;
            FaithMagicInputs.ValidateCurrentUnit(current); var v = unit.Vitals!;
            if (current.UnitId != u.UnitId || v.MaxHp == 0 || v.Hp > v.MaxHp || current.Mp > v.MaxMp)
                throw new ArgumentException("Current identities and vital bounds must agree.", nameof(units));
            if (u.Active && (v.Hp == 0 || current.Statuses.Dead || current.Statuses.Petrify || current.Statuses.Wall))
                throw new NotSupportedException("Active projection units must be living and outside Petrify/Wall.");
        }
        Map = map; Units = captured; RngState = rngState;
    }

    public bool TryGetUnit(BattleUnitId id, out MagicProjectionUnit? unit)
    { unit = Units.FirstOrDefault(u => u.Occupant.UnitId == id); return unit is not null; }
}
