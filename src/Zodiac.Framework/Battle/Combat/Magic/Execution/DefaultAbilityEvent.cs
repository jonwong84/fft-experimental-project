using System.Collections.Immutable;

namespace Zodiac.Framework;

public abstract record DefaultAbilityEvent
{
    public sealed record Tick(ulong Number) : DefaultAbilityEvent;
    public sealed record TurnReady(BattleUnitId UnitId) : DefaultAbilityEvent;
    public sealed record TurnEnded(BattleUnitId UnitId) : DefaultAbilityEvent;
    public sealed record Stepped(BattleUnitId UnitId, TilePosition Destination) : DefaultAbilityEvent;
    public sealed record Moved(BattleUnitId UnitId, TilePosition From, TilePosition Destination,
        BattleFacing PreviousFacing, FlatMovementPath Path) : DefaultAbilityEvent;
    public sealed record Queued(BattleUnitId UnitId, AssetId SpellId, byte ChargeTicks) : DefaultAbilityEvent;
    public sealed record Resolved(BattleUnitId UnitId, AssetId SpellId, int MpCost, TilePosition Center,
        ImmutableArray<MagicCastTargetResult> Targets) : DefaultAbilityEvent;
    public sealed record Fizzled(BattleUnitId UnitId, AssetId SpellId, int MpCost, string Reason) : DefaultAbilityEvent;
    public sealed record Applied(BattleUnitId UnitId, ushort Hp, bool Critical, bool Dead) : DefaultAbilityEvent;
    public sealed record Rewarded(BattleUnitId UnitId, byte Exp, int Jp, byte Level, byte JobLevel) : DefaultAbilityEvent;
    public sealed record EncounterEnded(BattleUnitId KnockedOutUnitId) : DefaultAbilityEvent;
    public sealed record LearningOffered(ulong ReportId, BattleUnitId UnitId, AssetId AbilityId) : DefaultAbilityEvent;
    public sealed record LearningAnswered(BattleUnitId UnitId, AssetId AbilityId, bool Accepted, bool Automatic = false) : DefaultAbilityEvent;
}
