using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>A confirmed choice-consumption or end-turn decision; ConfirmAct does not resolve combat.</summary>
public sealed record TurnCommand(BattleUnitId UnitId, TurnCommandKind Kind);
