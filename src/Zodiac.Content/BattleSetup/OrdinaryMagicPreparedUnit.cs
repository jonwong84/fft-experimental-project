using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

public sealed record OrdinaryMagicPreparedUnit(string Id, string Name, int SourceOrder, int X, int Y,
    SpellTargetTeam Team, string JobId, byte Level, byte Experience, byte Faith, byte Brave,
    MagicZodiacSign Sign, bool Male, bool Female, BattleRawStats RawStats, BattleAttributeValues BaseAttributes,
    ushort Hp, ushort Mp, Dictionary<string, JobPointBalance> Career, string[] LearnedSpells,
    BattleFacing Facing = BattleFacing.East, int[]? LearnedPrimarySlots = null);
