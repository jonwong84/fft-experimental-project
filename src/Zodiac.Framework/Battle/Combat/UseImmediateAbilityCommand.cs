using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record UseImmediateAbilityCommand(BattleUnitId ActorId, AssetId AbilityId);
