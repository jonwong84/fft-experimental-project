using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>A bounded unit-target command, validated atomically before publishing effects.</summary>
public sealed record AttackUnitCommand(BattleUnitId ActorId, BattleUnitId TargetId);
