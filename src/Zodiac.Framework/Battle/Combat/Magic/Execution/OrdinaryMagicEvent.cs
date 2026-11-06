using System.Collections.Immutable;

namespace Zodiac.Framework;

public abstract record OrdinaryMagicEvent
{
    public sealed record Tick(ulong Number) : OrdinaryMagicEvent;
    public sealed record TurnReady(BattleUnitId UnitId) : OrdinaryMagicEvent;
    public sealed record TurnEnded(BattleUnitId UnitId) : OrdinaryMagicEvent;
    public sealed record Stepped(BattleUnitId UnitId, TilePosition Destination) : OrdinaryMagicEvent;
    public sealed record Moved(BattleUnitId UnitId, TilePosition From, TilePosition Destination,
        BattleFacing PreviousFacing, FlatMovementPath Path) : OrdinaryMagicEvent;
    public sealed record Queued(BattleUnitId UnitId, AssetId SpellId, byte ChargeTicks) : OrdinaryMagicEvent;
    public sealed record Resolved(BattleUnitId UnitId, AssetId SpellId, int MpCost, TilePosition Center,
        ImmutableArray<MagicCastTargetResult> Targets) : OrdinaryMagicEvent;
    public sealed record Fizzled(BattleUnitId UnitId, AssetId SpellId, int MpCost, string Reason) : OrdinaryMagicEvent;
    public sealed record Applied(BattleUnitId UnitId, ushort Hp, bool Critical, bool Dead) : OrdinaryMagicEvent;
    public sealed record Rewarded(BattleUnitId UnitId, byte Exp, int Jp, byte Level, byte JobLevel) : OrdinaryMagicEvent;
    public sealed record EncounterEnded(BattleUnitId KnockedOutUnitId) : OrdinaryMagicEvent;
    public sealed record LearningOffered(ulong ReportId, BattleUnitId UnitId, AssetId AbilityId) : OrdinaryMagicEvent;
    public sealed record LearningAnswered(BattleUnitId UnitId, AssetId AbilityId, bool Accepted) : OrdinaryMagicEvent;
}
