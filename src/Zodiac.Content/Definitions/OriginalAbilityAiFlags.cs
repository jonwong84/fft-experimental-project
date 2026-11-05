using System.Collections.Immutable;
using Zodiac.Framework.Ai;

namespace Zodiac.Content.Definitions;

/// <summary>Original primary AI-byte decoding only. Never interprets the fourth static byte as runtime search policy.</summary>
internal static class OriginalAbilityAiFlags
{
    internal static AiAbilityBehavior Decode(ImmutableArray<byte> flags)
    {
        int target = flags[0], effect = flags[1], trajectory = flags[2];
        return new(TargetsAllies: (target & 1) != 0, TargetsEnemies: (target & 2) != 0,
            Unequips: (target & 4) != 0, AffectsStats: (target & 8) != 0,
            AddsStatus: (target & 16) != 0, CancelsStatus: (target & 32) != 0,
            AffectsMp: (target & 64) != 0, AffectsHp: (target & 128) != 0,
            AffectedBySilence: (effect & 1) != 0, Evadeable: (effect & 2) != 0,
            AffectedByFaith: (effect & 4) != 0, RandomUse: (effect & 8) != 0,
            FollowTarget: (effect & 16) != 0, UndeadReverse: (effect & 32) != 0,
            Reflectable: (effect & 64) != 0, TargetsMapTiles: (effect & 128) != 0,
            PhysicalAttack: (trajectory & 1) != 0, MagicalAttack: (trajectory & 2) != 0,
            HasExhaustiveAreaShape: (trajectory & 60) != 0,
            DirectTrajectory: (trajectory & 64) != 0, ArcTrajectory: (trajectory & 128) != 0);
    }
}
