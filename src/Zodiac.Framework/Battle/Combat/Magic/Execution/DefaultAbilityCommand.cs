namespace Zodiac.Framework;

public abstract record DefaultAbilityCommand
{
    public sealed record Advance : DefaultAbilityCommand;
    public sealed record AnswerLearning(ulong ReportId, BattleUnitId UnitId, bool Accept) : DefaultAbilityCommand;
    public sealed record Cast(BattleUnitId UnitId, AssetId SpellId, TilePosition Aim, bool FollowUnit = true) : DefaultAbilityCommand;
    public sealed record Wait(BattleUnitId UnitId) : DefaultAbilityCommand;
    public sealed record Step(BattleUnitId UnitId, TilePosition Destination) : DefaultAbilityCommand;
    public sealed record Move(BattleUnitId UnitId, TilePosition Destination) : DefaultAbilityCommand;
}
