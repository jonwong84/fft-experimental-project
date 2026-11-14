using System.Collections.Immutable;

namespace Zodiac.Framework;

public abstract record TwoUnitBattleCommand
{
    public sealed record Advance : TwoUnitBattleCommand;
    public sealed record Move(BattleUnitId UnitId, TilePosition Destination) : TwoUnitBattleCommand;
    public sealed record Attack(BattleUnitId ActorId, BattleUnitId TargetId) : TwoUnitBattleCommand;
    public sealed record UseAbility(BattleUnitId ActorId, AssetId AbilityId) : TwoUnitBattleCommand;
    public sealed record UseTargetedAbility(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId) : TwoUnitBattleCommand;
    public sealed record Wait(BattleUnitId UnitId) : TwoUnitBattleCommand;
}
