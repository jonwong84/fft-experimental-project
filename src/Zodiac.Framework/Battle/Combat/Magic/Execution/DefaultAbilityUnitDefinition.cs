using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Admitted human unit setup: no equipment/RSM, innate statuses, reactions or other special context.</summary>
public sealed record DefaultAbilityUnitDefinition(SpellTargetOccupant Occupant, MagicCurrentUnit Current,
    MagicUnitVitals Vitals, BattleRawStats RawStats, BattleGrowthRates Growth,
    BattleStatMultipliers Multipliers, BattleAttributeValues BaseAttributes, byte Level,
    byte Experience, byte OriginalJobId, AssetId RewardJobId,
    ImmutableDictionary<AssetId, JobPointBalance> JobPoints, ImmutableHashSet<AssetId> LearnedSpells,
    byte Move = 3, byte Jump = 3, BattleFacing InitialFacing = BattleFacing.East,
    ImmutableHashSet<int>? LearnedPrimarySlots = null, bool PlayerControlled = true);
