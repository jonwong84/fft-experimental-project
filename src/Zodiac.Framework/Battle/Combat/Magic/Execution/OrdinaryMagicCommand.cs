namespace Zodiac.Framework;

public abstract record OrdinaryMagicCommand
{
    public sealed record Advance : OrdinaryMagicCommand;
    public sealed record AnswerLearning(ulong ReportId, BattleUnitId UnitId, bool Accept) : OrdinaryMagicCommand;
    public sealed record Cast(BattleUnitId UnitId, AssetId SpellId, TilePosition Aim, bool FollowUnit = true) : OrdinaryMagicCommand;
    public sealed record Wait(BattleUnitId UnitId) : OrdinaryMagicCommand;
    public sealed record Step(BattleUnitId UnitId, TilePosition Destination) : OrdinaryMagicCommand;
    public sealed record Move(BattleUnitId UnitId, TilePosition Destination) : OrdinaryMagicCommand;
}
