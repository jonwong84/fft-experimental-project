using System.Collections.Immutable;
using Zodiac.Framework.Ai;

namespace Zodiac.Content.Definitions;

/// <summary>Original primary AI-byte import/export boundary. Never interprets the fourth static byte as runtime search policy.</summary>
internal static class OriginalAbilityAiFlags
{
    internal sealed record Fields(AiAbilityBehavior Behavior, bool Usable, bool Motion, byte Unresolved);

    internal static Fields Read(IEnumerable<byte> aiFlagsRaw)
    {
        ArgumentNullException.ThrowIfNull(aiFlagsRaw);
        var flags = aiFlagsRaw.ToImmutableArray();
        if (flags.Length != 4) throw new ArgumentException("Exactly four raw AI flag bytes are required.", nameof(aiFlagsRaw));
        return new(Decode(flags), (flags[3] & 128) != 0, (flags[3] & 1) != 0, (byte)(flags[3] & 126));
    }

    internal static void ValidateUnresolved(byte unresolvedAiBitsRaw)
    {
        if ((unresolvedAiBitsRaw & 129) != 0)
            throw new ArgumentOutOfRangeException(nameof(unresolvedAiBitsRaw), "Known usable/motion bits must be supplied as booleans.");
    }

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
            AreaShape: (AiAbilityAreaShape)((trajectory >> 2) & 15),
            DirectTrajectory: (trajectory & 64) != 0, ArcTrajectory: (trajectory & 128) != 0);
    }

    internal static ImmutableArray<byte> Encode(AiAbilityBehavior b, bool usable, bool motion, byte unresolved) =>
        [(byte)((b.TargetsAllies ? 1 : 0) | (b.TargetsEnemies ? 2 : 0) | (b.Unequips ? 4 : 0) | (b.AffectsStats ? 8 : 0) |
            (b.AddsStatus ? 16 : 0) | (b.CancelsStatus ? 32 : 0) | (b.AffectsMp ? 64 : 0) | (b.AffectsHp ? 128 : 0)),
         (byte)((b.AffectedBySilence ? 1 : 0) | (b.Evadeable ? 2 : 0) | (b.AffectedByFaith ? 4 : 0) | (b.RandomUse ? 8 : 0) |
            (b.FollowTarget ? 16 : 0) | (b.UndeadReverse ? 32 : 0) | (b.Reflectable ? 64 : 0) | (b.TargetsMapTiles ? 128 : 0)),
         (byte)((b.PhysicalAttack ? 1 : 0) | (b.MagicalAttack ? 2 : 0) | ((int)b.AreaShape << 2) |
            (b.DirectTrajectory ? 64 : 0) | (b.ArcTrajectory ? 128 : 0)),
         (byte)((usable ? 128 : 0) | (motion ? 1 : 0) | unresolved)];
}
