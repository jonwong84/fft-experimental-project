using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record SpellTargetSelection(ImmutableArray<TilePosition> Panels, ImmutableArray<BattleUnitId> UnitIds);
