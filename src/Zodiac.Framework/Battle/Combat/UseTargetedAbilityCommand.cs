using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record UseTargetedAbilityCommand(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId);
