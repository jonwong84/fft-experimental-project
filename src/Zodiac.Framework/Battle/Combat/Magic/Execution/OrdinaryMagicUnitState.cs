using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Actual coherent unit/career fields owned by an ordinary magic battle.</summary>
public sealed record OrdinaryMagicUnitState(OrdinaryMagicUnitDefinition Definition, TilePosition Position,
    MagicCurrentUnit Current, MagicUnitVitals Vitals, BattleRawStats RawStats, byte Level, byte Experience,
    ImmutableDictionary<AssetId, JobPointBalance> JobPoints, ImmutableArray<AssetId> UnlockedJobs,
    byte KoCount = 0, byte DeathCounter = 3, AbilityOutcomeMarker OutcomeMarker = AbilityOutcomeMarker.None,
    BattleFacing Facing = BattleFacing.East)
{
    public BattleUnitId Id => Current.UnitId;
    public SpellTargetOccupant Occupant => Definition.Occupant with { Position = Position };
}
